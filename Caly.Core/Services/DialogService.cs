using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Core.Views;

namespace Caly.Core.Services
{
    internal sealed class DialogService : IDialogService
    {
        private readonly TimeSpan _annotationExpiration = TimeSpan.FromSeconds(60);
        private readonly Window _target;

        private WindowNotificationManager? _windowNotificationManager;

        public DialogService(Window target)
        {
            _target = target;
            _target.Loaded += _window_Loaded;
        }

        private void _window_Loaded(object? sender, RoutedEventArgs e)
        {
            if (sender is MainWindow mw)
            {
                _windowNotificationManager = mw.NotificationManager;
            }
            else
            {
                throw new InvalidOperationException($"Expecting '{typeof(MainWindow)}' but got '{sender?.GetType()}'.");
            }
        }

        public Task<string?> ShowPdfPasswordDialogAsync()
        {
            return new PdfPasswordWindow().ShowDialog<string?>(_target);
        }

        public void ShowNotification(string? title, string? message, NotificationType type)
        {
            Debug.ThrowNotOnUiThread();
            System.Diagnostics.Debug.WriteLine($"Annotation ({type}): {title}\n{message}");
            if (_windowNotificationManager is not null)
            {
                _windowNotificationManager.Show(new Notification(title, message, type, _annotationExpiration));
            }
            else
            {
                // TODO - we need a queue system to display the annotations when the manager is loaded
                System.Diagnostics.Debug.WriteLine($"Annotation (ERROR NOT LOADED) ({type}): {title}\n{message}");
            }
        }

        public Task ShowExceptionWindowAsync(Exception exception)
        {
            return ShowExceptionWindowAsync(new ExceptionViewModel(exception));
        }

        public async Task ShowExceptionWindowAsync(ExceptionViewModel exception)
        {
            Debug.ThrowNotOnUiThread();

            System.Diagnostics.Debug.WriteLine(exception.ToString());

            var window = new MessageWindow
            {
                DataContext = exception
            };
            await window.ShowDialog(_target);
        }

        public void ShowExceptionWindow(Exception exception)
        {
            ShowExceptionWindow(new ExceptionViewModel(exception));
        }

        public void ShowExceptionWindow(ExceptionViewModel exception)
        {
            Debug.ThrowNotOnUiThread();
            System.Diagnostics.Debug.WriteLine(exception.ToString());
            var window = new MessageWindow
            {
                DataContext = exception
            };
            window.Show();
        }
    }
}
