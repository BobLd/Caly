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

using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Caly.Core.Utilities;
using Caly.Core.ViewModels;

namespace Caly.Core.Controls
{
    [TemplatePart("PART_ListBox", typeof(ListBox))]
    public class PdfDocumentThumbnailControl : TemplatedControl
    {
        private ListBox? _listBox;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _listBox = e.NameScope.FindFromNameScope<ListBox>("PART_ListBox");
            _listBox.ContainerPrepared += _listBox_ContainerPrepared; // TODO - Unsubscribe
            _listBox.ContainerClearing += _listBox_ContainerClearing;
        }

        private async void _listBox_ContainerClearing(object? sender, ContainerClearingEventArgs e)
        {
            if (e.Container.DataContext is PdfPageViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine($"_listBox_ContainerClearing: {vm.PageNumber}.");
                await vm.UnloadThumbnailCommand.ExecuteAsync(null);
                // Seems like there's a bug in ListBox when scrolling up/down via arrows. some visible containers are cleared (whereas they should not)
            }
        }

        private async void _listBox_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
        {
            if (e.Container.DataContext is PdfPageViewModel vm)
            {
                await vm.LoadThumbnailCommand.ExecuteAsync(null);
            }
        }
    }
}
