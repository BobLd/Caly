using System.Threading.Tasks;
using Avalonia.Controls;
using Caly.Core.Services.Interfaces;
using Caly.Core.ViewModels;
using Caly.Core.Views;

namespace Caly.Core.Services
{
    internal sealed class DialogService : IDialogService
    {
        private readonly Window _target;

        public DialogService(Window target)
        {
            _target = target;
        }

        public Task<string?> ShowPdfPasswordDialogAsync()
        {
            var window = new PdfPasswordWindow();
            return window.ShowDialog<string?>(_target);
        }

        public async Task ShowErrorAsync(ExceptionViewModel exception)
        {
            Debug.ThrowNotOnUiThread();
            System.Diagnostics.Debug.WriteLine(exception.ToString());
            var window = new MessageWindow
            {
                DataContext = exception
            };
            await window.ShowDialog(_target);
        }

        public void ShowErrorWindow(ExceptionViewModel exception)
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
