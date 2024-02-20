using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
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
        private readonly Window _target;
        private readonly MainViewModel _mainViewModel;
        private readonly IFilesService _filesService;
        private readonly IDialogService _dialogService;

        private readonly Channel<IStorageFile?> _fileChannel;
        private readonly ChannelWriter<IStorageFile?> _channelWriter;
        private readonly ChannelReader<IStorageFile?> _channelReader;

        private readonly ConcurrentDictionary<string, PdfDocumentViewModel> _openedFiles = new();

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
                        Dispatcher.UIThread.Post(() => _dialogService.ShowExceptionWindowAsync(e));
                    }
                });
            }
            catch (Exception e)
            {
                // TODO
                // Critical error - can't open document anymore
                System.Diagnostics.Debug.WriteLine($"ERROR in WorkerProc {e}");
                File.WriteAllText($"error_avalonia_worker_proc_{Guid.NewGuid()}.txt", e.ToString());
                Dispatcher.UIThread.Post(() => _dialogService.ShowExceptionWindowAsync(e));
                throw;
            }
        }

        public PdfDocumentsService(Window target, IFilesService filesService, IDialogService dialogService)
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
                AllowSynchronousContinuations = false,
                SingleReader = false,
                SingleWriter = false
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

            // TODO - Do we need the named mutex here?

            await document.CancelAsync();

            _mainViewModel.PdfDocuments.RemoveSafely(document);

            _openedFiles.TryRemove(document.LocalPath, out _);

            await document.CleanAfterClose();

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
            if (_openedFiles.TryGetValue(storageFile.Path.LocalPath, out var vm))
            {
                // Already open - Activate tab
                // We need a lock to avoid issues with tabs when opening documents in parallel (this might not be needed here though).
                int index = _mainViewModel.PdfDocuments.IndexOfSafely(vm);
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

                var pdfService = App.Current?.Services?.GetRequiredService<IPdfService>();
                if (pdfService is null)
                {
                    throw new NullReferenceException($"Missing {nameof(IPdfService)} instance.");
                }

                int pageCount = await pdfService.OpenDocument(storageFile, password, cancellationToken);

                if (pageCount > 0)
                {
                    var documentViewModel = new PdfDocumentViewModel(pdfService);
                    if (_openedFiles.TryAdd(storageFile.Path.LocalPath, documentViewModel))
                    {
                        // We need a lock to avoid issues with tabs when opening documents in parallel
                        _mainViewModel.PdfDocuments.AddSafely(documentViewModel);
                        BringMainWindowToFront();
                    }
                    else
                    {
                        // TODO - Why would that happen?
                        await Task.Run(pdfService.DisposeAsync, CancellationToken.None);
                    }
                }
                else
                {
                    await Task.Run(pdfService.DisposeAsync, CancellationToken.None);
                }
            }
        }

        private static string GetMutexName(string path)
        {
            // The backslash character (\) is reserved for mutex names
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(path)).Replace('\\', '#');
        }

        private void BringMainWindowToFront()
        {
            _target.Activate(); // Bring window to front

            Dispatcher.UIThread.Post(() =>
            {
                // Popup from taskbar
                if (_target.WindowState == WindowState.Minimized)
                {
                    _target.WindowState = WindowState.Normal;
                }
            });
        }
    }
}
