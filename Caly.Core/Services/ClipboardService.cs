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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Caly.Core.Services.Interfaces;

namespace Caly.Core.Services
{
    internal sealed class ClipboardService : IClipboardService
    {
        private readonly Visual _target;

        public ClipboardService(Visual target)
        {
            _target = target;
        }

        public async Task SetAsync(string text)
        {
            var _clipboard = TopLevel.GetTopLevel(_target)?.Clipboard ??
                             throw new ArgumentNullException($"Could not find {typeof(IClipboard)}");
            await _clipboard.SetTextAsync(text);
        }

        public async Task ClearAsync()
        {
            var _clipboard = TopLevel.GetTopLevel(_target)?.Clipboard ??
                             throw new ArgumentNullException($"Could not find {typeof(IClipboard)}");
            await _clipboard.ClearAsync();
        }
    }
}
