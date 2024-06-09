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
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Caly.Core.ViewModels;
using UglyToad.PdfPig.Core;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive;

namespace Caly.Core.Controls
{
    internal sealed class PdfPageSearchLayerControl : Control
    {
        public static readonly StyledProperty<int?> PageNumberProperty = AvaloniaProperty.Register<PdfPageTextLayerControl, int?>(nameof(PageNumber));

        public static readonly StyledProperty<TextSearchResultViewModel?> SelectedTextSearchResultProperty = AvaloniaProperty.Register<PdfDocumentControl, TextSearchResultViewModel?>(nameof(SelectedTextSearchResult));

        public static readonly StyledProperty<ObservableCollection<TextSearchResultViewModel>?> TextSearchResultsProperty = AvaloniaProperty.Register<PdfDocumentControl, ObservableCollection<TextSearchResultViewModel>?>(nameof(TextSearchResults));

        public int? PageNumber
        {
            get => GetValue(PageNumberProperty);
            set => SetValue(PageNumberProperty, value);
        }

        public TextSearchResultViewModel? SelectedTextSearchResult
        {
            get => GetValue(SelectedTextSearchResultProperty);
            set => SetValue(SelectedTextSearchResultProperty, value);
        }

        public ObservableCollection<TextSearchResultViewModel>? TextSearchResults
        {
            get => GetValue(TextSearchResultsProperty);
            set => SetValue(TextSearchResultsProperty, value);
        }

        static PdfPageSearchLayerControl()
        {
            AffectsRender<PdfPageSearchLayerControl>(SelectedTextSearchResultProperty, TextSearchResultsProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextSearchResultsProperty)
            {
                if (change.NewValue is not null)
                {
                    TextSearchResults
                        .GetWeakCollectionChangedObservable()
                        .Subscribe(e =>
                        {
                            InvalidateVisual();
                        });
                }
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Bounds.Width <= 0 || Bounds.Height <= 0 || TextSearchResults is null || !TextSearchResults.Any())
            {
                return;
            }

            var selectionBrush = new ImmutableSolidColorBrush(_selectionColor);

            foreach (TextSearchResultViewModel result in TextSearchResults.Where(r => r.PageNumber.Equals(PageNumber)))
            {
                // TODO - Should do recursion
                if (result.Nodes is not null)
                {
                    foreach (var node in result.Nodes)
                    {
                        if (node.Word is null)
                        {
                            continue;
                        }
                        context.DrawGeometry(selectionBrush, null, GetGeometry(node.Word.BoundingBox, true));
                    }
                }
            }
        }

        private static readonly Color _selectionColor = Color.FromArgb(200, 255, 0, 0);

        private static StreamGeometry GetGeometry(PdfRectangle rect, bool isFilled = false)
        {
            var sg = new StreamGeometry();
            using (var ctx = sg.Open())
            {
                ctx.BeginFigure(new Point(rect.BottomLeft.X, rect.BottomLeft.Y), isFilled);
                ctx.LineTo(new Point(rect.TopLeft.X, rect.TopLeft.Y));
                ctx.LineTo(new Point(rect.TopRight.X, rect.TopRight.Y));
                ctx.LineTo(new Point(rect.BottomRight.X, rect.BottomRight.Y));
                ctx.EndFigure(true);
            }

            return sg;
        }
    }
}
