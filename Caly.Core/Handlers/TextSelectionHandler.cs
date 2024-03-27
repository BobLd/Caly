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
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Caly.Core.Controls;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.Geometry;

namespace Caly.Core.Handlers
{
    // See https://github.com/AvaloniaUI/Avalonia.HtmlRenderer/blob/master/Source/HtmlRenderer/Core/Handlers/SelectionHandler.cs
    //
    // See https://github.com/AvaloniaUI/Avalonia/pull/13107/files#diff-f183b476e3366d748fd935e515bf1c8d8845525dcb130aae00ebd70422cd453e
    // See https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/src/AvaloniaEdit/Editing/SelectionLayer.cs

    public sealed class TextSelectionHandler : ITextSelectionHandler
    {
        private static readonly Brush _selectionBrush = new SolidColorBrush(Color.FromArgb(0xa9, 0x33, 0x99, 0xFF));

        private readonly PdfTextSelection _selection;

        /// <summary>
        /// <c>true</c> if we are currently selecting text. <c>false</c> otherwise.
        /// </summary>
        private bool _isSelecting;

        /// <summary>
        /// <c>true</c> if we are selecting text though multiple click (full word selection).
        /// </summary>
        private bool _isMultipleClickSelection;

        public TextSelectionHandler(PdfTextSelection selection)
        {
            _selection = selection;
        }

        public void SelectTextToEndInPage(PdfPageTextLayerControl control)
        {
            if (control.PdfPageTextLayer is null || !control.PageNumber.HasValue)
            {
                return;
            }

            _selection.Extend(control.PageNumber.Value, control.PdfPageTextLayer[_selection.GetLastWordIndex()]);
            _selection.SelectWordsInRange(control);
        }

        private void ClearSelection(PdfPageTextLayerControl currentTextLayer)
        {
            bool isBackward = _selection.IsBackward;
            int anchorPage = _selection.AnchorPageIndex;
            int focusPage = _selection.FocusPageIndex;
            _selection.ResetSelection();

            if (anchorPage == -1 || focusPage == -1)
            {
                return;
            }

            var pdfDocumentControl = currentTextLayer.FindAncestorOfType<PdfDocumentControl>();

            if (isBackward)
            {
                for (int p = anchorPage; p >= focusPage; --p)
                {
                    InvalidatePage(p);
                }
            }
            else
            {
                for (int p = anchorPage; p <= focusPage; ++p)
                {
                    InvalidatePage(p);
                }
            }

            void InvalidatePage(int pageNumber)
            {
                if (pageNumber == currentTextLayer.PageNumber)
                {
                    return;
                }

                var page = pdfDocumentControl?.GetPdfPageControl(pageNumber);
                var layer = page?.GetVisualDescendants()
                    .OfType<PdfPageTextLayerControl>()
                    .FirstOrDefault();

                layer?.InvalidateVisual();
            }
        }

        private bool TrySwitchCapture(PdfPageTextLayerControl currentTextLayer, PointerEventArgs e)
        {
            // TODO - Unselect when skipping pages from outside

            var pdfDocumentControl = currentTextLayer.FindAncestorOfType<PdfDocumentControl>();
            var pdfPage = pdfDocumentControl?.GetPdfPageControlOver(e);

            if (pdfPage is null)
            {
                // Cursor is not over any page, do nothing
                return false;
            }

            var endTextLayer = pdfPage.GetVisualDescendants()
                .OfType<PdfPageTextLayerControl>()
                .FirstOrDefault();

            if (endTextLayer is null)
            {
                throw new NullReferenceException($"{typeof(PdfPageTextLayerControl)} not found.");
            }

            if (!endTextLayer.PageNumber.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("WARNING the page number of the end text layer is not set.");
            }

            if (endTextLayer.PdfPageTextLayer is null)
            {
                // TODO - properly handle that. TO CHECK - only required when selection shortens
                System.Diagnostics.Debug.WriteLine("WARNING the PdfPageTextLayer of the end text layer is not set.");
            }

            // Update current page selection
            bool endAfterCurrent = endTextLayer.PageNumber > currentTextLayer.PageNumber;
            bool isExtendsSelection = _selection.IsBackward ? !endAfterCurrent : endAfterCurrent;

            if (isExtendsSelection)
            {
                // Extends selection: Set text selection to start of page
                if (currentTextLayer.PdfPageTextLayer.Count > 0)
                {
                    // Only if any word in the page
                    _selection.Extend(currentTextLayer.PageNumber.Value, currentTextLayer.PdfPageTextLayer[_selection.GetLastWordIndex()]);
                    _selection.SelectWordsInRange(currentTextLayer);
                }
            }
            else
            {
                // Shortens selection: Set text selection to none
                // TODO - see above. Need to make sure endTextLayer.PdfPageTextLayer is loaded and not null
                if (endTextLayer.PdfPageTextLayer.Count > 0)
                {
                    _selection.Extend(endTextLayer.PageNumber.Value, endTextLayer.PdfPageTextLayer[_selection.GetLastWordIndex()]);
                }
                _selection.ClearSelectedWordsForPage(currentTextLayer.PageNumber.Value);
            }

            currentTextLayer.InvalidateVisual();

            // Update every page between the current page, and the page (excluded) that will receive capture
            bool hasPagesBetween = Math.Abs(currentTextLayer.PageNumber.Value - endTextLayer.PageNumber.Value) > 1;
            if (hasPagesBetween)
            {
                void UpdatePage(int pageNumber)
                {
                    var page = pdfDocumentControl?.GetPdfPageControl(pageNumber);
                    var layer = page?.GetVisualDescendants()
                        .OfType<PdfPageTextLayerControl>()
                        .FirstOrDefault();

                    if (layer is null)
                    {
                        return;
                    }

                    layer.SelectTextToEnd();
                    layer.InvalidateVisual();
                }

                if (_selection.IsBackward)
                {
                    for (int p = currentTextLayer.PageNumber.Value - 1; p > endTextLayer.PageNumber.Value; p--)
                    {
                        UpdatePage(p);
                    }
                }
                else
                {
                    for (int p = currentTextLayer.PageNumber.Value + 1; p < endTextLayer.PageNumber.Value; p++)
                    {
                        UpdatePage(p);
                    }
                }
            }

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
                    PdfPoint? projection = PdfTextSelection.ProjectPointOnLine(line.BoundingBox.BottomLeft,
                        line.BoundingBox.BottomRight, point, out double s);

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
            if (e.Source is not PdfPageTextLayerControl control || control.PdfPageTextLayer is null)
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
            if (_isMultipleClickSelection)
            {
                return;
            }

            if (!control.Bounds.Contains(loc))
            {
                if (TrySwitchCapture(control, e))
                {
                    return;
                }

                return;
            }

            // Get the line under the mouse or nearest from the top
            PdfTextLine? lineBox = control.PdfPageTextLayer!.FindLineOver(loc.X, loc.Y);

            PdfWord? word = null;
            if (_selection.HasStarted() && lineBox is null)
            {
                // Try to find the closest line as we are already selecting something
                word = FindNearestWordWhileSelecting(loc, control.PdfPageTextLayer);
            }

            if (lineBox == null && word is null) return;

            if (word == null)
            {
                // get the word under the mouse
                word = lineBox.FindWordOver(loc.X, loc.Y);
            }

            // if no word found under the mouse use the last or the first word in the line
            if (word == null)
            {
                word = lineBox.FindNearestWord(loc.X, loc.Y);
            }

            if (word == null)
            {
                return;
            }

            // if there is matching word
            bool allowPartialSelect = !_isMultipleClickSelection;

            Point? partialSelectLoc = allowPartialSelect ? loc : null;
            if (!_selection.HasStarted())
            {
                _selection.Start(control.PageNumber!.Value, word, partialSelectLoc);
            }

            // Always set the focus word
            _selection.Extend(control.PageNumber!.Value, word, partialSelectLoc);
            _selection.SelectWordsInRange(control);

            control.SetIbeamCursor();
            control.InvalidateVisual();

            _isSelecting = _selection.IsValid() &&
                           (_selection.AnchorWord != _selection.FocusWord || // Multiple words selected
                            (_selection.AnchorOffset != -1 && _selection.FocusOffset != -1)); // Selection within same word
        }

        /// <summary>
        /// Handle mouse hover over words, links or others
        /// </summary>
        private static void HandleMouseMoveOver(PdfPageTextLayerControl control, Point loc)
        {
            bool isLink = false; // TODO - Check if link
            if (isLink)
            {
                control.SetHandCursor();
            }
            else
            {
                PdfWord? word = control.PdfPageTextLayer!.FindWordOver(loc.X, loc.Y);
                if (word != null)
                {
                    control.SetIbeamCursor();
                }
                else
                {
                    control.SetDefaultCursor();
                }
            }
        }

        private void HandleMultipleClick(PdfPageTextLayerControl control, PointerPressedEventArgs e, PdfWord word)
        {
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
                var block = control.PdfPageTextLayer!.TextBlocks![word.TextBlockIndex];
                var line = block.TextLines![word.TextLineIndex - block.TextLines[0].IndexInPage];

                startWord = line.Words![0];
                endWord = line.Words[^1];
            }
            else if (e.ClickCount == 4)
            {
                // Select whole paragraph
                var block = control.PdfPageTextLayer!.TextBlocks![word.TextBlockIndex];

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
            _selection.Start(pageNumber, startWord);
            _selection.Extend(pageNumber, endWord);
            _selection.SelectWordsInRange(control);

            control.InvalidateVisual();

            System.Diagnostics.Debug.WriteLine($"HandleMultipleClick: {startWord} -> {endWord}.");
        }

        public void OnPointerPressed(PointerPressedEventArgs e)
        {
            Debug.ThrowNotOnUiThread();

            if (e.Source is not PdfPageTextLayerControl control || control.PdfPageTextLayer is null)
            {
                return;
            }

            bool clearSelection = false;

            _isMultipleClickSelection = e.ClickCount > 1;

            var pointerPoint = e.GetCurrentPoint(control);
            var point = pointerPoint.Position;

            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                PdfWord? word = control.PdfPageTextLayer.FindWordOver(point.X, point.Y);

                if (word != null && _selection.IsWordSelected(control.PageNumber!.Value, word))
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
                control.InvalidateVisual();
            }
        }

        public void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.Source is not PdfPageTextLayerControl control || control.PdfPageTextLayer is null)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(control);

            bool ignore = _isSelecting || _isMultipleClickSelection;
            if (!ignore && pointerPoint.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                ClearSelection(control);
                control.InvalidateVisual();

                // TODO - Get link under cursor, and execute link if need be
            }

            _isSelecting = false;
        }

        private static StreamGeometry GetGeometry(PdfRectangle rect, bool isFilled = false)
        {
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(new Point(rect.BottomLeft.X, rect.BottomLeft.Y), isFilled);
                ctx.LineTo(new Point(rect.TopLeft.X, rect.TopLeft.Y));
                ctx.LineTo(new Point(rect.TopRight.X, rect.TopRight.Y));
                ctx.LineTo(new Point(rect.BottomRight.X, rect.BottomRight.Y));
                ctx.EndFigure(true);
            }

            return sg;
        }

        private static StreamGeometry GetGeometry(PdfWord word)
        {
            return GetGeometry(word.BoundingBox, true);
        }

        private static StreamGeometry? GetGeometry(PdfWord word, int startIndex, int endIndex)
        {
            System.Diagnostics.Debug.Assert(startIndex > -1);
            System.Diagnostics.Debug.Assert(endIndex > -1);
            System.Diagnostics.Debug.Assert(startIndex <= endIndex);

            var rects = new List<PdfRectangle>();
            for (int l = startIndex; l <= endIndex; ++l)
            {
                rects.Add(word.Letters![l].BoundingBox);
            }

            var bbox = GeometryExtensions.MinimumAreaRectangle(rects
                .SelectMany(r => new[]
                {
                    r.BottomLeft,
                    r.BottomRight,
                    r.TopLeft,
                    r.TopRight
                }));

            return GetGeometry(bbox, true);
        }

        private static void DrawArrow(DrawingContext context, IPen pen, Point lineStart, Point lineEnd)
        {
            context.DrawLine(pen, lineStart, lineEnd);
            context.DrawEllipse(null, pen, lineEnd, 1, 1);
        }

        public void RenderPage(PdfPageTextLayerControl control, DrawingContext context)
        {
            if (control.PdfPageTextLayer is null)
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

            foreach (var block in control.PdfPageTextLayer.TextBlocks)
            {
                context.DrawGeometry(greenBrush, greenPen, GetGeometry(block.BoundingBox, true));

                foreach (var line in block.TextLines)
                {
                    foreach (var word in line.Words)
                    {
                        context.DrawGeometry(redBrush, redPen, GetGeometry(word.BoundingBox));
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
                context.DrawGeometry(redBrush, redPen, GetGeometry(focusLine.BoundingBox, true));
            }
#endif

            var selectionBrush = _selectionBrush.ToImmutable();

            foreach (var g in _selection.GetPageSelectionAs(control.PageNumber!.Value, GetGeometry, GetGeometry))
            {
                context.DrawGeometry(selectionBrush, null, g);
            }
        }
    }
}
