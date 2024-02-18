using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Caly.Core.Services.Interfaces;

namespace Caly.Core.Services
{
    internal sealed class ClipboardService : IClipboardService
    {
        private readonly IClipboard _clipboard;

        public ClipboardService(Window target)
        {
            _clipboard = target.Clipboard ?? throw new ArgumentNullException($"Could not find {typeof(IClipboard)}");
        }

        public async Task SetAsync(string text)
        {
            await _clipboard.SetTextAsync(text);
        }

        public async Task ClearAsync()
        {
            await _clipboard.ClearAsync();
        }
    }
}
