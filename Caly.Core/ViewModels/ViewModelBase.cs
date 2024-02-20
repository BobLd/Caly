using Avalonia.Threading;
using Caly.Core.Services.Interfaces;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private ExceptionViewModel? _exception;

    partial void OnExceptionChanging(ExceptionViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        var dialogService = App.Current?.Services?.GetRequiredService<IDialogService>();
        if (dialogService is null)
        {
            throw new NullReferenceException($"Missing {nameof(IDialogService)} instance.");
        }

        Dispatcher.UIThread.Post(() => dialogService.ShowExceptionWindowAsync(value));
    }
}
