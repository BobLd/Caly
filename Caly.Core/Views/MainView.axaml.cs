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
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Caly.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DropEvent, Drop);
    }

    private async void Drop(object? sender, DragEventArgs e)
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

            var pdfDocumentsService = App.Current?.Services?.GetRequiredService<IPdfDocumentsService>();
            if (pdfDocumentsService is null)
            {
                throw new NullReferenceException($"Missing {nameof(IPdfDocumentsService)} instance.");
            }

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

            foreach (var textBlock in stackPanel.GetVisualDescendants().OfType<TextBlock>())
            {
                textBlock.InvalidateMeasure();
            }
        }
    }
}
