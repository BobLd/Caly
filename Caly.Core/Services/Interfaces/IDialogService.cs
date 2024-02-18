using System.Threading.Tasks;
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
        /// Show an exception popup.
        /// </summary>
        Task ShowErrorAsync(ExceptionViewModel exception);

        void ShowErrorWindow(ExceptionViewModel exception);
    }
}
