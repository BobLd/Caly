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
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Caly.Core.Utilities;
using SkiaSharp;

namespace Caly.Core.Controls
{
    // An attempt at rendering on background thread
    // See https://stackoverflow.com/questions/64737621/c-sharp-skiasharp-opentk-winform-how-to-draw-from-a-background-thread

    /// <summary>
    /// Skia Pdf page control.
    /// </summary>
    public sealed class SkiaRenderLoopPdfPageControl : Control
    {
        private sealed class SkiaDrawOperation : ICustomDrawOperation
        {
            private readonly SKPaint? _paint;

            //private readonly SKRect _visibleArea;

            public SkiaDrawOperation(Rect bounds, SKPaint? paint) // Rect visibleArea,
            {
                _paint = paint;
                //_visibleArea = visibleArea.ToSKRect();
                Bounds = bounds;
            }

            public void Dispose()
            {
                // No op
            }

            public Rect Bounds { get; }

            public bool HitTest(Point p) => Bounds.Contains(p);

            public bool Equals(ICustomDrawOperation? other) => false;

            /// <summary>
            /// This operation is executed on Render thread.
            /// </summary>
            public void Render(ImmediateDrawingContext context)
            {
                Debug.ThrowOnUiThread();

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                if (!context.TryGetFeature(out ISkiaSharpApiLeaseFeature leaseFeature))
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                {
                    return;
                }

                using (var lease = leaseFeature.Lease())
                {
                    var canvas = lease?.SkCanvas;
                    if (canvas is null)
                    {
                        return;
                    }

                    canvas.Save();
                    //canvas.ClipRect(_visibleArea);
                    canvas.DrawPaint(_paint);
                    canvas.Restore();
                }
            }
        }

        private readonly AutoResetEvent _renderAutoResetEvent;
        private readonly Task _renderTask;
        private bool _shouldRender = true;
        private int _requestsCount;
        private SKPaint _defaultBitmap;
        private SKPaint _scaledImage;
        private IRef<SKPicture>? _picture;
        private Rect? _visibleArea;

        /// <summary>
        /// Defines the <see cref="Picture"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty =
            AvaloniaProperty.Register<SkiaRenderLoopPdfPageControl, IRef<SKPicture>?>(nameof(Picture));

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty =
            AvaloniaProperty.Register<SkiaRenderLoopPdfPageControl, Rect?>(nameof(VisibleArea));

        /// <summary>
        /// Gets or sets the <see cref="SKPicture"/> picture.
        /// </summary>
        [Content]
        public IRef<SKPicture>? Picture
        {
            get => GetValue(PictureProperty);
            set => SetValue(PictureProperty, value);
        }

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        static SkiaRenderLoopPdfPageControl()
        {
            ClipToBoundsProperty.OverrideDefaultValue<SkiaPdfPageControl>(true);
        }

        public SkiaRenderLoopPdfPageControl()
        {
            ResetBitmap();
            _renderAutoResetEvent = new AutoResetEvent(false);
            _renderTask = Task.Run(RenderingLoop);
        }

        private async Task RenderingLoop()
        {
            await Task.Yield();

            Debug.ThrowOnUiThread();

            while (_shouldRender)
            {
                await Task.Delay(100);
                if (RenderPage())
                {
                    Dispatcher.UIThread.Post(InvalidateVisual);
                }

                _renderAutoResetEvent.WaitOne();
            }
        }

        private bool RenderPage()
        {
            Debug.ThrowOnUiThread();
            if (_requestsCount == 0)
            {
                return false;
            }

            if (_requestsCount > 1)
            {
                System.Diagnostics.Debug.WriteLine($"DrawLayers(): SKIP queue {_requestsCount}");
                Interlocked.Exchange(ref _requestsCount, 1);
                return false;
            }

            if (!_visibleArea.HasValue || _visibleArea.Value.IsEmpty())
            {
                Interlocked.Decrement(ref _requestsCount);
                return false;
            }

            var picture = _picture;
            if (picture?.Item is null || picture.Item.CullRect.IsEmpty)
            {
                Interlocked.Decrement(ref _requestsCount);
                return false;
            }

            var translate = SKMatrix.CreateTranslation((float)_visibleArea.Value.Left, (float)_visibleArea.Value.Top);
            _scaledImage = new SKPaint()
            {
                Shader = picture.Item.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, translate,
                    _visibleArea.Value.ToSKRect())
            };

            Interlocked.Decrement(ref _requestsCount);
            System.Diagnostics.Debug.WriteLine($"DrawLayers(): queue {_requestsCount}");
            return _requestsCount == 0;
        }

        private void RequestRendering()
        {
            _scaledImage = _defaultBitmap;
            Dispatcher.UIThread.Post(InvalidateVisual);
            Interlocked.Increment(ref _requestsCount);
            _renderAutoResetEvent.Set();
        }

        /// <summary>
        /// This operation is executed on UI thread.
        /// </summary>
        public override void Render(DrawingContext context)
        {
            Debug.ThrowNotOnUiThread();

            var viewPort = new Rect(Bounds.Size);

            if (viewPort.IsEmpty() || !_visibleArea.HasValue || _visibleArea.Value.IsEmpty())
            {
                base.Render(context);
                return;
            }

            //using (context.PushClip(viewPort.Intersect(_visibleArea.Value)))
            context.Custom(new SkiaDrawOperation(viewPort, _scaledImage)); //_visibleArea.Value,

            base.Render(context);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PictureProperty)
            {
                _picture = change.NewValue as IRef<SKPicture>;
                RenderAsBitmap();
                RequestRendering();
            }
            else if (change.Property == VisibleAreaProperty)
            {
                _visibleArea = change.NewValue as Rect?;
                RequestRendering();
            }
            else if (change.Property == DataContextProperty)
            {
                ResetBitmap();
            }
        }

        private void ResetBitmap()
        {
            _defaultBitmap = new SKPaint()
            {
#if DEBUG
                Color = SKColors.Aquamarine
#else
                Color = SKColors.Transparent
#endif
            };
            _scaledImage = _defaultBitmap;
        }

        private void RenderAsBitmap()
        {
            var picture = _picture;
            if (picture?.Item is null || picture.Item.CullRect.IsEmpty)
            {
                return;
            }

            using (var bitmap = new SKBitmap((int)picture.Item.CullRect.Width, (int)picture.Item.CullRect.Height))
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.DrawPicture(picture.Item);
                _defaultBitmap = new SKPaint() { Shader = bitmap.ToShader() };
                _scaledImage = _defaultBitmap;
            }
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            GC.KeepAlive(_renderTask);
            _shouldRender = false;
            _renderAutoResetEvent.Set();
            _renderAutoResetEvent.Dispose();
            Picture?.Dispose();
        }
    }
}
