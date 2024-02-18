using System;
using Avalonia.Threading;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
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
            // No op
        }

        public void Debug(string message, Exception ex)
        {
            // No op
        }

        public void Warn(string message)
        {
            //Dispatcher.UIThread.Post(() => _dialogService.ShowErrorAsync(new ExceptionViewModel(new Exception(message))));
        }

        public void Error(string message)
        {
            Dispatcher.UIThread.Post(() => _dialogService.ShowErrorAsync(new ExceptionViewModel(new Exception(message))));
        }

        public void Error(string message, Exception ex)
        {
            Dispatcher.UIThread.Post(() => _dialogService.ShowErrorAsync(new ExceptionViewModel(ex)));
        }
    }
}
