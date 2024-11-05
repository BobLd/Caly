// Copyright (C) 2024 BobLd
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY - without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.Services
{
    internal sealed class PdfDocumentsService : IPdfDocumentsService
    {
        private sealed class PdfDocumentRecord
        {
            public required AsyncServiceScope Scope { get; init; }

            public required PdfDocumentViewModel ViewModel { get; init; }
        }

        private readonly Visual _target;
        private readonly MainViewModel _mainViewModel;
        private readonly IFilesService _filesService;
        private readonly IDialogService _dialogService;

        private readonly Channel<IStorageFile?> _fileChannel;
        private readonly ChannelWriter<IStorageFile?> _channelWriter;
        private readonly ChannelReader<IStorageFile?> _channelReader;

        private readonly ConcurrentDictionary<string, PdfDocumentRecord> _openedFiles = new();

        private async Task ProcessDocumentsQueue(CancellationToken token)
        {
            try
            {
                Debug.ThrowOnUiThread();

                await Parallel.ForEachAsync(_channelReader.ReadAllAsync(token), token, async (d, ct) =>
                {
                    try
                    {
                        if (d is not null)
                        {
                            await OpenLoadDocumentInternal(d, null, ct);
                        }
                    }
                    catch (Exception e)
                    {
                        await _dialogService.ShowExceptionWindowAsync(e);
                    }
                });
            }
            catch (Exception e)
            {
                // Critical error - can't open document anymore
                System.Diagnostics.Debug.WriteLine($"ERROR in WorkerProc {e}");
                Debug.WriteExceptionToFile(e);
                await _dialogService.ShowExceptionWindowAsync(e);
                throw;
            }
        }

        public PdfDocumentsService(Visual target, IFilesService filesService, IDialogService dialogService)
        {
            Debug.ThrowNotOnUiThread();

            _target = target;

            if (_target.DataContext is not MainViewModel mvm)
            {
                throw new ArgumentException("Could not get a valid DataContext for the main window.");
            }

            _mainViewModel = mvm;

            _filesService = filesService ?? throw new NullReferenceException("Missing File Service instance.");
            _dialogService = dialogService ?? throw new NullReferenceException("Missing Dialog Service instance.");

            _fileChannel = Channel.CreateUnbounded<IStorageFile?>(new UnboundedChannelOptions()
            {
                AllowSynchronousContinuations = false, SingleReader = false, SingleWriter = false
            });
            _channelWriter = _fileChannel.Writer;
            _channelReader = _fileChannel.Reader;

            _ = Task.Run(() => ProcessDocumentsQueue(CancellationToken.None));
        }

        public async Task OpenLoadDocument(CancellationToken cancellationToken)
        {
            Debug.ThrowNotOnUiThread();

            IStorageFile? file = await _filesService.OpenPdfFileAsync();

            await Task.Run(() => OpenLoadDocument(file, cancellationToken), cancellationToken);
        }

        public async Task OpenLoadDocument(string? path, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                // TODO - Log
                return;
            }

            var file = await _filesService.TryGetFileFromPathAsync(path);

            await OpenLoadDocument(file, cancellationToken);
        }

        public async Task OpenLoadDocument(IStorageFile? storageFile, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            await _channelWriter.WriteAsync(storageFile, cancellationToken);
        }

        public async Task OpenLoadDocuments(IEnumerable<IStorageItem?> storageFiles, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            foreach (IStorageItem? item in storageFiles)
            {
                if (item is not IStorageFile file)
                {
                    continue;
                }

                await OpenLoadDocument(file, cancellationToken);
            }
        }

        public async Task CloseUnloadDocument(PdfDocumentViewModel? document)
        {
            Debug.ThrowOnUiThread();

            if (document is null)
            {
                return;
            }

            if (string.IsNullOrEmpty(document.LocalPath))
            {
                throw new Exception($"Invalid {nameof(document.LocalPath)} value for view model.");
            }

            await document.CancelAsync();

            _mainViewModel.PdfDocuments.RemoveSafely(document);

            if (_openedFiles.TryRemove(document.LocalPath, out var docRecord))
            {
                await docRecord.Scope.DisposeAsync();
            }
            else
            {
                // TODO - Log error
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }

        private async Task OpenLoadDocumentInternal(IStorageFile? storageFile, string? password, CancellationToken cancellationToken)
        {
            Debug.ThrowOnUiThread();

            if (storageFile is null)
            {
                // TODO - Log
                return;
            }

            // TODO - Look into Avalonia bookmark
            // string? id = await storageFile.SaveBookmarkAsync();

            // Check if file is already open
            if (_openedFiles.TryGetValue(storageFile.Path.LocalPath, out var doc))
            {
                // Already open - Activate tab
                // We need a lock to avoid issues with tabs when opening documents in parallel (this might not be needed here though).
                int index = _mainViewModel.PdfDocuments.IndexOfSafely(doc.ViewModel);
                if (index != -1 && _mainViewModel.SelectedDocumentIndex != index)
                {
                    _mainViewModel.SelectedDocumentIndex = index;
                }

                BringMainWindowToFront();
                return;
            }

            // We use a named mutex to ensure a single file with the same path is only opened once
            using (new Mutex(true, GetMutexName(storageFile.Path.LocalPath), out bool created))
            {
                if (!created)
                {
                    // Already processing
                    return;
                }

                var scope = App.Current!.Services!.CreateAsyncScope();

                var documentViewModel = scope.ServiceProvider.GetRequiredService<PdfDocumentViewModel>();

                BringMainWindowToFront();

                // We need a lock to avoid issues with tabs when opening documents in parallel
                _mainViewModel.PdfDocuments.AddSafely(documentViewModel); // Add the pdf document straight away

                int pageCount;
                try
                {
                    pageCount = await documentViewModel.OpenDocument(storageFile, password, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    // TODO - Log error
                    _mainViewModel.PdfDocuments.RemoveSafely(documentViewModel);
                    await Task.Run(scope.DisposeAsync, CancellationToken.None);
                    throw;
                }

                if (pageCount > 0)
                {
                    var docRecord = new PdfDocumentRecord()
                    {
                        Scope = scope,
                        ViewModel = documentViewModel
                    };

                    if (_openedFiles.TryAdd(storageFile.Path.LocalPath, docRecord))
                    {
                        await Task.WhenAll(
                                documentViewModel.LoadPagesTask,
                                documentViewModel.LoadBookmarksTask,
                                documentViewModel.LoadPropertiesTask)
                            .ConfigureAwait(false);
                        return;
                    }
                }

                // TODO - Log error
                _mainViewModel.PdfDocuments.RemoveSafely(documentViewModel);
                await Task.Run(scope.DisposeAsync, CancellationToken.None);
            }
        }

        private static string GetMutexName(string path)
        {
            // The backslash character (\) is reserved for mutex names
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(path)).Replace('\\', '#');
        }

        private void BringMainWindowToFront()
        {
            if (_target is not Window w)
            {
                return;
            }

            w.Activate(); // Bring window to front

            Dispatcher.UIThread.Post(() =>
            {
                // Popup from taskbar
                if (w.WindowState == WindowState.Minimized)
                {
                    w.WindowState = WindowState.Normal;
                }
            });
        }
    }
}
