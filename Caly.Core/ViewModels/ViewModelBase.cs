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
