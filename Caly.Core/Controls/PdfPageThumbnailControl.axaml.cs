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
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace Caly.Core.Controls
{
    public sealed class PdfPageThumbnailControl : TemplatedControl
    {
        /*
         * See PDF Reference 1.7 - C.2 Architectural limits
         * Thumbnail images should be no larger than 106 by 106 samples, and should be created at one-eighth scale for 8.5-by-11-inch and A4-size pages.
         */

        private const double _borderThickness = 2;

        private static readonly Color _areaColor = Colors.DodgerBlue;
        private static readonly Brush _areaBrush = new SolidColorBrush(_areaColor);
        private static readonly Brush _areaTransparentBrush = new SolidColorBrush(_areaColor, 0.3);
        private static readonly Pen _areaPen = new Pen() { Brush = _areaBrush, Thickness = _borderThickness };

        private Matrix _scale = Matrix.Identity;

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, Rect?>(nameof(VisibleArea), null);

        /// <summary>
        /// Defines the <see cref="ThumbnailWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ThumbnailWidthProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, double>(nameof(ThumbnailWidth));

        /// <summary>
        /// Defines the <see cref="ThumbnailHeight"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ThumbnailHeightProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, double>(nameof(ThumbnailHeight));

        /// <summary>
        /// Defines the <see cref="PageWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<double> PageWidthProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, double>(nameof(PageWidth));

        /// <summary>
        /// Defines the <see cref="PageHeight"/> property.
        /// </summary>
        public static readonly StyledProperty<double> PageHeightProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, double>(nameof(PageHeight));

        /// <summary>
        /// Defines the <see cref="Thumbnail"/> property.
        /// </summary>
        public static readonly StyledProperty<IImage?> ThumbnailProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, IImage?>(nameof(Thumbnail));

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        public double ThumbnailWidth
        {
            get => GetValue(ThumbnailWidthProperty);
            set => SetValue(ThumbnailWidthProperty, value);
        }

        public double ThumbnailHeight
        {
            get => GetValue(ThumbnailHeightProperty);
            set => SetValue(ThumbnailHeightProperty, value);
        }

        public double PageWidth
        {
            get => GetValue(PageWidthProperty);
            set => SetValue(PageWidthProperty, value);
        }

        public double PageHeight
        {
            get => GetValue(PageHeightProperty);
            set => SetValue(PageHeightProperty, value);
        }

        public IImage? Thumbnail
        {
            get => GetValue(ThumbnailProperty);
            set => SetValue(ThumbnailProperty, value);
        }

        static PdfPageThumbnailControl()
        {
            AffectsRender<PdfPageThumbnailControl>(ThumbnailProperty, VisibleAreaProperty, ThumbnailHeightProperty, PageHeightProperty);
            AffectsMeasure<PdfPageThumbnailControl>(ThumbnailProperty, VisibleAreaProperty, ThumbnailHeightProperty, PageHeightProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ThumbnailWidthProperty ||
                change.Property == ThumbnailHeightProperty ||
                change.Property == PageWidthProperty ||
                change.Property == PageHeightProperty)
            {
                UpdateScaleMatrix();
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            try
            {
                // Bitmap might not be null here but already disposed.
                // We use Dispatcher.UIThread.Invoke(() => t?.Dispose(), DispatcherPriority.Loaded);
                // in the PdfPageViewModel to avoid this issue

                var thumbnail = Thumbnail;
                if (thumbnail is not null && Bounds is { Width: > 0, Height: > 0 })
                {
                    context.DrawImage(thumbnail, Bounds);
                }
            }
            catch (Exception e)
            {
                // We just ignore for the moment
                Debug.WriteExceptionToFile(e);
            }

            if (VisibleArea.HasValue)
            {
                var area = VisibleArea.Value.TransformToAABB(_scale);

                if (area is { Width: > _borderThickness, Height: > _borderThickness })
                {
                    context.DrawRectangle(_areaTransparentBrush.ToImmutable(), _areaPen.ToImmutable(), area.Deflate(_borderThickness / 2.0));
                }
                else
                {
                    context.DrawRectangle(_areaBrush.ToImmutable(), null, area);
                }
            }
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            if (Thumbnail is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private void UpdateScaleMatrix()
        {
            if (IsNotValid(PageHeight) || IsNotValid(ThumbnailHeight))
            {
                return;
            }

            double ratio = Math.Round(ThumbnailHeight / PageHeight, 7);
            _scale = Matrix.CreateScale(ratio, ratio);
        }

        private static bool IsNotValid(double v)
        {
            return v <= 0 || double.IsInfinity(v) || double.IsNaN(v);
        }
    }
}
