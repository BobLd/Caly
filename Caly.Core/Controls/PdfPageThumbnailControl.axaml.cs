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
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Caly.Core.Controls
{
    public class PdfPageThumbnailControl : TemplatedControl
    {
        /*
         * See PDF Reference 1.7 - C.2 Architectural limits
         * Thumbnail images should be no larger than 106 by 106 samples, and should be created at one-eighth scale for 8.5-by-11-inch and A4-size pages.
         */

        private static readonly Brush _areaBrush = new SolidColorBrush(Colors.DodgerBlue, 0.3);
        private static readonly Pen _areaPen = new Pen(Colors.DodgerBlue.ToUInt32());

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

        /// <summary>
        /// Defines the <see cref="LoadThumbnailCommand"/> property.
        /// </summary>
        public static readonly StyledProperty<ICommand?> LoadThumbnailCommandProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, ICommand?>(nameof(LoadThumbnailCommand));

        /// <summary>
        /// Defines the <see cref="UnloadThumbnailCommand"/> property.
        /// </summary>
        public static readonly StyledProperty<ICommand?> UnloadThumbnailCommandProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, ICommand?>(nameof(UnloadThumbnailCommand));

        /// <summary>
        /// Defines the <see cref="Thumbnail"/> property.
        /// </summary>
        public static readonly StyledProperty<Bitmap?> ThumbnailProperty = AvaloniaProperty.Register<PdfPageThumbnailControl, Bitmap?>(nameof(Thumbnail));

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

        /// <summary>
        /// Gets or sets an <see cref="ICommand"/> to be invoked when the page picture needs to be loaded.
        /// <para>This is when the page becomes 'visible'.</para>
        /// </summary>
        public ICommand? LoadThumbnailCommand
        {
            get => GetValue(LoadThumbnailCommandProperty);
            set => SetValue(LoadThumbnailCommandProperty, value);
        }

        /// <summary>
        /// Gets or sets an <see cref="ICommand"/> to be invoked when the page picture needs to be unloaded.
        /// <para>This is when the page becomes 'invisible'.</para>
        /// </summary>
        public ICommand? UnloadThumbnailCommand
        {
            get => GetValue(UnloadThumbnailCommandProperty);
            set => SetValue(UnloadThumbnailCommandProperty, value);
        }

        public Bitmap? Thumbnail
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
            if (change.Property == ThumbnailHeightProperty || change.Property == PageHeightProperty)
            {
                UpdateScaleMatrix();
            }
            else if (change.Property == UnloadThumbnailCommandProperty)
            {
                if (change.OldValue is ICommand o)
                {
                    o.Execute(null);
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.FillRectangle(Brushes.White, Bounds);

            try
            {
                // TODO - Bitmap is not the correct object
                // We should use a similar approach to SKPicture
                var thumbnail = Thumbnail;
                if (thumbnail is not null)
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
                context.DrawRectangle(_areaBrush.ToImmutable(),
                    _areaPen.ToImmutable(),
                    VisibleArea.Value.TransformToAABB(_scale));
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
