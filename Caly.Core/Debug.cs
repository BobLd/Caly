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
using System.Diagnostics;
using System.IO;
using Avalonia;

namespace Caly.Core
{
    public static class Debug
    {
        [Conditional("DEBUG")]
        public static void ThrowOnUiThread()
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                throw new InvalidOperationException("Call from UI thread");
            }
        }

        [Conditional("DEBUG")]
        public static void ThrowNotOnUiThread()
        {
            if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                throw new InvalidOperationException("Call from non-UI thread");
            }
        }

        [Conditional("DEBUG")]
        public static void WriteExceptionToFile(Exception? exception)
        {
            if (exception is null)
            {
                File.WriteAllText($"error_caly_{Guid.NewGuid()}.txt", "Received null exception");
                return;
            }

            File.WriteAllText($"error_caly_{Guid.NewGuid()}.txt", exception.ToString());
        }

        /// <summary>
        /// Assert if the matrix is only a scale matrix (or null) and scale X equals scale Y.
        /// </summary>
        [Conditional("DEBUG")]
        public static void AssertIsNullOrScale(Matrix? matrix)
        {
            if (!matrix.HasValue) return;
            System.Diagnostics.Debug.Assert(!matrix.Value.ContainsPerspective());
            System.Diagnostics.Debug.Assert(matrix.Value.M12.Equals(0));
            System.Diagnostics.Debug.Assert(matrix.Value.M21.Equals(0));
            System.Diagnostics.Debug.Assert(matrix.Value.M21.Equals(0));
            System.Diagnostics.Debug.Assert(matrix.Value.M32.Equals(0));

            System.Diagnostics.Debug.Assert(matrix.Value.M11.Equals(matrix.Value.M22));
        }
    }
}
