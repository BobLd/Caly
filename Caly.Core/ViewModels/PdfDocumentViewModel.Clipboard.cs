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
using Caly.Core.Models;
using Caly.Core.Services.Interfaces;
using Caly.Core.Utilities;
using Caly.Pdf.Models;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.ViewModels
{
    public partial class PdfDocumentViewModel
    {
        [RelayCommand(CanExecute = nameof(CanCopyText))]
        private async Task CopyText(CancellationToken token)
        {
            try
            {
                PdfTextSelection selection = TextSelectionHandler.Selection;

                if (!selection.IsValid)
                {
                    return;
                }

                // https://docs.avaloniaui.net/docs/next/concepts/services/clipboardS

                IClipboardService clipboardService = App.Current?.Services?.GetRequiredService<IClipboardService>() ??
                                                      throw new NullReferenceException($"Missing {nameof(IClipboardService)} instance.");

                System.Diagnostics.Debug.WriteLine("Starting SetClipboardAsync");

                string text = await Task.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("Starting SetClipboardAsync: Get text");
                    var sb = new StringBuilder();

                    await foreach (var word in selection
                                       .GetDocumentSelectionAsAsync(w => w.Value,
                                           PartialWord, this,
                                           token))
                    {
                        sb.AppendReadOnlySequence(word);
                        if (sb.Length > 0 && !char.IsWhiteSpace(sb[^1]))
                        {
                            sb.Append(' ');
                        }
                    }

                    sb.Length--; // Last char added was a space
                    return sb.ToString();

                    static ReadOnlySequence<char> PartialWord(PdfWord word, int startIndex, int endIndex)
                    {
                        System.Diagnostics.Debug.Assert(startIndex != -1);
                        System.Diagnostics.Debug.Assert(endIndex != -1);
                        System.Diagnostics.Debug.Assert(startIndex <= endIndex);

                        return word.Value.Slice(startIndex, endIndex - startIndex + 1);
                    }
                }, token);

                System.Diagnostics.Debug.WriteLine("Starting SetClipboardAsync: Get text Done");

                await clipboardService.SetAsync(text);
                System.Diagnostics.Debug.WriteLine("Ended SetClipboardAsync");
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.ToString());
                Exception = new ExceptionViewModel(e);
            }
        }

        private bool CanCopyText()
        {
            return TextSelectionHandler.Selection.IsValid;
        }
    }
}
