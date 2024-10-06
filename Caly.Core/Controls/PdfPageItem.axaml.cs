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
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;
using SkiaSharp;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_PdfPageTextLayerControl", typeof(PdfPageTextLayerControl))]
    public class PdfPageItem : ContentControl
    {
        /// <summary>
        /// Defines the <see cref="IsPageRendering"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsPageRenderingProperty = AvaloniaProperty.Register<PdfPageItem, bool>(nameof(IsPageRendering));

        /// <summary>
        /// Defines the <see cref="Picture"/> property.
        /// </summary>
        public static readonly StyledProperty<IRef<SKPicture>?> PictureProperty = AvaloniaProperty.Register<PdfPageItem, IRef<SKPicture>?>(nameof(Picture), defaultBindingMode: BindingMode.OneWay);

        /// <summary>
        /// Defines the <see cref="IsPageVisible"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsPageVisibleProperty = AvaloniaProperty.Register<PdfPageItem, bool>(nameof(IsPageVisible), false);

        /// <summary>
        /// Defines the <see cref="VisibleArea"/> property.
        /// </summary>
        public static readonly StyledProperty<Rect?> VisibleAreaProperty = AvaloniaProperty.Register<PdfPageItem, Rect?>(nameof(VisibleArea), null, defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="Exception"/> property.
        /// </summary>
        public static readonly StyledProperty<ExceptionViewModel?> ExceptionProperty = AvaloniaProperty.Register<PdfPageItem, ExceptionViewModel?>(nameof(Exception), defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Defines the <see cref="PdfPageTextLayerControl"/> property.
        /// </summary>
        public static readonly DirectProperty<PdfPageItem, PdfPageTextLayerControl?> TextLayerProperty =
            AvaloniaProperty.RegisterDirect<PdfPageItem, PdfPageTextLayerControl?>(nameof(LayoutTransformControl), o => o.TextLayer);

        private PdfPageTextLayerControl? _textLayer;

        static PdfPageItem()
        {
            AffectsRender<PdfPageItem>(PictureProperty, IsPageVisibleProperty);
        }

        public bool IsPageRendering
        {
            get => GetValue(IsPageRenderingProperty);
            set => SetValue(IsPageRenderingProperty, value);
        }

        public IRef<SKPicture>? Picture
        {
            get => GetValue(PictureProperty);
            set => SetValue(PictureProperty, value);
        }

        public bool IsPageVisible
        {
            get => GetValue(IsPageVisibleProperty);
            set => SetValue(IsPageVisibleProperty, value);
        }

        public Rect? VisibleArea
        {
            get => GetValue(VisibleAreaProperty);
            set => SetValue(VisibleAreaProperty, value);
        }

        public ExceptionViewModel? Exception
        {
            get => GetValue(ExceptionProperty);
            set => SetValue(ExceptionProperty, value);
        }

        /// <summary>
        /// Gets the text layer.
        /// </summary>
        public PdfPageTextLayerControl? TextLayer
        {
            get => _textLayer;
            private set => SetAndRaise(TextLayerProperty, ref _textLayer, value);
        }

        public PdfPageItem()
        {
#if DEBUG
            if (Design.IsDesignMode)
            {
                // Only if in design mode
                DataContext = new PdfPageViewModel();
            }
#endif
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            TextLayer = e.NameScope.FindFromNameScope<PdfPageTextLayerControl>("PART_PdfPageTextLayerControl");
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            Picture?.Dispose();

            System.Diagnostics.Debug.Assert((Picture?.RefCount ?? 0) == 0);
        }
    }
}
