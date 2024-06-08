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
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;

namespace Caly.Core.Views
{
    public partial class MainWindow : Window
    {
        public WindowNotificationManager? NotificationManager { get; set; }

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            NotificationManager = new WindowNotificationManager(this)
            {
                Position = NotificationPosition.BottomRight,
#if DEBUG
                MaxItems = 50
#else
                MaxItems = 5
#endif
            };

            base.OnLoaded(e);
        }
    }
}
