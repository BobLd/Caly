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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Caly.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Caly.Core.Views
{
    public partial class MainView : UserControl
    {
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
    }
}
