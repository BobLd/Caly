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
    // https://stackoverflow.com/questions/64737621/c-sharp-skiasharp-opentk-winform-how-to-draw-from-a-background-thread

    internal class SkiaRenderLoopPdfPageControl2 : Control
    {
        private sealed class SkiaDrawOperation : ICustomDrawOperation
        {
            private readonly SKPaint _background;
            private readonly SKPaint? _paint;

            private readonly SKRect _visibleArea;

            public SkiaDrawOperation(Rect bounds, Rect visibleArea, SKPaint background, SKPaint? paint)
            {
                _background = background;
                _paint = paint;
                _visibleArea = visibleArea.ToSKRect();
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
                    if (_paint is not null)
                    {
                        canvas.Save();
                        canvas.ClipRect(_visibleArea, SKClipOperation.Difference);
                        canvas.DrawPaint(_background);
                        canvas.Restore();

                        canvas.ClipRect(_visibleArea);
                        canvas.DrawPaint(_paint);
                    }
                    else
                    {
                        canvas.DrawPaint(_background);
                    }
                    canvas.Restore();
                }
            }
        }

        private readonly AutoResetEvent _isRendering = new AutoResetEvent(true);
        private readonly AutoResetEvent _renderAutoResetEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent _exitAutoResetEvent = new AutoResetEvent(false);
        private readonly CancellationTokenSource _ctr = new CancellationTokenSource();
        private readonly WaitHandle[] _handles;
        private readonly Task _renderTask;

        private bool _canRender = true;

        /// <summary>
        /// Defines the <see cref="Picture"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty =
            AvaloniaProperty.Register<SkiaRenderLoopPdfPageControl2, IRef<SKPicture>?>(nameof(Picture));

        /// <summary>
        /// Defines the <see cref="Bitmap"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKBitmap>?> BitmapProperty =
            AvaloniaProperty.Register<SkiaRenderLoopPdfPageControl2, IRef<SKBitmap>?>(nameof(Bitmap));

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty =
            AvaloniaProperty.Register<SkiaRenderLoopPdfPageControl2, Rect?>(nameof(VisibleArea));

        private IRef<SKPicture>? _picture;
        private Rect? _visibleArea;

        /// <summary>
        /// Gets or sets the <see cref="SKPicture"/> picture.
        /// </summary>
        [Content]
        public IRef<SKPicture>? Picture
        {
            get => GetValue(PictureProperty);
            set => SetValue(PictureProperty, value);
        }

        public IRef<SKBitmap>? Bitmap
        {
            get => GetValue(BitmapProperty);
            set => SetValue(BitmapProperty, value);
        }

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        public SkiaRenderLoopPdfPageControl2()
        {
            _handles = [_renderAutoResetEvent, _exitAutoResetEvent];
            _renderTask = Task.Run(RenderingLoop);
        }

        private SKPaint? _scaledImage;
        private SKPaint _backgroundPaint = new SKPaint()
        {
            Color = SKColors.Transparent
        };

        private void RenderingLoop()
        {
            Debug.ThrowOnUiThread();

            while (_canRender)
            {
                int ar = WaitHandle.WaitAny(_handles);
                if (ar == 0)
                {
                    // Render requested
                    if (_isRendering.WaitOne(0))
                    {
                        //_isRendering.Reset();
                        Task.Run(Render);
                    }
                    else
                    {
                        // Already rendering
                        _scaledImage = null;
                        Dispatcher.UIThread.Post(InvalidateVisual);
                    }
                }
                else
                {
                    // Cancel requested
                    _canRender = false;
                    _ctr.Cancel();

                    _renderAutoResetEvent.Dispose();
                    _exitAutoResetEvent.Dispose();
                    _isRendering.Dispose();
                    _ctr.Dispose();
                    return;
                }
            }
        }

        private async Task Render()
        {
            try
            {
                if (!_canRender)
                {
                    return;
                }

                await Task.Delay(250, _ctr.Token);
                var picture = _picture;
                if (!_visibleArea.HasValue || _visibleArea.Value.IsEmpty() ||
                    picture?.Item is null || picture.Item.CullRect.IsEmpty)
                {
                    _scaledImage = null;
                    return;
                }

                var tileMode = SKShaderTileMode.Clamp;
                var translation = SKMatrix.CreateTranslation((float)_visibleArea.Value.Left, (float)_visibleArea.Value.Top);
                var tile = _visibleArea.Value.ToSKRect();

                _scaledImage = new SKPaint() { Shader = picture.Item.ToShader(tileMode, tileMode, translation, tile) };

                Dispatcher.UIThread.Post(InvalidateVisual);
            }
            catch (OperationCanceledException)
            {
                // No op
            }
            finally
            {
                if (_canRender)
                {
                    _isRendering.Set();
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            Debug.ThrowNotOnUiThread();

            var viewPort = new Rect(Bounds.Size);

            if (viewPort.IsEmpty() || !_visibleArea.HasValue || _visibleArea.Value.IsEmpty())
            {
                base.Render(context);
                return;
            }

            context.Custom(new SkiaDrawOperation(viewPort, _visibleArea.Value, _backgroundPaint, _scaledImage));

            base.Render(context);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == DataContextProperty)
            {
                _backgroundPaint = new SKPaint()
                {
                    Color = SKColors.Transparent
                };
                _scaledImage = null;
                _picture = null;
                Dispatcher.UIThread.Post(InvalidateVisual);
            }
            else if (change.Property == PictureProperty)
            {
                _picture = change.NewValue as IRef<SKPicture>;
                RequestRender();
            }
            else if (change.Property == BitmapProperty)
            {
                if (change.NewValue is IRef<SKBitmap> bitmap)
                {
                    _backgroundPaint = new SKPaint()
                    {
                        Shader = bitmap.Item.ToShader(),
                        FilterQuality = SKFilterQuality.High
                    };
                }
                else
                {
                    _backgroundPaint = new SKPaint()
                    {
                        Color = SKColors.Transparent
                    };
                }
                RequestRender();
            }
            else if (change.Property == VisibleAreaProperty)
            {
                _visibleArea = change.NewValue as Rect?;

                if (change.OldValue is null && _visibleArea.HasValue)
                {
                    Dispatcher.UIThread.Post(InvalidateVisual);
                }

                if (_visibleArea.HasValue)
                {
                    RequestRender();
                }
            }
        }

        private void RequestRender()
        {
            if (!_canRender)
            {
                return;
            }
            _renderAutoResetEvent.Set();
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            _exitAutoResetEvent.Set();
            GC.KeepAlive(_renderTask);
        }
    }
}
