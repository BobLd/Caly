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
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caly.Core.Controls;
using Caly.Core.Handlers.Interfaces;
using Caly.Core.Models;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using Caly.Pdf;
using Caly.Pdf.Models;
using UglyToad.PdfPig.Actions;
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

        private static readonly Color _searchColor = Color.FromArgb(0xa9, 255, 0, 0);

        /// <summary>
        /// <c>true</c> if we are currently selecting text. <c>false</c> otherwise.
        /// </summary>
        private bool _isSelecting;

        /// <summary>
        /// <c>true</c> if we are selecting text though multiple click (full word selection).
        /// </summary>
        private bool _isMultipleClickSelection;

        private Point? _startPointerPressed;

        public PdfTextSelection Selection { get; }

        public TextSelectionHandler(int numberOfPages)
        {
            Selection = new PdfTextSelection(numberOfPages);
        }

        public void ClearSelection(PdfDocumentControl pdfDocumentControl)
        {
            Debug.ThrowNotOnUiThread();
            int start = Selection.GetStartPageIndex();
            int end = Selection.GetEndPageIndex();

            System.Diagnostics.Debug.Assert(start <= end);

            Selection.ResetSelection();

            if (start == -1 || end == -1 ||
                pdfDocumentControl.DataContext is not PdfDocumentViewModel docVm)
            {
                return;
            }

            for (int pageNumber = start; pageNumber <= end; ++pageNumber)
            {
                docVm.Pages[pageNumber - 1].FlagSelectionChanged();
            }
        }

        public void ClearSelection(PdfPageTextLayerControl currentTextLayer)
        {
            Debug.ThrowNotOnUiThread();

            PdfDocumentControl pdfDocumentControl = currentTextLayer.FindAncestorOfType<PdfDocumentControl>() ??
                                                    throw new NullReferenceException($"{typeof(PdfDocumentControl)} not found.");
            ClearSelection(pdfDocumentControl);
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

            if (e is PointerWheelEventArgs we)
            {
                // TODO - Looks like there's a bug in Avalonia (TBC) where the position of the pointer
                // is 1 step behind the actual position.
                // We need to add back this step (1 scroll step is 50, see link below)
                // https://github.com/AvaloniaUI/Avalonia/blob/dadc9ab69284bb228ad460f36d5442b4eee4a82a/src/Avalonia.Controls/Presenters/ScrollContentPresenter.cs#L684

                // TODO - The hack does not work when zoomed

                double x = Math.Max(loc.X - we.Delta.X * 50.0, 0);
                double y = Math.Max(loc.Y - we.Delta.Y * 50.0, 0);

                loc = new Point(x, y);

                // TODO - We have an issue when scrolling and changing page here, similar the TrySwitchCapture
                // not sure how we should address it
            }

            if (pointerPoint.Properties.IsLeftButtonPressed && _startPointerPressed.HasValue && _startPointerPressed.Value.Euclidean(loc) > 1.0)
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
            if (Selection.HasStarted && lineBox is null)
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
            if (!Selection.HasStarted)
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

                if (pdfDocumentControl.DataContext is not PdfDocumentViewModel docVm)
                {
                    throw new ArgumentNullException($"DataContext {typeof(PdfDocumentViewModel)} not set.");
                }

                // Focus page has changed
                int start = Math.Min(focusPageIndex, Selection.FocusPageIndex);
                int end = Math.Max(focusPageIndex, Selection.FocusPageIndex);
                for (int i = start; i <= end; ++i) // TODO - do not always do end page, only if deselecting
                {
                    Selection.SelectWordsInRange(docVm.Pages[i - 1]);
                }
            }

            control.SetIbeamCursor();

            _isSelecting = Selection.IsValid &&
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
            PdfAnnotation? annotation = control.PdfTextLayer!.FindAnnotationOver(loc.X, loc.Y);

            if (annotation is not null)
            {
                if (!string.IsNullOrEmpty(annotation.Content))
                {
                    ShowAnnotation(control, annotation);
                }

                if (annotation.IsInteractive)
                {
                    control.SetHandCursor();
                    return;
                }
            }
            else
            {
                HideAnnotation(control);
            }

            PdfWord? word = control.PdfTextLayer!.FindWordOver(loc.X, loc.Y);
            if (word is not null)
            {
                if (UrlMatch().IsMatch(word.Value.Span))
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
                _startPointerPressed = point;
                PdfWord? word = control.PdfTextLayer.FindWordOver(point.X, point.Y);

                if (word is not null && Selection.IsWordSelected(control.PageNumber!.Value, word))
                {
                    clearSelection = e.ClickCount == 1; // Clear selection if single click
                    HandleMultipleClick(control, e, word); // TODO - we pass 1 click here too
                }
                else if (word is not null && e.ClickCount == 2)
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

            _startPointerPressed = null;

            var pointerPoint = e.GetCurrentPoint(control);

            bool ignore = _isSelecting || _isMultipleClickSelection;
            if (!ignore && pointerPoint.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                ClearSelection(control);

                // Check link
                if (!_isSelecting)
                {
                    var point = pointerPoint.Position;

                    // Annotation
                    PdfAnnotation? annotation = control.PdfTextLayer.FindAnnotationOver(point.X, point.Y);

                    if (annotation?.Action is not null)
                    {
                        switch (annotation.Action.Type)
                        {
                            case ActionType.URI:
                                string? uri = ((UriAction)annotation.Action)?.Uri;
                                if (!string.IsNullOrEmpty(uri))
                                {
                                    CalyExtensions.OpenBrowser(uri);
                                    return;
                                }
                                break;

                            case ActionType.GoTo:
                            case ActionType.GoToE:
                            case ActionType.GoToR:
                                var goToAction = (AbstractGoToAction)annotation.Action;
                                var dest = goToAction?.Destination;
                                if (dest is not null)
                                {
                                    var documentControl = control.FindAncestorOfType<PdfDocumentControl>();
                                    documentControl?.GoToPage(dest.PageNumber);
                                    return;
                                }
                                else
                                {
                                    // Log error
                                }
                                break;
                        }
                    }

                    // Words
                    PdfWord? word = control.PdfTextLayer.FindWordOver(point.X, point.Y);

                    if (word is not null)
                    {
                        foreach (ValueMatch match in UrlMatch().EnumerateMatches(word.Value.Span))
                        {
                            if (match.Length == 0)
                            {
                                continue;
                            }

                            CalyExtensions.OpenBrowser(word.Value.Span.Slice(match.Index, match.Length));
                            break; // Only opens first url matched
                        }
                    }
                }
            }

            _isSelecting = false;
        }

        private readonly Dictionary<int, PdfWord[]> _searchResults = new();

        public void ClearTextSearchResults(PdfDocumentViewModel documentViewModel)
        {
            var currentPages = _searchResults.Keys.ToArray();
            _searchResults.Clear();

            foreach (int p in currentPages)
            {
                Dispatcher.UIThread.Post(documentViewModel.Pages[p - 1].FlagSelectionChanged);
            }
        }
        
        public void AddTextSearchResults(PdfDocumentViewModel documentViewModel,
            IReadOnlyCollection<TextSearchResultViewModel> searchResults)
        {
            if (searchResults.Count > 0)
            {
                foreach (var result in searchResults)
                {
                    System.Diagnostics.Debug.Assert(result.Nodes is not null);

                    _searchResults[result.PageNumber] = result.Nodes
                        .Where(x => x is { ItemType: SearchResultItemType.Word, Word: not null })
                        .Select(x => x.Word!)
                        .ToArray();

                    Dispatcher.UIThread.Post(documentViewModel.Pages[result.PageNumber - 1].FlagSelectionChanged);
                }
            }
        }

        private static void ShowAnnotation(PdfPageTextLayerControl control, PdfAnnotation annotation)
        {
            //System.Diagnostics.Debug.WriteLine($"Annotation: [{annotation.Date}] '{annotation.Content}'");

            if (FlyoutBase.GetAttachedFlyout(control) is not Flyout attachedFlyout)
            {
                return;
            }

            // TODO - Should we use MVVM instead?
            var contentText = new Avalonia.Controls.TextBlock()
            {
                MaxWidth = 200,
                TextWrapping = TextWrapping.Wrap,
                Text = annotation.Content
            };

            if (!string.IsNullOrEmpty(annotation.Date))
            {
                attachedFlyout.Content = new StackPanel()
                {
                    Orientation = Orientation.Vertical,
                    Children =
                    {
                        new Avalonia.Controls.TextBlock()
                        {
                            Text = annotation.Date
                        },
                        contentText
                    }
                };
            }
            else
            {
                attachedFlyout.Content = contentText;
            }

            attachedFlyout.ShowAt(control);
        }

        private static void HideAnnotation(PdfPageTextLayerControl control)
        {
            if (FlyoutBase.GetAttachedFlyout(control) is Flyout attachedFlyout)
            {
                attachedFlyout.Hide();
                attachedFlyout.Content = null;
            }
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
#if DEBUG
            if (control.PdfTextLayer?.Annotations is not null)
            {
                var purpleBrush = new SolidColorBrush(Colors.Purple, 0.4);
                var purplePen = new Pen(purpleBrush, 0.5);

                foreach (var annotation in control.PdfTextLayer.Annotations)
                {
                    context.DrawGeometry(purpleBrush, purplePen, PdfWordHelpers.GetGeometry(annotation.BoundingBox, true));
                }
            }
#endif

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

            var yellowBrush = new SolidColorBrush(Colors.Yellow, 0.4);
            var yellowPen = new Pen(yellowBrush, 0.5);

            PdfWord? previousWord = null;

            foreach (var block in control.PdfTextLayer.TextBlocks)
            {
                context.DrawGeometry(greenBrush, greenPen, PdfWordHelpers.GetGeometry(block.BoundingBox, true));
                context.DrawEllipse(Brushes.DarkGreen, null, new Point(block.BoundingBox.TopLeft.X, block.BoundingBox.TopLeft.Y), 2, 2);
                context.DrawEllipse(Brushes.DarkBlue, null, new Point(block.BoundingBox.BottomLeft.X, block.BoundingBox.BottomLeft.Y), 2, 2);
                context.DrawEllipse(Brushes.DarkRed, null, new Point(block.BoundingBox.BottomRight.X, block.BoundingBox.BottomRight.Y), 2, 2);

                foreach (var line in block.TextLines)
                {
                    context.DrawGeometry(yellowBrush, yellowPen, PdfWordHelpers.GetGeometry(line.BoundingBox, true));
                    context.DrawEllipse(Brushes.DarkGreen, null, new Point(line.BoundingBox.TopLeft.X, line.BoundingBox.TopLeft.Y), 1, 1);
                    context.DrawEllipse(Brushes.DarkBlue, null, new Point(line.BoundingBox.BottomLeft.X, line.BoundingBox.BottomLeft.Y), 1, 1);
                    context.DrawEllipse(Brushes.DarkRed, null, new Point(line.BoundingBox.BottomRight.X, line.BoundingBox.BottomRight.Y), 1, 1);

                    foreach (var word in line.Words)
                    {
                        context.DrawGeometry(redBrush, redPen, PdfWordHelpers.GetGeometry(word.BoundingBox));
                        context.DrawEllipse(Brushes.DarkGreen, null, new Point(word.BoundingBox.TopLeft.X, word.BoundingBox.TopLeft.Y), 0.5, 0.5);
                        context.DrawEllipse(Brushes.DarkBlue, null, new Point(word.BoundingBox.BottomLeft.X, word.BoundingBox.BottomLeft.Y), 0.5, 0.5);
                        context.DrawEllipse(Brushes.DarkRed, null, new Point(word.BoundingBox.BottomRight.X, word.BoundingBox.BottomRight.Y), 0.5, 0.5);

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

            if (_searchResults.TryGetValue(control.PageNumber!.Value, out var results))
            {
                var searchBrush = new ImmutableSolidColorBrush(_searchColor);

                foreach (PdfWord result in results)
                {
                    context.DrawGeometry(searchBrush, null, PdfWordHelpers.GetGeometry(result));
                }
            }

            var selectionBrush = new ImmutableSolidColorBrush(_selectionColor);

            foreach (var g in Selection.GetPageSelectionAs(control.PageNumber!.Value, PdfWordHelpers.GetGeometry, PdfWordHelpers.GetGeometry))
            {
                context.DrawGeometry(selectionBrush, null, g);
            }
        }
    }
}
