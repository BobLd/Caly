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
            private readonly SKPaint? _picture;

            private readonly SKRect _visibleArea;

            public SkiaDrawOperation(Rect bounds, Rect visibleArea, SKPaint? picture)
            {
                _picture = picture;
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
                // See https://github.com/AvaloniaUI/Avalonia/commit/f3f26eb113ceec63e310115d34a8f02fe8e86b51
                // What does that do?

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
                    canvas.ClipRect(_visibleArea);
                    canvas.DrawPaint(_picture);
                    canvas.Restore();
                }
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
            ClipToBoundsProperty.OverrideDefaultValue<SkiaPdfPageControl>(true);

            AffectsRender<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
            AffectsMeasure<SkiaPdfPageControl>(PictureProperty, VisibleAreaProperty);
        }

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

            SKMatrix translate = SKMatrix.CreateTranslation((float)VisibleArea.Value.Left, (float)VisibleArea.Value.Top);
            SKRect tile = VisibleArea.Value.ToSKRect();
            var tileMode = SKShaderTileMode.Clamp;
            // TODO - Do we need to dispose shader and paint?

            SKPaint paint = new SKPaint() { Shader = picture.Item.ToShader(tileMode, tileMode, translate, tile) };
            context.Custom(new SkiaDrawOperation(viewPort, VisibleArea.Value, paint));

            base.Render(context);
        }
    }
}
