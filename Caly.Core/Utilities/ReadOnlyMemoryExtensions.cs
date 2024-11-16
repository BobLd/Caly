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
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Caly.Core.Utilities
{
    internal static class ReadOnlyMemoryExtensions
    {
        private const char Padding = '\0';
        private const char Space = ' ';

        public static void AppendClean(this StringBuilder sb, ReadOnlyMemory<char> memory)
        {
            Span<char> output = memory.Length < 512 ?
                stackalloc char[(int)memory.Length] :
                new char[memory.Length];

            memory.Span.CopyTo(output);

            // Padding chars are problematic in string builder, we remove them
            for (int i = 0; i < output.Length; ++i)
            {
                if (output[i] == Padding)
                {
                    output[i] = Space;
                }
            }

            if (!output.IsEmpty && !MemoryExtensions.IsWhiteSpace(output))
            {
                sb.Append(output);
            }
        }

        public static string GetString(this ReadOnlySequence<char> sequence)
        {
            return string.Create((int)sequence.Length, sequence, (chars, state) =>
            {
                // https://www.stevejgordon.co.uk/creating-strings-with-no-allocation-overhead-using-string-create-csharp
                state.CopyTo(chars);
            });
        }

        public static string GetString(this ReadOnlyMemory<char> memory)
        {
            if (MemoryMarshal.TryGetString(memory, out string? str, out _, out _))
            {
                return str;
            }

            return string.Empty;
        }
    }
}
