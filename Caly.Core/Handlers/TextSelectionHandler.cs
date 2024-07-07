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
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using Caly.Core.Controls;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;

namespace Caly.Core.Handlers
{
    // See https://github.com/AvaloniaUI/Avalonia.HtmlRenderer/blob/master/Source/HtmlRenderer/Core/Handlers/SelectionHandler.cs
    //
    // See https://github.com/AvaloniaUI/Avalonia/pull/13107/files#diff-f183b476e3366d748fd935e515bf1c8d8845525dcb130aae00ebd70422cd453e
    // See https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/src/AvaloniaEdit/Editing/SelectionLayer.cs

    public sealed partial class TextSelectionHandler : ITextSelectionHandler
    {
        private static readonly Color _selectionColor = Color.FromArgb(0xa9, 0x33, 0x99, 0xFF);

        /// <summary>
        /// <c>true</c> if we are currently selecting text. <c>false</c> otherwise.
        /// </summary>
        private bool _isSelecting;

        /// <summary>
        /// <c>true</c> if we are selecting text though multiple click (full word selection).
        /// </summary>
        private bool _isMultipleClickSelection;

        public PdfTextSelection Selection { get; }

        public TextSelectionHandler(int numberOfPages)
        {
            Selection = new PdfTextSelection(numberOfPages);
        }

        private void ClearSelection(PdfPageTextLayerControl currentTextLayer)
        {
            int start = Selection.GetStartPageIndex();
            int end = Selection.GetEndPageIndex();

            System.Diagnostics.Debug.Assert(start <= end);

            Selection.ResetSelection();

            if (start == -1 || end == -1)
            {
                return;
            }

            PdfDocumentControl pdfDocumentControl = currentTextLayer.FindAncestorOfType<PdfDocumentControl>() ??
                                                    throw new NullReferenceException($"{typeof(PdfDocumentControl)} not found.");

            for (int pageNumber = start; pageNumber <= end; ++pageNumber)
            {
                if (pdfDocumentControl.GetPdfPageItem(pageNumber)?.DataContext is PdfPageViewModel vm)
                {
                    vm.FlagSelectionChanged();
                }
            }
        }

        private static bool TrySwitchCapture(PdfPageTextLayerControl currentTextLayer, PointerEventArgs e)
        {
            PdfPageItem? endPdfPage = currentTextLayer.FindAncestorOfType<PdfDocumentControl>()?.GetPdfPageItemOver(e);
            if (endPdfPage is null)
            {
                // Cursor is not over any page, do nothing
                return false;
            }

            PdfPageTextLayerControl endTextLayer = endPdfPage.TextLayer ??
                                                   throw new NullReferenceException($"{typeof(PdfPageTextLayerControl)} not found.");

            e.Pointer.Capture(endTextLayer); // Switch capture to new page
            return true;
        }

        private PdfWord? FindNearestWordWhileSelecting(Point loc, PdfTextLayer textLayer)
        {
            if (textLayer.TextBlocks is null || textLayer.TextBlocks.Count == 0)
            {
                return null;
            }

#if DEBUG
            p1 = loc;
#endif

            // Try find closest line as we are already selecting something

            // TODO - To finish, improve performance
            var point = new PdfPoint(loc.X, loc.Y);

            double dist = double.MaxValue;
            double projectionOnLine = 0;
            PdfTextLine? l = null;

            foreach (var block in textLayer.TextBlocks)
            {
                foreach (var line in block.TextLines)
                {
                    PdfPoint? projection = PdfPointExtensions.ProjectPointOnLine(in point, line.BoundingBox.BottomLeft,
                        line.BoundingBox.BottomRight, out double s);

                    if (!projection.HasValue || s < 0) // Cursor is before (to the left) line, we ignore
                    {
                        continue;
                    }

                    double localDist = Distances.Euclidean(point, projection.Value);

                    // Line is closer OR same distance AND current projection is closer
                    if (localDist < dist || localDist.AlmostEquals(dist) && s < projectionOnLine)
                    {
                        dist = localDist;
                        l = line;
                        projectionOnLine = s;
#if DEBUG
                        p2 = new Point(projection.Value.X, projection.Value.Y);
                        focusLine = l;
#endif
                    }
                }
            }

            if (l is null)
            {
#if DEBUG
                p2 = null;
                focusLine = null;
#endif
                return null;
            }

            if (projectionOnLine >= 1)
            {
                // Cursor after line, return last word
                return l.Words[^1];
            }

            // TODO - to improve, we already know where on the line is the point thanks to 'projectionOnLine'
            return l.FindNearestWord(loc.X, loc.Y);
        }

#if DEBUG
        Point? p1;
        Point? p2;
        PdfTextLine? focusLine;
#endif
        public void OnPointerMoved(PointerEventArgs e)
        {
#if DEBUG
            p1 = null;
            p2 = null;
            focusLine = null;
#endif
            // Needs to be on UI thread to access
            if (e.Source is not PdfPageTextLayerControl control || control.PdfTextLayer is null)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(control);
            var loc = pointerPoint.Position;

            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                HandleMouseMoveSelection(control, e, loc);
            }
            else
            {
                HandleMouseMoveOver(control, loc);
            }
        }

        private void HandleMouseMoveSelection(PdfPageTextLayerControl control, PointerEventArgs e, Point loc)
        {
            if (_isMultipleClickSelection || control.DataContext is not PdfPageViewModel cvm)
            {
                return;
            }

            if (!control.Bounds.Contains(loc))
            {
                if (TrySwitchCapture(control, e))
                {
                    // Update all pages
                    return;
                }

                return;
            }

            // Get the line under the mouse or nearest from the top
            PdfTextLine? lineBox = control.PdfTextLayer!.FindLineOver(loc.X, loc.Y);

            PdfWord? word = null;
            if (Selection.HasStarted() && lineBox is null)
            {
                // Try to find the closest line as we are already selecting something
                word = FindNearestWordWhileSelecting(loc, control.PdfTextLayer);
            }

            if (lineBox is null && word is null)
            {
                return;
            }

            if (lineBox is null)
            {
                return;
            }

            if (word is null)
            {
                // get the word under the mouse
                word = lineBox.FindWordOver(loc.X, loc.Y);
            }

            // if no word found under the mouse use the last or the first word in the line
            if (word is null)
            {
                word = lineBox.FindNearestWord(loc.X, loc.Y);
            }

            if (word is null)
            {
                return;
            }

            // if there is matching word
            bool allowPartialSelect = !_isMultipleClickSelection;

            int focusPageIndex = Selection.FocusPageIndex;
            Point? partialSelectLoc = allowPartialSelect ? loc : null;
            if (!Selection.HasStarted())
            {
                Selection.Start(control.PageNumber!.Value, word, partialSelectLoc);
            }

            // Always set the focus word
            Selection.Extend(control.PageNumber!.Value, word, partialSelectLoc);
            Selection.SelectWordsInRange(cvm);

            // Check for change of focus page
            if (focusPageIndex != -1 && focusPageIndex != Selection.FocusPageIndex)
            {
                PdfDocumentControl pdfDocumentControl = control.FindAncestorOfType<PdfDocumentControl>() ??
                                                        throw new ArgumentNullException($"{typeof(PdfDocumentControl)} not found.");

                // Focus page has changed
                int start = Math.Min(focusPageIndex, Selection.FocusPageIndex);
                int end = Math.Max(focusPageIndex, Selection.FocusPageIndex);
                for (int i = start; i <= end; ++i) // TODO - do not always do end page, only if deselecting
                {
                    var textLayerControl = pdfDocumentControl.GetPdfPageItem(i)?.DataContext;

                    if (textLayerControl is not PdfPageViewModel vm)
                    {
                        System.Diagnostics.Debug.WriteLine($"Skipping page {i} as not loaded.");
                        continue;
                    }

                    Selection.SelectWordsInRange(vm);
                }
            }

            control.SetIbeamCursor();

            _isSelecting = Selection.IsValid() &&
                           (Selection.AnchorWord != Selection.FocusWord || // Multiple words selected
                            (Selection.AnchorOffset != -1 && Selection.FocusOffset != -1)); // Selection within same word
        }

        [GeneratedRegex(@"(?i)(http(s)?:\/\/)?(\w{2,25}\.)+\w{3}([a-z0-9\-?=$-_.+!*()]+)(?i)")]
        private static partial Regex UrlMatch();

        /// <summary>
        /// Handle mouse hover over words, links or others
        /// </summary>
        private static void HandleMouseMoveOver(PdfPageTextLayerControl control, Point loc)
        {
            PdfWord? word = control.PdfTextLayer!.FindWordOver(loc.X, loc.Y);
            if (word != null)
            {
                ReadOnlySequence<char> sequence = word.Value;

                Span<char> output = sequence.Length < 512 ? stackalloc char[(int)sequence.Length]
                    : new char[sequence.Length]; // This allocates and could be improved using ArrayPool<T>

                sequence.CopyTo(output);

                if (UrlMatch().IsMatch(output))
                {
                    control.SetHandCursor();
                }
                else
                {
                    control.SetIbeamCursor();
                }
            }
            else
            {
                control.SetDefaultCursor();
            }
        }

        private void HandleMultipleClick(PdfPageTextLayerControl control, PointerPressedEventArgs e, PdfWord word)
        {
            if (control.PdfTextLayer is null || control.DataContext is not PdfPageViewModel vm)
            {
                return;
            }

            if (e.ClickCount < 2)
            {
                //throw new ArgumentException($"Click count should be 2 or more. Got {e.ClickCount} click(s).");
            }

            PdfWord? startWord;
            PdfWord? endWord;

            if (e.ClickCount == 2)
            {
                // Select whole word
                startWord = word;
                endWord = word;
            }
            else if (e.ClickCount == 3)
            {
                // Select whole line
                var block = control.PdfTextLayer.TextBlocks![word.TextBlockIndex];
                var line = block.TextLines![word.TextLineIndex - block.TextLines[0].IndexInPage];

                startWord = line.Words![0];
                endWord = line.Words[^1];
            }
            else if (e.ClickCount == 4)
            {
                // Select whole paragraph
                var block = control.PdfTextLayer.TextBlocks![word.TextBlockIndex];

                startWord = block.TextLines![0].Words![0];
                endWord = block.TextLines![^1].Words![^1];
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"HandleMultipleClick: Not handled, got {e.ClickCount} click(s).");
                return;
            }

            ClearSelection(control);

            int pageNumber = control.PageNumber!.Value;
            Selection.Start(pageNumber, startWord);
            Selection.Extend(pageNumber, endWord);
            Selection.SelectWordsInRange(vm);

            System.Diagnostics.Debug.WriteLine($"HandleMultipleClick: {startWord} -> {endWord}.");
        }

        public void OnPointerPressed(PointerPressedEventArgs e)
        {
            Debug.ThrowNotOnUiThread();

            if (e.Source is not PdfPageTextLayerControl control || control.PdfTextLayer is null)
            {
                return;
            }

            bool clearSelection = false;

            _isMultipleClickSelection = e.ClickCount > 1;

            var pointerPoint = e.GetCurrentPoint(control);
            var point = pointerPoint.Position;

            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                PdfWord? word = control.PdfTextLayer.FindWordOver(point.X, point.Y);

                if (word != null && Selection.IsWordSelected(control.PageNumber!.Value, word))
                {
                    clearSelection = e.ClickCount == 1; // Clear selection if single click
                    HandleMultipleClick(control, e, word); // TODO - we pass 1 click here too
                }
                else if (word != null && e.ClickCount == 2)
                {
                    // TODO - do better multiple click selection
                    HandleMultipleClick(control, e, word);
                }
                else
                {
                    clearSelection = true;
                }
            }
            else if (pointerPoint.Properties.IsRightButtonPressed)
            {
                // TODO
            }

            if (clearSelection)
            {
                ClearSelection(control);
            }
        }

        public void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.Source is not PdfPageTextLayerControl control || control.PdfTextLayer is null)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(control);

            bool ignore = _isSelecting || _isMultipleClickSelection;
            if (!ignore && pointerPoint.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                ClearSelection(control);

                // Check link
                if (!_isSelecting)
                {
                    var point = pointerPoint.Position;
                    PdfWord? word = control.PdfTextLayer.FindWordOver(point.X, point.Y);

                    if (word is not null)
                    {
                        ReadOnlySequence<char> sequence = word.Value;
                        Span<char> output = sequence.Length < 512
                            ? stackalloc char[(int)sequence.Length]
                            : new char[sequence.Length]; // This allocates and could be improved using ArrayPool<T>

                        sequence.CopyTo(output);

                        foreach (ValueMatch match in UrlMatch().EnumerateMatches(output))
                        {
                            if (match.Length == 0)
                            {
                                continue;
                            }

                            CalyExtensions.OpenBrowser(output.Slice(match.Index, match.Length));
                            break; // Only opens first url matched
                        }
                    }
                }
            }

            _isSelecting = false;
        }

#if DEBUG
        private static void DrawArrow(DrawingContext context, IPen pen, Point lineStart, Point lineEnd)
        {
            context.DrawLine(pen, lineStart, lineEnd);
            context.DrawEllipse(null, pen, lineEnd, 1, 1);
        }
#endif

        public void RenderPage(PdfPageTextLayerControl control, DrawingContext context)
        {
            if (control.PdfTextLayer?.TextBlocks is null)
            {
                return;
            }

#if DEBUG
            var redBrush = new SolidColorBrush(Colors.Red, 0.4);
            var redPen = new Pen(redBrush, 0.5);
            var blueBrush = new SolidColorBrush(Colors.Blue, 0.4);
            var bluePen = new Pen(blueBrush, 0.5);
            var greenBrush = new SolidColorBrush(Colors.Green, 0.4);
            var greenPen = new Pen(greenBrush, 0.5);

            PdfWord? previousWord = null;

            foreach (var block in control.PdfTextLayer.TextBlocks)
            {
                context.DrawGeometry(greenBrush, greenPen, PdfWordHelpers.GetGeometry(block.BoundingBox, true));

                foreach (var line in block.TextLines)
                {
                    foreach (var word in line.Words)
                    {
                        context.DrawGeometry(redBrush, redPen, PdfWordHelpers.GetGeometry(word.BoundingBox));
                        context.DrawEllipse(Brushes.Blue, null, new Point(word.BoundingBox.BottomLeft.X, word.BoundingBox.BottomLeft.Y), 0.5, 0.5);

                        if (previousWord is not null)
                        {
                            var start = new Point(previousWord.BoundingBox.Centroid.X, previousWord.BoundingBox.Centroid.Y);
                            var end = new Point(word.BoundingBox.Centroid.X, word.BoundingBox.Centroid.Y);
                            DrawArrow(context, bluePen, start, end);
                        }

                        previousWord = word;
                    }
                }
            }

            if (p1.HasValue && p2.HasValue)
            {
                context.DrawLine(redPen, p1.Value, p2.Value);
                context.DrawEllipse(null, redPen, p2.Value, 1, 1);
            }

            if (focusLine is not null)
            {
                context.DrawGeometry(redBrush, redPen, PdfWordHelpers.GetGeometry(focusLine.BoundingBox, true));
            }
#endif

            var selectionBrush = new ImmutableSolidColorBrush(_selectionColor);

            foreach (var g in Selection.GetPageSelectionAs(control.PageNumber!.Value, PdfWordHelpers.GetGeometry, PdfWordHelpers.GetGeometry))
            {
                context.DrawGeometry(selectionBrush, null, g);
            }
        }
    }
}
