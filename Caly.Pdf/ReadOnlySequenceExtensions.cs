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

using System.Buffers;

namespace Caly.Pdf
{
    internal static class ReadOnlySequenceExtensions
    {
        public static bool IsEmptyOrWhiteSpace(this ReadOnlySequence<char> sequence)
        {
            if (sequence.IsEmpty)
            {
                return true;
            }

            if (sequence.IsSingleSegment)
            {
                return sequence.FirstSpan.IsWhiteSpace();
            }

            Span<char> output = sequence.Length < 512 ? stackalloc char[(int)sequence.Length] : new char[sequence.Length];

            sequence.CopyTo(output);

            return MemoryExtensions.IsWhiteSpace(output);
        }
    }
}
