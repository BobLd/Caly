using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Caly.Core.Controls
{
    public class PdfPageThumbnailControl : TemplatedControl
    {
        /*
         * See PDF Reference 1.7 - C.2 Architectural limits
         * Thumbnail images should be no larger than 106 by 106 samples, and should be created at one-eighth scale for 8.5-by-11-inch and A4-size pages.
         */

        private readonly Brush _areaBrush = new SolidColorBrush(Colors.DodgerBlue, 0.3);
        private readonly Pen _areaPen = new Pen(Colors.DodgerBlue.ToUInt32());

        private Matrix _scale = Matrix.Identity;

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, Rect?>(nameof(VisibleArea), null);

        /// <summary>
        /// Defines the <see cref="ThumbnailHeight"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ThumbnailHeightProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, double>(nameof(ThumbnailHeight));

        /// <summary>
        /// Defines the <see cref="PageHeight"/> property.
        /// </summary>
        public static readonly StyledProperty<double> PageHeightProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, double>(nameof(PageHeight));

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        public double ThumbnailHeight
        {
            get => GetValue(ThumbnailHeightProperty);
            set => SetValue(ThumbnailHeightProperty, value);
        }

        public double PageHeight
        {
            get => GetValue(PageHeightProperty);
            set => SetValue(PageHeightProperty, value);
        }

        static PdfPageThumbnailControl()
        {
            AffectsRender<PdfPageThumbnailControl>(VisibleAreaProperty, ThumbnailHeightProperty, PageHeightProperty);
            AffectsMeasure<PdfPageThumbnailControl>(VisibleAreaProperty, ThumbnailHeightProperty, PageHeightProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ThumbnailHeightProperty || change.Property == PageHeightProperty)
            {
                UpdateScaleMatrix();
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.FillRectangle(Brushes.White, Bounds); // TODO - Should render page image instead?

#if DEBUG
            if (DataContext is ViewModels.PdfPageViewModel vm && vm.IsPageVisible)
            {
                context.DrawRectangle(new Pen(Colors.Red.ToUInt32(), 4), Bounds);
            }
#endif

            if (VisibleArea.HasValue)
            {
                var area = VisibleArea.Value.TransformToAABB(_scale);
                context.DrawRectangle(_areaBrush.ToImmutable(), _areaPen.ToImmutable(), area);
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
