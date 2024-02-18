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
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Caly.Core.Handlers.Interfaces;
using Caly.Pdf.Models;

namespace Caly.Core.Controls
{
    public sealed class PdfPageTextLayerControl : Control
    {
        // https://github.com/AvaloniaUI/Avalonia/blob/master/src/Avalonia.Controls/Primitives/TextSelectionCanvas.cs#L62
        // Check caret handle

        private static readonly Cursor IbeamCursor = new(StandardCursorType.Ibeam);
        private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

        private ITextSelectionHandler? _textSelectionHandler;
        private IDisposable? _pointerMovedDisposable;
        private IDisposable? _pointerPressedDisposable;
        private IDisposable? _pointerReleasedDisposable;

        public static readonly StyledProperty<PdfTextLayer?> PdfPageTextLayerProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, PdfTextLayer?>(nameof(PdfPageTextLayer));

        public static readonly StyledProperty<int?> PageNumberProperty =
            AvaloniaProperty.Register<PdfPageTextLayerControl, int?>(nameof(PageNumber));

        public PdfTextLayer? PdfPageTextLayer
        {
            get => GetValue(PdfPageTextLayerProperty);
            set => SetValue(PdfPageTextLayerProperty, value);
        }

        public int? PageNumber
        {
            get => GetValue(PageNumberProperty);
            set => SetValue(PageNumberProperty, value);
        }

        static PdfPageTextLayerControl()
        {
            AffectsRender<PdfPageTextLayerControl>(PdfPageTextLayerProperty);
        }

        internal void SetIbeamCursor()
        {
            if (Cursor == IbeamCursor)
            {
                return;
            }

            Cursor = IbeamCursor;
        }

        internal void SetHandCursor()
        {
            if (Cursor == HandCursor)
            {
                return;
            }

            Cursor = HandCursor;
        }

        internal void SetDefaultCursor()
        {
            if (Cursor == Cursor.Default)
            {
                return;
            }

            Cursor = Cursor.Default;
        }

        internal void SelectTextToEnd()
        {
            _textSelectionHandler?.SelectTextToEndInPage(this);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Bounds.Width <= 0 || Bounds.Height <= 0)
            {
                return;
            }

            // We need to fill to get Pointer events
            context.FillRectangle(Brushes.Transparent, Bounds);

            _textSelectionHandler?.RenderPage(this, context);
        }

        public void AttachTextSelectionHandler(ITextSelectionHandler textSelectionHandler)
        {
            // If the textSelectionHandler was already attached, we unsubscribe
            _pointerMovedDisposable?.Dispose();
            _pointerPressedDisposable?.Dispose();
            _pointerReleasedDisposable?.Dispose();

            _textSelectionHandler = textSelectionHandler;

            _pointerMovedDisposable = this.GetObservable(PointerMovedEvent)
                .DistinctUntilChanged()
                .Subscribe(_textSelectionHandler!.OnPointerMoved);

            _pointerPressedDisposable = this.GetObservable(PointerPressedEvent)
                .DistinctUntilChanged()
                .Subscribe(_textSelectionHandler.OnPointerPressed);

            _pointerReleasedDisposable = this.GetObservable(PointerReleasedEvent)
                .DistinctUntilChanged()
                .Subscribe(_textSelectionHandler.OnPointerReleased);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            // First time the text layer is attached, we retrieve the TextSelectionHandler
            if (_textSelectionHandler is null)
            {
                var pdfDocumentControl = this.FindAncestorOfType<PdfDocumentControl>();
                if (pdfDocumentControl is null)
                {
                    throw new NullReferenceException($"Could not find ancestor of type {typeof(PdfDocumentControl)}.");
                }

                System.Diagnostics.Debug.Assert(pdfDocumentControl.TextSelectionHandler is not null);
                AttachTextSelectionHandler(pdfDocumentControl.TextSelectionHandler);
            }
#if DEBUG
            else
            {
                System.Diagnostics.Debug.WriteLine("TextSelectionHandler not null");
            }
#endif
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            _pointerMovedDisposable?.Dispose();
            _pointerPressedDisposable?.Dispose();
            _pointerReleasedDisposable?.Dispose();
        }
    }
}
