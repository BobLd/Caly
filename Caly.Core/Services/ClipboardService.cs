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
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;

namespace Caly.Core.Services
{
    internal sealed class ClipboardService : IClipboardService
    {
        // English rules
        private static ReadOnlySpan<char> _noWhitespaceAfter => [' ', '(', '[', '{'];
        private static ReadOnlySpan<char> _noWhitespaceBefore => [' ', ')', ']', '}', ':', '.', '′', '\'', ',', '?', '!'];

        private readonly Visual _target;

        public ClipboardService(Visual target)
        {
            _target = target;
        }

        public async Task SetAsync(PdfDocumentViewModel document, CancellationToken token)
        {
            // TODO - Check use of tasks here

            PdfTextSelection selection = document.TextSelectionHandler.Selection;

            if (!selection.IsValid)
            {
                return;
            }
            
            // https://docs.avaloniaui.net/docs/next/concepts/services/clipboardS

            System.Diagnostics.Debug.WriteLine("Starting IClipboardService.SetAsync");

            string text = await Task.Run(async () =>
            {
                var sb = new StringBuilder();

                await foreach (var word in selection
                                   .GetDocumentSelectionAsAsync(w => w.Value,
                                       PartialWord, document,
                                       token))
                {
                    if (word.IsEmpty)
                    {
                        continue;
                    }

                    if (_noWhitespaceBefore.Contains(word.FirstSpan[0]) && char.IsWhiteSpace(sb[^1]))
                    {
                        sb.Length--;
                    }

                    sb.AppendReadOnlySequence(word);

                    if (sb.Length == 0 || _noWhitespaceAfter.Contains(sb[^1]))
                    {
                        continue;
                    }

                    sb.Append(' ');
                }

                if (sb[^1] == ' ')
                {
                    sb.Length--; // Last char added was a space
                }

                return sb.ToString();
            }, token);

            await SetAsync(text);
            System.Diagnostics.Debug.WriteLine("Ended IClipboardService.SetAsync");
        }

        public async Task SetAsync(string text)
        {
            IClipboard clipboard = TopLevel.GetTopLevel(_target)?.Clipboard ??
                                   throw new ArgumentNullException($"Could not find {typeof(IClipboard)}");

            await clipboard.SetTextAsync(text);
        }

        public async Task ClearAsync()
        {
            IClipboard clipboard = TopLevel.GetTopLevel(_target)?.Clipboard ??
                                   throw new ArgumentNullException($"Could not find {typeof(IClipboard)}");

            await clipboard.ClearAsync();
        }
        
        private static ReadOnlySequence<char> PartialWord(PdfWord word, int startIndex, int endIndex)
        {
            // TODO - We have an issue with words containing ligatures (e.g. 'f‌i', 'ff', 'fl')
            // The index seems to not be correct as there is 1 bounding box for e.g. 2 chars.
            // The startIndex / endIndex being based on bounding boxes, the index is lagged.

            System.Diagnostics.Debug.Assert(startIndex != -1);
            System.Diagnostics.Debug.Assert(endIndex != -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);

            endIndex = word.GetCharIndexFromBboxIndex(endIndex);

            return word.Value.Slice(startIndex, endIndex - startIndex + 1);
        }
    }
}
