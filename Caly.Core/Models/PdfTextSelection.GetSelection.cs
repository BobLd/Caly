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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;

namespace Caly.Core.Models
{
    public partial class PdfTextSelection
    {
        public IEnumerable<int> GetSelectedPagesIndexes()
        {
            if (!IsValid())
            {
                yield break;
            }

            int start = GetStartPageIndex();
            int end = GetEndPageIndex();

            System.Diagnostics.Debug.Assert(start <= end);

            if (start > end)
            {
                throw new ArgumentOutOfRangeException($"The selection's start page ({start}) is after the end page ({end}).");
            }

            for (int p = start; p <= end; ++p)
            {
                yield return p;
            }
        }

        private IReadOnlyList<PdfWord>? GetPageSelectedWords(int pageNumber)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber <= NumberOfPages);
#endif

            return _selectedWords[pageNumber - 1];
        }

        /// <summary>
        /// Get the page selection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pageNumber">The page number. Starts at 1.</param>
        /// <param name="processFull">Handle fully selected words.</param>
        /// <param name="processPartial">Handle partially selected words.</param>
        /// <param name="page">The page view model. It will be used to set the page selection if need be.</param>
        /// <param name="token"></param>
        private async IAsyncEnumerable<T> GetPageSelectionAsAsync<T>(int pageNumber, Func<PdfWord, T> processFull,
            Func<PdfWord, int, int, T> processPartial, PdfPageViewModel page, [EnumeratorCancellation] CancellationToken token)
        {
            // TODO - merge words on same line
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber >= 1 && pageNumber <= NumberOfPages);
#endif
            System.Diagnostics.Debug.Assert(pageNumber == page.PageNumber);

            IReadOnlyList<PdfWord>? selectedWords = GetPageSelectedWords(pageNumber);

            if (selectedWords is null)
            {
                // We need to load the layer
                await page.LoadInteractiveLayer(token);
                selectedWords = GetPageSelectedWords(pageNumber);

                System.Diagnostics.Debug.WriteLine($"GetPageSelectionAsAsync: loaded page interactive layer {pageNumber}.");

                if (selectedWords is null)
                {
                    yield break;
                }
            }

            int i = 0;
            foreach (T w in GetPageSelectionAs(selectedWords, pageNumber, processFull, processPartial))
            {
                if (i % 100 == 0) // Check every 100 words
                {
                    token.ThrowIfCancellationRequested();
                }

                yield return w;
                i++;
            }
        }

        /// <summary>
        /// Get the page selection, if already set.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pageNumber">The page number. Starts at 1.</param>
        /// <param name="processFull">Handle fully selected words.</param>
        /// <param name="processPartial">Handle partially selected words.</param>
        public IEnumerable<T> GetPageSelectionAs<T>(int pageNumber, Func<PdfWord, T> processFull,
            Func<PdfWord, int, int, T> processPartial)
        {
            // TODO - merge words on same line
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber >= 1 && pageNumber <= NumberOfPages);
#endif

            var selectedWords = GetPageSelectedWords(pageNumber);
            if (selectedWords is null)
            {
                // TODO - We need to load the layer
                yield break;
            }

            foreach (T w in GetPageSelectionAs(selectedWords, pageNumber, processFull, processPartial))
            {
                yield return w;
            }
        }

        private IEnumerable<T> GetPageSelectionAs<T>(IReadOnlyList<PdfWord> selectedWords, int pageNumber,
            Func<PdfWord, T> processFull, Func<PdfWord, int, int, T> processPartial)
        {
            if (selectedWords.Count == 0)
            {
                yield break;
            }

            int wordStartIndex;
            int wordEndIndex;

            if (IsBackward)
            {
                wordStartIndex = FocusPageIndex == pageNumber ? FocusOffset : -1;
                wordEndIndex = AnchorPageIndex == pageNumber ? AnchorOffset : -1;
            }
            else
            {
                wordStartIndex = AnchorPageIndex == pageNumber ? AnchorOffset : -1;
                wordEndIndex = FocusPageIndex == pageNumber ? FocusOffset : -1;
            }

            if (wordStartIndex == -1 && wordEndIndex == -1)
            {
                // Only whole words
                foreach (var word in selectedWords)
                {
                    yield return processFull(word); // Return full words
                }

                yield break; // We are done
            }

            if (selectedWords.Count == 1)
            {
                // Single word selected
                var word = selectedWords[0];
                int lastIndex = word.Letters!.Count - 1;
                if ((wordStartIndex == -1 || wordStartIndex == 0) && (wordEndIndex == -1 || wordEndIndex == lastIndex))
                {
                    yield return processFull(word);
                }
                else
                {
                    yield return processPartial(word, wordStartIndex == -1 ? 0 : wordStartIndex,
                        wordEndIndex == -1 ? lastIndex : wordEndIndex);
                }

                yield break;
            }

            // Do first word
            var firstWord = selectedWords[0];
            if (wordStartIndex != -1 && wordStartIndex != 0)
            {
                int endIndex = firstWord.Letters!.Count - 1;
                if (wordStartIndex > endIndex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: wordStartIndex {wordStartIndex} is larger than {endIndex}.");
                    wordStartIndex = endIndex;
                }

                yield return processPartial(firstWord, wordStartIndex, endIndex);
            }
            else
            {
                yield return processFull(firstWord);
            }

            // Do words in the middle
            for (int i = 1; i < selectedWords.Count - 1; ++i)
            {
                var word = selectedWords[i];
                yield return processFull(word);
            }

            // Do last word
            var lastWord = selectedWords[^1];
            if (wordEndIndex != -1 && wordEndIndex != lastWord.Letters!.Count - 1)
            {
                yield return processPartial(lastWord, 0, wordEndIndex);
            }
            else
            {
                yield return processFull(lastWord);
            }
        }

        /// <summary>
        /// Get the document selection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="processFull">Handle fully selected words.</param>
        /// <param name="processPartial">Handle partially selected words.</param>
        /// <param name="document">The document view model. It will be used to load the text layer if not already loaded.</param>
        /// <param name="token"></param>
        public async IAsyncEnumerable<T> GetDocumentSelectionAsAsync<T>(Func<PdfWord, T> processFull,
            Func<PdfWord, int, int, T> processPartial, PdfDocumentViewModel document, [EnumeratorCancellation] CancellationToken token)
        {
            foreach (int p in GetSelectedPagesIndexes())
            {
                await foreach (T b in GetPageSelectionAsAsync(p, processFull, processPartial, document.Pages[p - 1], token))
                {
                    yield return b;
                }
            }
        }
    }
}
