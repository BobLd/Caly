using Caly.Core.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Avalonia.Collections;
using Tabalonia.Controls;

namespace Caly.Core.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    public ObservableCollection<PdfDocumentViewModel> PdfDocuments { get; } = new();

    [ObservableProperty] private int _selectedDocumentIndex;

    private readonly IDisposable documentCollectionDisposable;

    public MainViewModel()
    {
        // TODO - Dispose to unsubscribe
        documentCollectionDisposable = PdfDocuments
            .GetWeakCollectionChangedObservable()
            .ObserveOn(Scheduler.Default)
            .Subscribe(async e =>
            {
                try
                {
                    if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems?.Count > 0)
                    {
                        foreach (var newDoc in e.NewItems.OfType<PdfDocumentViewModel>())
                        {
                            await Task.WhenAll(newDoc.LoadPages(), newDoc.LoadBookmarks());
                        }

                        SelectedDocumentIndex = PdfDocuments.Count - 1;
                    }
                }
                catch (OperationCanceledException)
                {
                    // No op
                }
                catch (Exception ex)
                {
                    await System.IO.File.WriteAllTextAsync($"error_avalonia_documents_obs_{Guid.NewGuid()}.txt",
                        ex.ToString());
                    //throw;
                    Exception = new ExceptionViewModel(ex);
                }
            });
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
            await System.IO.File.WriteAllTextAsync($"error_avalonia_open_file_{Guid.NewGuid()}.txt", e.ToString());
            //throw;
            //ErrorMessages?.Add(e.Message);
            Exception = new ExceptionViewModel(e);
        }
    }

    [RelayCommand]
    private void Purge()
    {
        SKGraphics.PurgeAllCaches();
    }

    [RelayCommand]
    private async Task CloseTab(object tabItem)
    {
        // TODO - Finish proper dispose / unload of document on close 
        if (((DragTabItem)tabItem)?.DataContext is PdfDocumentViewModel vm)
        {
            var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>();
            if (pdfDocumentsService is null)
            {
                throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");
            }

            await Task.Run(() => pdfDocumentsService.CloseUnloadDocument(vm));

            // TODO - Look into TabsControl's CloseItem / RemoveItem behaviour
        }
        else
        {
            // No op
        }
    }
}