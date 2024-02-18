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
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Caly.Core.ViewModels;

namespace Caly.Core.Services.Interfaces
{
    public interface IDialogService
    {
        /// <summary>
        /// <c>null</c> if cancelled.
        /// </summary>
        Task<string?> ShowPdfPasswordDialogAsync();

        /// <summary>
        /// Show a notification.
        /// </summary>
        void ShowNotification(string? title, string? message, NotificationType type);

        /// <summary>
        /// Show an exception in a popup window.
        /// </summary>
        Task ShowExceptionWindowAsync(Exception exception);

        /// <summary>
        /// Show an exception in a popup window.
        /// </summary>
        Task ShowExceptionWindowAsync(ExceptionViewModel exception);

        /// <summary>
        /// Show an exception in a popup window.
        /// </summary>
        void ShowExceptionWindow(Exception exception);

        /// <summary>
        /// Show an exception in a popup window.
        /// </summary>
        void ShowExceptionWindow(ExceptionViewModel exception);
    }
}
