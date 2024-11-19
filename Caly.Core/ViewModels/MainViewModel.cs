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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Tabalonia.Controls;

namespace Caly.Core.ViewModels
{
    public sealed partial class MainViewModel : ViewModelBase
    {
        private readonly IDisposable _documentCollectionDisposable;

        public ObservableCollection<PdfDocumentViewModel> PdfDocuments { get; } = new();

        [ObservableProperty] private int _selectedDocumentIndex;

        [ObservableProperty] private bool _isPaneOpen;

        [ObservableProperty] private string _version = CalyExtensions.GetCalyVersion();

#if DEBUG
        partial void OnSelectedDocumentIndexChanged(int oldValue, int newValue)
        {
            System.Diagnostics.Debug.WriteLine($"Selected Document Index changed from {oldValue} to {newValue}.");
        }
#endif

        public MainViewModel()
        {
            // TODO - Dispose to unsubscribe
            _documentCollectionDisposable = PdfDocuments
                .GetWeakCollectionChangedObservable()
                .ObserveOn(Scheduler.Default)
                .Subscribe(async e =>
                {
                    // NB: Tabalonia uses a Remove + Add when moving tabs
                    try
                    {
                        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
                        {
                            foreach (var newDoc in e.NewItems.OfType<PdfDocumentViewModel>())
                            {
                                await Task.WhenAll(newDoc.LoadPagesTask, newDoc.LoadBookmarksTask, newDoc.LoadPropertiesTask);
                            }

                            SelectedDocumentIndex = e.NewStartingIndex;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // No op
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteExceptionToFile(ex);
                        Exception = new ExceptionViewModel(ex);
                    }
                });
        }

        private PdfDocumentViewModel? GetCurrentPdfDocument()
        {
            try
            {
                return (SelectedDocumentIndex < 0 || PdfDocuments.Count == 0) ? null : PdfDocuments[SelectedDocumentIndex];
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
                return null;
            }
        }

        [RelayCommand]
        private async Task OpenFile(CancellationToken token)
        {
            try
            {
                var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>();
                if (pdfDocumentsService is null)
                {
                    throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");
                }

                await pdfDocumentsService.OpenLoadDocument(token);
            }
            catch (OperationCanceledException)
            {
                // No op
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
                Exception = new ExceptionViewModel(e);
            }
        }

        [RelayCommand]
        private async Task CloseTab(object tabItem)
        {
            // TODO - Finish proper dispose / unload of document on close 
            if (((DragTabItem)tabItem)?.DataContext is PdfDocumentViewModel vm)
            {
                await CloseDocumentInternal(vm);
            }
        }

        [RelayCommand]
        private async Task CloseDocument(CancellationToken token)
        {
            PdfDocumentViewModel? vm = GetCurrentPdfDocument();
            if (vm is null)
            {
                return;
            }
            await CloseDocumentInternal(vm);
        }

        private async Task CloseDocumentInternal(PdfDocumentViewModel vm)
        {
            IPdfDocumentsService pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>()
                ?? throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");

            await Task.Run(() => pdfDocumentsService.CloseUnloadDocument(vm));
        }

        [RelayCommand]
        private void ActivateSearchTextTab()
        {
            GetCurrentPdfDocument()?.ActivateSearchTextTabCommand.Execute(null);
        }

        [RelayCommand]
        private Task CopyText(CancellationToken token)
        {
            PdfDocumentViewModel? vm = GetCurrentPdfDocument();
            return vm is null ? Task.CompletedTask : vm.CopyTextCommand.ExecuteAsync(null);
        }

        [RelayCommand]
        private void ActivateNextDocument()
        {
            int lastIndex = PdfDocuments.Count - 1;

            if (lastIndex <= 0)
            {
                return;
            }

            int newIndex = SelectedDocumentIndex + 1;

            if (newIndex > lastIndex)
            {
                newIndex = 0;
            }
            SelectedDocumentIndex = newIndex;
        }

        [RelayCommand]
        private void ActivatePreviousDocument()
        {
            int lastIndex = PdfDocuments.Count - 1;

            if (lastIndex <= 0)
            {
                return;
            }

            int newIndex = SelectedDocumentIndex - 1;

            if (newIndex < 0)
            {
                newIndex = lastIndex;
            }
            SelectedDocumentIndex = newIndex;
        }
    }
}