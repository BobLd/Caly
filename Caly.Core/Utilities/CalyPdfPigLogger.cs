using System;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Caly.Core.Services.Interfaces;
using UglyToad.PdfPig.Logging;

namespace Caly.Core.Utilities
{
    internal sealed class CalyPdfPigLogger : ILog
    {
        private readonly IDialogService _dialogService;

        public CalyPdfPigLogger(IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(dialogService, nameof(dialogService));
            _dialogService = dialogService;
        }

        public void Debug(string message)
        {
            //Dispatcher.UIThread.Post(() => _dialogService.ShowNotification(null, message, NotificationType.Information));
        }

        public void Debug(string message, Exception ex)
        {
            //Dispatcher.UIThread.Post(() => _dialogService.ShowNotification(null, message, NotificationType.Information));
        }

        public void Warn(string message)
        {
            Dispatcher.UIThread.Post(() => _dialogService.ShowNotification(null, message, NotificationType.Warning));
        }

        public void Error(string message)
        {
            Dispatcher.UIThread.Post(() => _dialogService.ShowNotification(null, message, NotificationType.Error));
        }

        public void Error(string message, Exception ex)
        {
            // We ignore the ex for the moment
            Dispatcher.UIThread.Post(() => _dialogService.ShowNotification(null, message, NotificationType.Error));
        }
    }
}
