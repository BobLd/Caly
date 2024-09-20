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
using Avalonia.Controls.Notifications;
using Caly.Core.Services.Interfaces;
using UglyToad.PdfPig.Logging;

namespace Caly.Core.Utilities
{
    internal sealed class CalyPdfPigLogger : ILog
    {
        private const string _annotationTitle = "Error in pdf document";

        private readonly IDialogService _dialogService;

        public CalyPdfPigLogger(IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(dialogService, nameof(dialogService));
            _dialogService = dialogService;
        }

        public void Debug(string message)
        {
        }

        public void Debug(string message, Exception ex)
        {
        }

        public void Warn(string message)
        {
        }

        public void Error(string message)
        {
            _dialogService.ShowNotification(_annotationTitle, message, NotificationType.Warning);
        }

        public void Error(string message, Exception ex)
        {
            // We ignore the ex for the moment
            _dialogService.ShowNotification(_annotationTitle, message, NotificationType.Warning);
        }
    }
}
