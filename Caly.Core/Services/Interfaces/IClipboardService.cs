using System.Threading.Tasks;

namespace Caly.Core.Services.Interfaces
{
    internal interface IClipboardService
    {
        /// <summary>
        /// Set clipboard.
        /// </summary>
        Task SetAsync(string text);

        /// <summary>
        /// Clear clipboard.
        /// </summary>
        Task ClearAsync();
    }
}
