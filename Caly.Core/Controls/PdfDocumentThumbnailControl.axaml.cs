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
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_ListBox", typeof(ListBox))]
    public sealed class PdfDocumentThumbnailControl : TemplatedControl
    {
        private ListBox? _listBox;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _listBox = e.NameScope.FindFromNameScope<ListBox>("PART_ListBox");
            _listBox.ContainerPrepared += _listBox_ContainerPrepared;
            _listBox.ContainerClearing += _listBox_ContainerClearing;
        }

        private void _listBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataContextProperty && e.OldValue is PdfDocumentViewModel oldVm)
            {
                oldVm.ClearAllThumbnails();
            }
        }
        
        private void _listBox_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (e.Container is not ListBoxItem container || e.Container.DataContext is not PdfPageViewModel vm)
            {
                return;
            }

            container.PropertyChanged += Container_PropertyChanged;
            vm.LoadThumbnail();
        }

        private void Container_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ContentPresenter.ContentProperty && e.OldValue is PdfPageViewModel oldVm)
            {
                if (_listBox?.ItemsPanelRoot is VirtualizingStackPanel panel)
                {
                    int startPage = panel.FirstRealizedIndex + 1;
                    int endPage = panel.LastRealizedIndex + 1;

                    if (startPage == 0 || endPage == 0 || oldVm.PageNumber <= startPage || oldVm.PageNumber >= endPage)
                    {
                        oldVm.UnloadThumbnail();
                    }
                }
            }
        }

        private void _listBox_ContainerClearing(object? sender, ContainerClearingEventArgs e)
        {
            if (e.Container is not ListBoxItem container)
            {
                return;
            }

            container.PropertyChanged -= Container_PropertyChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            if (_listBox is not null)
            {
                _listBox.ContainerPrepared -= _listBox_ContainerPrepared;
                _listBox.ContainerClearing -= _listBox_ContainerClearing;
            }
        }
    }
}
