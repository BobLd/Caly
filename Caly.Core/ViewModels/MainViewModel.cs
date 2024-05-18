﻿// Copyright (C) 2024 BobLd
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

namespace Caly.Core.ViewModels;

public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IDisposable _documentCollectionDisposable;

    public ObservableCollection<PdfDocumentViewModel> PdfDocuments { get; } = new();

    [ObservableProperty] private int _selectedDocumentIndex;

    [ObservableProperty] private bool _isPaneOpen = !CalyExtensions.IsMobilePlatform();

    public MainViewModel()
    {
        // TODO - Dispose to unsubscribe
        _documentCollectionDisposable = PdfDocuments
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
                            await Task.WhenAll(newDoc.LoadPagesTask, newDoc.LoadBookmarksTask);
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
                    Debug.WriteExceptionToFile(ex);
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
            var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>();
            if (pdfDocumentsService is null)
            {
                throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");
            }

            await Task.Run(() => pdfDocumentsService.CloseUnloadDocument(vm));
        }
    }
}