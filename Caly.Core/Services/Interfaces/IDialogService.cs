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
