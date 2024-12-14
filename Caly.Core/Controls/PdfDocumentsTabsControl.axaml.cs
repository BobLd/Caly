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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls;

public sealed partial class PdfDocumentsTabsControl : UserControl
{
    private static readonly Cursor SizeWestEastCursor = new(StandardCursorType.SizeWestEast);

    private const int MaxPaneLength = 500;
    private const int MinPaneLength = 200;

    private Point? _lastPoint;
    private double _originalPaneLength;

    private SplitView? _splitView;

    public PdfDocumentsTabsControl()
    {
        InitializeComponent();
    }

    private SplitView? GetSplitView()
    {
        if (_splitView is null)
        {
            _splitView = this.FindDescendantOfType<SplitView>();
            if (_splitView is null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(_splitView.Name) || !_splitView.Name.Equals("PART_SplitView"))
            {
                throw new Exception("The found split view does not have the correct name.");
            }
        }

        return _splitView;
    }

    #region Resize SplitView.Pane
    private void Resize_OnPointerEntered(object? sender, PointerEventArgs e)
    {
        Cursor = SizeWestEastCursor;
    }

    private void Resize_OnPointerExited(object? sender, PointerEventArgs e)
    {
        Cursor = Cursor.Default;
    }

    private void Resize_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Grid grid)
        {
            return;
        }

        SplitView? splitView = GetSplitView();

        if (splitView is null)
        {
            return;
        }

        if (!splitView.IsPaneOpen)
        {
            return;
        }

        _lastPoint = e.GetPosition(null);
        _originalPaneLength = splitView.OpenPaneLength;
        e.Handled = true;
        e.PreventGestureRecognition();
    }

    private void Resize_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_lastPoint.HasValue || sender is not Grid)
        {
            return;
        }

        SplitView? splitView = GetSplitView();

        if (splitView is null || !splitView.IsPaneOpen)
        {
            return;
        }

        Point mouseMovement = (e.GetPosition(null) - _lastPoint).Value;
        splitView.OpenPaneLength = Math.Max(Math.Min(_originalPaneLength + mouseMovement.X, MaxPaneLength), MinPaneLength);
    }

    private void Resize_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_lastPoint.HasValue || sender is not Grid)
        {
            return;
        }

        _lastPoint = null;
        _originalPaneLength = 0;
        e.Handled = true;
    }
    #endregion

    private void PageNumberTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (sender is TextBox { DataContext: PdfDocumentViewModel vm })
        {
            if (int.TryParse(vm.SelectedPageIndexString, out int pageNumber))
            {
                if (pageNumber >= 1 && pageNumber <= vm.PageCount)
                {
                    vm.SelectedPageIndex = pageNumber;
                }
                else if (pageNumber < 1)
                {
                    vm.SelectedPageIndex = 1;
                }
                else
                {
                    vm.SelectedPageIndex = vm.PageCount;
                }
            }
            else
            {
                if (vm.SelectedPageIndex.HasValue)
                {
                    vm.SelectedPageIndexString = vm.SelectedPageIndex.Value.ToString("0");
                }
                else
                {
                    vm.SelectedPageIndexString = string.Empty;
                }
            }
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged ||
            sender is not PdfDocumentsTabsControl tabsControl ||
            e.NewSize.Width > e.PreviousSize.Width)
        {
            return;
        }

        if (GetSplitView()?.DataContext is not PdfDocumentViewModel vm || !vm.IsPaneOpen)
        {
            return;
        }
        
        if (tabsControl.Bounds.Width < vm.PaneSize * 2)
        {
            vm.IsPaneOpen = false;
        }
    }
}