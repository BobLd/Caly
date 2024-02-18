using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Caly.Core.Utilities;
using SkiaSharp;

namespace Caly.Core.Controls
{
    /// <summary>
    /// Skia Pdf page control.
    /// </summary>
    public sealed class SkiaPdfPageControl : Control
    {
        private sealed class SkiaDrawOperation : ICustomDrawOperation
        {
            private readonly IRef<SKPicture>? _picture;

            private readonly SKRect _visibleArea;

            public SkiaDrawOperation(Rect bounds, Rect visibleArea, IRef<SKPicture>? picture)
            {
                _picture = picture;
                _visibleArea = visibleArea.ToSKRect();
                Bounds = bounds;
            }

            public void Dispose()
            {
                // No-op
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
                if (_picture?.Item is null || !context.TryGetFeature(out ISkiaSharpApiLeaseFeature leaseFeature))
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                {
                    return;
                }

                using (var picture = _picture.Clone())
                using (var lease = leaseFeature.Lease())
                {
                    var canvas = lease?.SkCanvas;
                    if (canvas is null)
                    {
                        return;
                    }

                    if (picture.Item.Handle == nint.Zero)
                    {
                        // Dirty rectangle (?) if page not visible anymore, but actually still being rendered
                        // Should not happen anymore thanks to ref count dispose
                        System.Diagnostics.Debug.WriteLine($"ERROR Invalid handle for picture {picture.Item.Handle}");
                        return;
                    }

                    canvas.Save();
                    canvas.ClipRect(_visibleArea);
                    canvas.DrawPicture(picture.Item);
                    canvas.Restore();
                }

                System.Diagnostics.Debug.WriteLine("End Render Page");
            }
        }

        /// <summary>
        /// Defines the <see cref="Picture"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty =
            AvaloniaProperty.Register<SkiaPdfPageControl, IRef<SKPicture>?>(nameof(Picture));

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty =
            AvaloniaProperty.Register<SkiaPdfPageControl, Rect?>(nameof(VisibleArea));

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

        static SkiaPdfPageControl()
        {
            AffectsRender<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
            AffectsMeasure<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
        }

        public SkiaPdfPageControl()
        {
            ClipToBounds = true;
        }

#if DEBUG
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            System.Diagnostics.Debug.WriteLine($"SKDrawableControl.OnPropertyChanged: {change.Property}");
            base.OnPropertyChanged(change);
        }
#endif
        /// <summary>
        /// This operation is executed on UI thread.
        /// </summary>
        public override void Render(DrawingContext context)
        {
            Debug.ThrowNotOnUiThread();

            var viewPort = new Rect(Bounds.Size);

            if (viewPort.IsEmpty())
            {
                base.Render(context);
                return;
            }

            if (!VisibleArea.HasValue || VisibleArea.Value.IsEmpty())
            {
                base.Render(context);
                return;
            }

            var picture = Picture;
            if (picture?.Item is null || picture.Item.CullRect.IsEmpty)
            {
                base.Render(context);
                return;
            }

            using (context.PushClip(viewPort.Intersect(picture.Item.CullRect.ToAvaloniaRect())))
            {
                context.Custom(new SkiaDrawOperation(new Rect(0, 0, Bounds.Width, Bounds.Height), VisibleArea.Value, picture));
            }

            base.Render(context);
        }
    }
}
