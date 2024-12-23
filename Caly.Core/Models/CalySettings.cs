﻿// Copyright (C) 2024 BobLd
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

namespace Caly.Core.Models
{
    public sealed class CalySettings
    {
        public static readonly CalySettings Default = new CalySettings()
        {
            Width = 1000,
            Height = 500,
            PaneSize = 350
        };

        // TODO - Add version for compatibility checks

        public int Width { get; set; }

        public int Height { get; set; }

        public int PaneSize { get; set; }

        public enum CalySettingsProperty
        {
            PaneSize = 0
        }
    }
}
