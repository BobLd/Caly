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
using Avalonia;
using Avalonia.Input;

namespace Caly.Core.Utilities
{
    internal static class CalyHotkeyConfiguration
    {
        private static readonly KeyModifiers _commandModifiers;

        static CalyHotkeyConfiguration()
        {
            if (Application.Current?.PlatformSettings is null)
            {
                throw new NullReferenceException("PlatformSettings is null.");
            }

            _commandModifiers = Application.Current.PlatformSettings.HotkeyConfiguration.CommandModifiers;
        }

        /// <summary>
        /// Gets a platform-specific <see cref="KeyGesture"/> for the Copy action
        /// </summary>
        public static KeyGesture? CopyGesture => Application.Current?.PlatformSettings?.HotkeyConfiguration.Copy.FirstOrDefault();

        public static KeyGesture OpenFileGesture => new KeyGesture(Key.O, _commandModifiers);

        public static KeyGesture DocumentSearchGesture => new KeyGesture(Key.F, _commandModifiers);

        public static KeyGesture DocumentCloseGesture => new KeyGesture(Key.F4, _commandModifiers);

        public static KeyGesture DocumentNextGesture => new KeyGesture(Key.PageDown, _commandModifiers);

        public static KeyGesture DocumentPreviousGesture => new KeyGesture(Key.PageUp, _commandModifiers);

        // TODO

        // See:
        // - MainView.axaml
        // - PdfPageItem.axaml
        // - PdfPageItemsControl.axaml
    }
}
