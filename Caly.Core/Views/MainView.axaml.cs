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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.VisualTree;
using Caly.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.Views
{
    [TemplatePart("PART_SplitView", typeof(SplitView))]
    public partial class MainView : UserControl
    {
        private SplitView? _splitView;

        public MainView()
        {
            InitializeComponent();
            AddHandler(DragDrop.DropEvent, Drop);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Pass KeyBindings to top level
            if (TopLevel.GetTopLevel(this) is Window w)
            {
                w.KeyBindings.AddRange(KeyBindings);
            }
        }

        private static async void Drop(object? sender, DragEventArgs e)
        {
            try
            {
                if (e.Data is null || !e.Data.Contains(DataFormats.Files))
                {
                    return;
                }

                var files = e.Data.GetFiles();

                if (files is null)
                {
                    return;
                }

                var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>()
                                          ?? throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");

                await Task.Run(() => pdfDocumentsService.OpenLoadDocuments(files, CancellationToken.None));
            }
            catch (Exception ex)
            {
                // TODO - Show dialog
                Debug.WriteExceptionToFile(ex);
            }
        }

        private void TreeView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!e.WidthChanged)
            {
                return;
            }

            if (sender is not TreeView treeView)
            {
                return;
            }

            double width = treeView.Bounds.Width;

            foreach (var control in treeView.GetRealizedContainers().OfType<TreeViewItem>())
            {
                var stackPanel = control.GetVisualChildren().OfType<StackPanel>().FirstOrDefault();
                if (stackPanel is null)
                {
                    continue;
                }

                stackPanel.SetCurrentValue(WidthProperty, width);

                foreach (TextBlock textBlock in stackPanel.GetVisualDescendants().OfType<TextBlock>())
                {
                    textBlock.InvalidateMeasure();
                }
            }
        }

        #region Resize SplitView.Pane

        private static readonly Cursor SizeWestEastCursor = new(StandardCursorType.SizeWestEast);

        private const int _maxPaneLength = 500;
        private const int _minPaneLength = 200;
        private Point? _lastPoint;
        private double _originalPaneLength;

        private void Rectangle_OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Rectangle)
            {
                Cursor = SizeWestEastCursor;
            }
        }

        private void Rectangle_OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is Rectangle)
            {
                Cursor = Cursor.Default;
            }
        }

        private void Rectangle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Rectangle)
            {
                return;
            }

            if (_splitView is null)
            {
                _splitView = this.FindDescendantOfType<SplitView>();
                if (_splitView is null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(_splitView.Name) || !_splitView.Name.Equals("PART_SplitView"))
                {
                    throw new Exception("The found split view does not have the correct name.");
                }
            }

            if (!_splitView.IsPaneOpen)
            {
                return;
            }

            _lastPoint = e.GetPosition(null);
            _originalPaneLength = _splitView.OpenPaneLength;
            e.Handled = true;
            e.PreventGestureRecognition();
        }

        private void Rectangle_OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_lastPoint.HasValue || sender is not Rectangle)
            {
                return;
            }

            if (_splitView is null || !_splitView.IsPaneOpen)
            {
                return;
            }

            Point mouseMovement = (Point)(e.GetPosition(null) - _lastPoint);
            _splitView.OpenPaneLength = Math.Max(Math.Min(_originalPaneLength + mouseMovement.X, _maxPaneLength), _minPaneLength);
        }

        private void Rectangle_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_lastPoint.HasValue || sender is not Rectangle)
            {
                return;
            }

            _lastPoint = null;
            _originalPaneLength = 0;
            e.Handled = true;
        }
        #endregion
    }
}
