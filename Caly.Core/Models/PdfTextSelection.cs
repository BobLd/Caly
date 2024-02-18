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
using System.Linq;
using Avalonia;
using Caly.Core.Controls;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Geometry;

namespace Caly.Core.Models
{
    // References:
    // - https://www.w3.org/TR/selection-api/
    // - https://github.com/AvaloniaUI/Avalonia.HtmlRenderer/blob/master/Source/HtmlRenderer/Core/Handlers/SelectionHandler.cs
    // - https://developer.mozilla.org/en-US/docs/Web/API/Selection

    /// <summary>
    /// A selection of text in a pdf document.
    /// <para>
    /// Note: Anchor and focus should not be confused with the start and end positions of a selection.
    /// The anchor can be placed before the focus or vice versa, depending on the direction you made your selection.
    /// </para>
    /// See <see href="https://developer.mozilla.org/en-US/docs/Web/API/Selection"/>.
    /// </summary>
    public sealed class PdfTextSelection
    {
        /*
         * Note: Anchor and focus should not be confused with the start and end positions of a selection.
         * The anchor can be placed before the focus or vice versa, depending on the direction you made your selection.
         *
         * The anchor is where the user began the selection and the focus is where the user ends the selection. If you
         * make a selection with a desktop mouse, the anchor is placed where you pressed the mouse button, and the
         * focus is placed where you released the mouse button.
         */

        /// <summary>
        /// The mouse location when selection started.
        /// <para>Used to ignore small selections.</para>
        /// </summary>
        public Point? AnchorPoint { get; private set; }

        public int AnchorPageIndex { get; private set; } = -1;

        public int FocusPageIndex { get; private set; } = -1;

        /// <summary>
        /// The <see cref="PdfWord"/> in which the selection begins. Can be <c>null</c> if selection never existed in the document.
        /// The anchor is where the user began the selection. If the selection is made with a desktop mouse, the anchor is placed where the user pressed the mouse button.
        /// <para>
        /// Note: Anchor and focus should not be confused with the start and end positions of a selection.
        /// The anchor can be placed before the focus or vice versa, depending on the direction you made your selection.
        /// </para>
        /// </summary>
        public PdfWord? AnchorWord { get; private set; }

        /// <summary>
        /// The <see cref="PdfWord"/> in which the selection ends. Can be <c>null</c> if selection never existed in the document.
        /// The focus is where the user ends the selection. If the selection is made with a desktop mouse, the focus is placed where the user released the mouse button.
        /// <para>
        /// Note: Anchor and focus should not be confused with the start and end positions of a selection.
        /// The anchor can be placed before the focus or vice versa, depending on the direction you made your selection.
        /// </para>
        /// </summary>
        public PdfWord? FocusWord { get; private set; }

        /// <summary>
        /// The number of characters that the selection's anchor is offset within the <see cref="AnchorWord"/>, if it is partially selected.
        /// <list type="bullet">
        /// <item>This number is zero-based. If the selection begins with the first character in the <see cref="AnchorWord"/>, the value is <c>0</c>.</item>
        /// <item>If the <see cref="AnchorWord"/> is not selected (<c>null</c>) or fully selected, the value is <c>-1</c>.</item>
        /// </list>
        /// </summary>
        public int AnchorOffset { get; private set; } = -1;

        /// <summary>
        /// The number of characters that the selection's focus is offset within the <see cref="FocusWord"/>, if it is partially selected.
        /// <list type="bullet">
        /// <item>This number is zero-based. If the selection ends with the first character in the <see cref="FocusWord"/>, the value is <c>0</c>.</item>
        /// <item>If the <see cref="FocusWord"/> is not selected (<c>null</c>) or fully selected, the value is <c>-1</c>.</item>
        /// </list>
        /// </summary>
        public int FocusOffset { get; private set; } = -1;

        /// <summary>
        /// The selection start offset distance if the first selected word is partially selected (-1 if not selected or fully selected).
        /// </summary>
        public double AnchorOffsetDistance { get; private set; } = -1;

        /// <summary>
        /// The selection end offset distance if the last selected word is partially selected (-1 if not selected or fully selected).
        /// </summary>
        public double FocusOffsetDistance { get; private set; } = -1;

        /// <summary>
        /// Is the selection backward, in reading order.
        /// <para>If the selection is backward, the anchor word comes after the focus word in reading order.</para>
        /// </summary>
        public bool IsBackward { get; private set; }

        /// <summary>
        /// Is the selection forward, in reading order.
        /// <para>If the selection is forward, the focus word comes after the anchor word in reading order.</para>
        /// </summary>
        public bool IsForward => !IsBackward;

        private readonly IReadOnlyList<PdfWord>?[] _selectedWords;

#if DEBUG
        public int NumberOfPages;
#endif

        public PdfTextSelection(int numberOfPages)
        {
            _selectedWords = new IReadOnlyList<PdfWord>[numberOfPages];
#if DEBUG
            NumberOfPages = numberOfPages;
#endif
        }

        /// <summary>
        /// Get the index of the last word, taking in account the selection direction.
        /// </summary>
        internal Index GetLastWordIndex()
        {
            return new Index(IsBackward ? 0 : 1, !IsBackward);
        }

        /// <summary>
        /// Get the index of the first word, taking in account the selection direction.
        /// </summary>
        internal Index GetFirstWordIndex()
        {
            return new Index(IsBackward ? 1 : 0, IsBackward);
        }

        /// <summary>
        /// Start the selection and set the anchor of the selection to a specified point.
        /// </summary>
        /// <param name="pageNumber">The page number where the anchor word is.</param>
        /// <param name="word">The word within which the anchor will be moved.</param>
        /// <param name="location">The location of the anchor. Should NOT be <c>null</c> if 'Allow partial select'. <c>null</c> otherwise.</param>
        public void Start(int pageNumber, PdfWord? word, Point? location = null)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber <= NumberOfPages);
#endif

            if (pageNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), $"The page number of the anchor should be greater or equal to 1. Current value is {pageNumber}");
            }

            AnchorPageIndex = pageNumber;
            AnchorWord = word;
            AnchorPoint = location;

            if (location.HasValue)
            {
                if (word is null)
                {
                    throw new NullReferenceException("Cannot have null word when updating anchor with a location.");
                }

                // Allow partial select
                CalculateWordCharIndexAndOffset(word, location.Value, true, out int index, out double offset);
                AnchorOffset = index;
                AnchorOffsetDistance = offset;
            }
            else
            {
                AnchorOffset = -1;
                AnchorOffsetDistance = -1;
            }
        }

        /// <summary>
        /// Moves the focus of the selection to a specified point.
        /// </summary>
        /// <param name="pageNumber">The page number where the focus word is.</param>
        /// <param name="word">The word within which the focus will be moved.</param>
        /// <param name="location">The location of the focus. Should NOT be <c>null</c> if 'Allow partial select'. <c>null</c> otherwise.</param>
        public void Extend(int pageNumber, PdfWord? word, Point? location = null)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber <= NumberOfPages);
#endif

            if (pageNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageNumber), $"The page number of the focus should be greater or equal to 1. Current value is {pageNumber}");
            }

            FocusPageIndex = pageNumber;
            FocusWord = word;

            if (location.HasValue)
            {
                if (word is null)
                {
                    throw new NullReferenceException("Cannot have null word when updating focus with a location.");
                }

                // Allow partial select
                CalculateWordCharIndexAndOffset(word, location.Value, false, out int index, out double offset);
                FocusOffset = index;
                FocusOffsetDistance = offset;
            }
            else
            {
                FocusOffset = -1;
                FocusOffsetDistance = -1;
            }

            UpdateSelectionDirection();
        }

        /// <summary>
        /// Clear selected words for the page, but do not reset selection information.
        /// <para>Starts at 1.</para>
        /// </summary>
        public void ClearSelectedWordsForPage(int pageNumber)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber <= NumberOfPages);
#endif

            _selectedWords[pageNumber - 1] = null;
        }

        /// <summary>
        /// Clear selected words in the document, but do not reset selection information.
        /// </summary>
        public void ClearSelectedWords()
        {
            for (int p = 0; p < _selectedWords.Length; ++p)
            {
                _selectedWords[p] = null;
            }
        }

        /// <summary>
        /// Reset document selection: anchor and focus words, page indexes, offsets, indexes and clears selected words.
        /// </summary>
        public void ResetSelection()
        {
            AnchorOffsetDistance = -1;
            AnchorOffset = -1;
            FocusOffsetDistance = -1;
            FocusOffset = -1;

            AnchorPageIndex = -1;
            FocusPageIndex = -1;
            AnchorPoint = null;
            AnchorWord = null;
            FocusWord = null;
            ClearSelectedWords();
        }

        /// <summary>
        /// Select all the words that are between the current <paramref name="selectionStart"/> word and <paramref name="selectionEnd"/> word in the DOM hierarchy,
        /// after having checked for selection direction.
        /// </summary>
        public void SelectWordsInRange(PdfPageTextLayerControl control)
        {
            PdfWord? anchor = AnchorPageIndex == control.PageNumber ? AnchorWord : control.PdfPageTextLayer![GetFirstWordIndex()];
            SelectWordsInRange(control, IsBackward ? FocusWord : anchor, IsBackward ? anchor : FocusWord);
        }

        /// <summary>
        /// Select all the words that are between <paramref name="selectionStart"/> word and <paramref name="selectionEnd"/> word in the DOM hierarchy.<br/>
        /// </summary>
        /// <param name="control">The text layer control.</param>
        /// <param name="selectionStart">selection start word limit</param>
        /// <param name="selectionEnd">selection end word limit</param>
        private void SelectWordsInRange(PdfPageTextLayerControl control, PdfWord? selectionStart, PdfWord? selectionEnd)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(control.PageNumber!.Value <= NumberOfPages);
#endif

            if (control.PdfPageTextLayer is null || selectionStart is null)
            {
                ClearSelectedWords();
            }
            else
            {
                SetSelectedWordsForPage(control.PageNumber!.Value,
                    control.PdfPageTextLayer.GetWords(selectionStart, selectionEnd!).ToArray());
            }
        }

        /// <summary>
        /// Set the selection for the given page.
        /// </summary>
        /// <param name="pageNumber">The page number, starts at 1.</param>
        /// <param name="words">The words in the selection.</param>
        public void SetSelectedWordsForPage(int pageNumber, IReadOnlyList<PdfWord> words)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber <= NumberOfPages);
#endif

            _selectedWords[pageNumber - 1] = words;
        }

        public bool HasStarted()
        {
            return AnchorWord is not null;
        }

        /// <summary>
        /// Is the selection valid.
        /// </summary>
        /// <returns><c>true</c> if both <see cref="AnchorWord"/> and <see cref="FocusWord"/> are defined. <c>false</c> otherwise.</returns>
        public bool IsValid()
        {
            return HasStarted() && FocusWord is not null;
        }

        /// <summary>
        /// Check if the selection direction is forward or backward.<br/>
        /// Is the selection anchor word is before the focus word.
        /// </summary>
        private void UpdateSelectionDirection()
        {
            if (!IsValid())
            {
                return;
            }

            if (AnchorPageIndex != -1 && FocusPageIndex != -1 &&
                AnchorPageIndex != FocusPageIndex)
            {
                IsBackward = AnchorPageIndex > FocusPageIndex;
            }
            else if (AnchorWord == FocusWord)
            {
                IsBackward = AnchorOffset > FocusOffset;
            }
            else
            {
                IsBackward = AnchorWord.IndexInPage > FocusWord.IndexInPage;
            }
        }

        public bool IsWordSelected(int pageNumber, PdfWord word)
        {
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber <= NumberOfPages);
#endif

            // TODO - handle word sub selection
            if (!IsValid())
            {
                return false;
            }

            if (pageNumber < AnchorPageIndex || pageNumber > FocusPageIndex)
            {
                return false;
            }

            return word.IndexInPage >= AnchorWord.IndexInPage && word.IndexInPage <= FocusWord.IndexInPage;
        }

        public IEnumerable<T> GetPageSelectionAs<T>(int pageNumber, Func<PdfWord, T> processFull, Func<PdfWord, int, int, T> processPartial)
        {
            // TODO - merge words on same line
#if DEBUG
            System.Diagnostics.Debug.Assert(pageNumber <= NumberOfPages);
#endif

            var selectedWords = GetPageSelectedWords(pageNumber);
            if (selectedWords is null)
            {
                // TODO - We need to load the layer
                yield break;
            }

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
                    yield return processPartial(word, wordStartIndex == -1 ? 0 : wordStartIndex, wordEndIndex == -1 ? lastIndex : wordEndIndex);
                }
                yield break;
            }

            // Do first word
            var firstWord = selectedWords[0];
            if (wordStartIndex != -1 && wordStartIndex != 0)
            {
                yield return processPartial(firstWord, wordStartIndex, firstWord.Letters!.Count - 1);
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
            var lastWord = selectedWords[selectedWords.Count - 1];
            if (wordEndIndex != -1 && wordEndIndex != lastWord.Letters!.Count - 1)
            {
                yield return processPartial(lastWord, 0, wordEndIndex);
            }
            else
            {
                yield return processFull(lastWord);
            }
        }

        public IEnumerable<T> GetDocumentSelectionAs<T>(Func<PdfWord, T> fullWord, Func<PdfWord, int, int, T> partialWord)
        {
            int startDocumentPage;
            int endDocumentPage;

            if (IsForward)
            {
                startDocumentPage = AnchorPageIndex;
                endDocumentPage = FocusPageIndex;
            }
            else
            {
                startDocumentPage = FocusPageIndex;
                endDocumentPage = AnchorPageIndex;
            }

            if (startDocumentPage > endDocumentPage)
            {
                throw new ArgumentOutOfRangeException($"The selection's start page ({startDocumentPage}) is after the end page ({endDocumentPage}).");
            }

            for (int p = startDocumentPage; p <= endDocumentPage; ++p)
            {
                foreach (var b in GetPageSelectionAs(p, fullWord, partialWord))
                {
                    yield return b;
                }
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
        /// Calculate the character index and offset by characters for the given word and given offset.<br/>
        /// If the location is below the word line then set the selection to the end.<br/>
        /// If the location is to the right of the word then set the selection to the end.<br/>
        /// If the offset is to the left of the word set the selection to the beginning.<br/>
        /// Otherwise calculate the width of each substring to find the char the location is on.
        /// </summary>
        /// <param name="word">the word to calculate its index and offset</param>
        /// <param name="loc">the location to calculate for</param>
        /// <param name="inclusive">is to include the first character in the calculation</param>
        /// <param name="selectionIndex">return the index of the char under the location</param>
        /// <param name="selectionOffset">return the offset of the char under the location</param>
        private static void CalculateWordCharIndexAndOffset(PdfWord word, Point loc, bool inclusive,
            out int selectionIndex, out double selectionOffset)
        {
            selectionOffset = 0;

            if (word.Letters == null || word.Letters.Count == 0)
            {
                // not a text word - set full selection
                selectionIndex = -1;
                selectionOffset = -1;
            }
            else // TODO - only select letter when cursor is over second half of bbox (careful with bidi text)
            {
                int index = word.FindLetterIndexOver(loc.X, loc.Y);
                if (index == -1)
                {
                    index = word.FindNearestLetterIndex(loc.X, loc.Y);
                }

                if (index > -1)
                {
                    var letter = word.Letters[index];
                    selectionOffset = ProjectPointOnLineM(letter.BoundingBox.BottomLeft,
                        letter.BoundingBox.BottomRight,
                        new PdfPoint(loc.X, loc.Y));
                }

                selectionIndex = index;
            }
        }

        // TODO - Put the below in helper class

        // https://stackoverflow.com/questions/54009832/scala-orthogonal-projection-of-a-point-onto-a-line
        /* Projects point `p` on line going through two points `line1` and `line2`. */
        internal static PdfPoint? ProjectPointOnLine(PdfPoint line1, PdfPoint line2, PdfPoint p, out double s)
        {
            PdfPoint v = p.Subtract(line1);
            PdfPoint d = line2.Subtract(line1);

            double den = d.X * d.X + d.Y * d.Y;

            if (Math.Abs(den) <= double.Epsilon)
            {
                s = 0;
                return null;
            }
            s = v.DotProduct(d) / den;

            return line1.Add(new PdfPoint(d.X * s, d.Y * s));
        }
        internal static double ProjectPointOnLineM(PdfPoint line1, PdfPoint line2, PdfPoint p)
        {
            PdfPoint v = p.Subtract(line1);
            PdfPoint d = line2.Subtract(line1);

            double den = d.X * d.X + d.Y * d.Y;

            if (Math.Abs(den) <= double.Epsilon)
            {
                return 0;
            }

            return v.DotProduct(d) / den;
        }
    }
}
