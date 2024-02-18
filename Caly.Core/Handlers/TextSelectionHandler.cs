using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    // See https://github.com/AvaloniaUI/Avalonia/pull/13107/files#diff-f183b476e3366d748fd935e515bf1c8d8845525dcb130aae00ebd70422cd453e
    // See https://github.com/AvaloniaUI/AvaloniaEdit/blob/master/src/AvaloniaEdit/Editing/SelectionLayer.cs

    internal sealed class TextSelectionHandler : ITextSelectionHandler
    {
        private readonly Brush _selectionBrush = new SolidColorBrush(Color.FromArgb(0xa9, 0x33, 0x99, 0xFF));

        private readonly PdfTextSelection _selection;

        /// <summary>
        /// used to ignore mouse up after selection
        /// </summary>
        public bool _inSelection;

        /// <summary>
        /// current selection process is after double click (full word selection)
        /// <para>previously _multipleClick</para>
        /// </summary>
        public bool _multipleClick;

        /// <summary>
        /// used to handle drag & drop
        /// </summary>
        public bool _mouseDownOnSelectedWord;

        /// <summary>
        /// is the cursor on the control has been changed by the selection handler
        /// </summary>
        public bool _cursorChanged;

        /// <summary>
        /// used to know if double click selection is requested
        /// </summary>
        public DateTime _lastMouseDown;

        public TextSelectionHandler(PdfTextSelection selection)
        {
            _selection = selection;
        }

        public void SelectAllTextInPage(PdfPageTextLayerControl control)
        {
            if (control.PdfPageTextLayer is null)
            {
                return;
            }

            // TODO - why do we need to pass _selection.SelectionEndPageIndex to make it work
            // when the capture changes and we are in backward selection?
            _selection.Extend(_selection.FocusPageIndex, control.PdfPageTextLayer[^1]);
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

            void InvalidatePage(int pageNumber)
            {
                if (pageNumber == currentTextLayer.PageNumber)
                {
                    return;
                }

                var page = pdfDocumentControl?.TryGetPdfPageControl(pageNumber);
                var layer = page?.GetVisualDescendants()
                    .OfType<PdfPageTextLayerControl>()
                    .FirstOrDefault();

                //if (layer is null)
                //{
                //throw new NullReferenceException($"{typeof(PdfPageTextLayerControl)} not found.");
                //}

                layer?.InvalidateVisual();
            }

            if (isBackward)
            {
                for (int p = anchorPage; p >= focusPage; p--)
                {
                    InvalidatePage(p);
                }
            }
            else
            {
                for (int p = anchorPage; p <= focusPage; p++)
                {
                    InvalidatePage(p);
                }
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

            // We stop capturing event here, we are outside
            //var test2 = pdfPage.Find<PdfPageTextLayerControl>("PART_PdfPageTextLayerControl");

            var endTextLayer = pdfPage.GetVisualDescendants()
                .OfType<PdfPageTextLayerControl>()
                .FirstOrDefault();

            if (endTextLayer is null)
            {
                throw new NullReferenceException($"{typeof(PdfPageTextLayerControl)} not found.");
            }

            // Update current page selection
            var endIndex = new Index(_selection.IsBackward ? 0 : 1, !_selection.IsBackward);

            if (!endTextLayer.PageNumber.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("WARNING the page number of the end text layer is not set.");
            }

            if (endTextLayer.PdfPageTextLayer is null)
            {
                // TODO - properly handle that. TO CHECK - only required when selection shortens
                System.Diagnostics.Debug.WriteLine("WARNING the PdfPageTextLayer of the end text layer is not set.");
            }

            bool isExtendsSelection = _selection.IsBackward
                ? currentTextLayer.PageNumber > endTextLayer.PageNumber
                : endTextLayer.PageNumber > currentTextLayer.PageNumber;

            if (isExtendsSelection)
            {
                // Extends selection: Set text selection to start of page
                if (currentTextLayer.PdfPageTextLayer.Count > 0)
                {
                    // Only if any word in the page
                    _selection.Extend(currentTextLayer.PageNumber.Value, currentTextLayer.PdfPageTextLayer[endIndex]);
                    _selection.SelectWordsInRange(currentTextLayer);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING Extends selection but no words in page {currentTextLayer.PageNumber}.");
                }
            }
            else
            {
                // Shortens selection: Set text selection to none
                // TODO - see above. Need to make sure endTextLayer.PdfPageTextLayer is loaded and not null
                if (endTextLayer.PdfPageTextLayer.Count > 0)
                {
                    _selection.Extend(endTextLayer.PageNumber.Value, endTextLayer.PdfPageTextLayer[endIndex]);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"WARNING Shortens selection but no words in page {currentTextLayer.PageNumber}.");
                }

                _selection.ClearSelectedWordsForPage(currentTextLayer.PageNumber.Value);
            }

            currentTextLayer.InvalidateVisual();

            // Update every page between the current page, and the page (excluded) that will receive capture
            if (Math.Abs(currentTextLayer.PageNumber.Value - endTextLayer.PageNumber.Value) > 1)
            {
                void UpdatePage(int pageNumber)
                {
                    var page = pdfDocumentControl?.TryGetPdfPageControl(pageNumber);
                    var layer = page?.GetVisualDescendants()
                        .OfType<PdfPageTextLayerControl>()
                        .FirstOrDefault();

                    // TODO - throw if null?

                    layer?.SelectAllText();
                    layer?.InvalidateVisual();
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

            // Switch capture to new page
            e.Pointer.Capture(endTextLayer);
            return true;
        }

        private PdfWord? FindNearestWordWhileSelecting(Point loc, PdfTextLayer textLayer)
        {
            if (textLayer.TextBlocks is null || textLayer.TextBlocks.Count == 0)
            {
                return null;
            }
            // TODO - to finish

#if DEBUG
            p1 = loc;
#endif

            // Try find closest line as we are already selecting something

            // TODO - Improve performance
            var point = new PdfPoint(loc.X, loc.Y);

            double dist = double.MaxValue;
            double projectionOnLine = 0;
            PdfTextLine? l = null;

            foreach (var block in textLayer.TextBlocks)
            {
                foreach (var line in block.TextLines)
                {
                    var projection = PdfTextSelection.ProjectPointOnLine(line.BoundingBox.BottomLeft,
                        line.BoundingBox.BottomRight, point, out double s);

                    if (s < 0) // Cursor is before (to the left) line, we ignore
                    {
                        continue;
                    }

                    double localDist = Distances.Euclidean(point, projection);

                    // Line is closer OR same distance AND current projection is closer
                    if (localDist < dist || localDist.AlmostEquals(dist) && s < projectionOnLine)
                    {
                        dist = localDist;
                        l = line;
                        projectionOnLine = s;
#if DEBUG
                        p2 = new Point(projection.X, projection.Y);
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
                if (!control.Bounds.Contains(loc))
                {
                    if (TrySwitchCapture(control, e))
                    {
                        return;
                    }

                    return;
                }

                // get the line under the mouse or nearest from the top
                // TODO - careful here, pdf page and control have same size so far, might not always be the case
                PdfTextLine? lineBox = control.PdfPageTextLayer.FindLineOver(loc.X, loc.Y);

                PdfWord? word = null;
                if (_selection.HasSelectionStarted())
                {
                    if (lineBox is null)
                    {
                        // Try find closest line as we are already selecting something
                        word = FindNearestWordWhileSelecting(loc, control.PdfPageTextLayer);
                    }
                }

                if (lineBox == null && word is null) return;

                if (_mouseDownOnSelectedWord)
                {
                    // TODO - StartDragDrop
                    return;
                }

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

                // if there is matching word
                if (word != null)
                {
                    bool allowPartialSelect = !_multipleClick;

                    if (!_selection.HasSelectionStarted())
                    {
                        _selection.Start(control.PageNumber!.Value, word,
                            allowPartialSelect ? loc : null);
                    }

                    // Always set the focus word
                    _selection.Extend(control.PageNumber!.Value, word, allowPartialSelect ? loc : null);
                    _selection.SelectWordsInRange(control);

                    /*
                    if (_selection.IsNonEmptySelection(loc, allowPartialSelect))
                    {
                        _selection.SelectWordsInRange(control);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("WARNING Selection is empty");
                    }
                    */

                    _cursorChanged = true;
                    control.SetIbeamCursor();
                    control.InvalidateVisual();

                    _inSelection =
                        _selection.AnchorWord != null && _selection.FocusWord != null &&
                        (_selection.AnchorWord != _selection.FocusWord ||
                         _selection.AnchorOffset != _selection.FocusOffset);
                }
            }
            else
            {
                // Handle mouse hover over the html to change the cursor depending if hovering word, link of other.
                bool isLink = false; // TODO - Check if link
                if (isLink)
                {
                    _cursorChanged = true;
                    control.SetHandCursor();
                }
                else
                {
                    PdfWord? word = control.PdfPageTextLayer.FindWordOver(loc.X, loc.Y);
                    _cursorChanged = word != null;
                    if (_cursorChanged)
                    {
                        control.SetIbeamCursor();
                    }
                    else
                    {
                        control.SetDefaultCursor();
                    }
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

            control.InvalidateVisual(); // TODO - not here?
        }

        public void OnPointerPressed(PointerPressedEventArgs e)
        {
            // Needs to be on UI thread to access
            if (e.Source is not PdfPageTextLayerControl control || control.PdfPageTextLayer is null)
            {
                return;
            }

            // TODO - Shift click to update selection

            bool clear = false; // !isMouseInContainer;

            _mouseDownOnSelectedWord = false;
            _multipleClick = e.ClickCount > 1; //(DateTime.Now - _lastMouseDown).TotalMilliseconds < 400;
            _lastMouseDown = DateTime.Now;

            var pointerPoint = e.GetCurrentPoint(control);
            var point = pointerPoint.Position;

            if (pointerPoint.Properties.IsLeftButtonPressed)
            {
                // TODO - careful here, pdf page and control have same size so far, might not always be the case
                PdfWord? word = control.PdfPageTextLayer.FindWordOver(point.X, point.Y);

                // TODO - check again conditions, was something like word != null && IsWordSelected(word) + new ones

                if (word != null && _selection.IsWordSelected(control.PageNumber!.Value, word))
                {
                    _mouseDownOnSelectedWord = true;
                    HandleMultipleClick(control, e, word); // TODO - we pass 1 click here too
                }
                else if (word != null && e.ClickCount == 2)
                {
                    // TODO - do better multiple click selection
                    HandleMultipleClick(control, e, word);
                }
                else
                {
                    clear = true;
                }
            }
            else if (pointerPoint.Properties.IsRightButtonPressed)
            {
                // TODO
            }

            if (clear) // Clear selection
            {
                ClearSelection(control);
                control.InvalidateVisual(); // TODO - not here?
            }
        }

        public void OnPointerReleased(PointerReleasedEventArgs e)
        {
            if (e.Source is not PdfPageTextLayerControl control || control.PdfPageTextLayer is null)
            {
                return;
            }

            var pointerPoint = e.GetCurrentPoint(control);
            var point = pointerPoint.Position;

            bool ignore = _inSelection || DateTime.Now - _lastMouseDown > TimeSpan.FromSeconds(1);
            if (!ignore && pointerPoint.Properties.IsLeftButtonPressed && _mouseDownOnSelectedWord)
            {
                ClearSelection(control);
                control.InvalidateVisual();
            }

            _mouseDownOnSelectedWord = false;
            _inSelection = false;

            if (!ignore && pointerPoint.Properties.IsLeftButtonPressed)
            {
                // TODO - Get link under cursor, and execute link if need be
            }
        }

        private static IEnumerable<Point> GetPoints(PdfRectangle rect)
        {
            yield return new Point(rect.BottomLeft.X, rect.BottomLeft.Y);
            yield return new Point(rect.TopLeft.X, rect.TopLeft.Y);
            yield return new Point(rect.TopRight.X, rect.TopRight.Y);
            yield return new Point(rect.BottomRight.X, rect.BottomRight.Y);
        }

        private static StreamGeometry GetGeometry(PdfRectangle rect, bool isFilled = false)
        {
            // might want to try with normal geometry, size of exe is quite big now
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                var points = GetPoints(rect).ToArray();
                ctx.BeginFigure(points[0], isFilled);
                foreach (var point in points.Skip(1))
                {
                    ctx.LineTo(point);
                }

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
                //context.DrawEllipse(Brushes.Blue, null, new Point(block.BoundingBox.BottomLeft.X, block.BoundingBox.BottomLeft.Y), 1, 1);

                foreach (var line in block.TextLines)
                {
                    //context.DrawGeometry(brush, pen, GetGeometry(line.BoundingBox));
                    //context.DrawEllipse(Brushes.Blue, null, new Point(line.BoundingBox.BottomLeft.X, line.BoundingBox.BottomLeft.Y), 0.5, 0.5);

                    foreach (var word in line.Words)
                    {
                        context.DrawGeometry(redBrush, redPen, GetGeometry(word.BoundingBox));
                        context.DrawEllipse(Brushes.Blue, null, new Point(word.BoundingBox.BottomLeft.X, word.BoundingBox.BottomLeft.Y), 0.5, 0.5);
                        foreach (var letter in word.Letters)
                        {
                            //context.DrawGeometry(brush, pen, GetGeometry(letter.BoundingBox));
                            //context.DrawEllipse(Brushes.Blue, null, new Point(letter.StartBaseLine.X, letter.StartBaseLine.Y), 1, 1);
                        }

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

            foreach (var g in _selection.GetSelectionForPageAs(control.PageNumber!.Value, GetGeometry, GetGeometry))
            {
                context.DrawGeometry(selectionBrush, null, g);
            }
        }
    }
}
