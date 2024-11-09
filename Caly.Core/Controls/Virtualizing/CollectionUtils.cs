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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;

namespace Caly.Core.Controls.Virtualizing
{
    internal static class CollectionUtils
    {
        public static NotifyCollectionChangedEventArgs ResetEventArgs { get; } = new(NotifyCollectionChangedAction.Reset);

        public static void InsertMany<T>(this List<T> list, int index, T item, int count)
        {
            var repeat = FastRepeat<T>.Instance;
            repeat.Count = count;
            repeat.Item = item;
            list.InsertRange(index, FastRepeat<T>.Instance);
            repeat.Item = default;
        }

        private class FastRepeat<T> : ICollection<T>
        {
            public static readonly FastRepeat<T> Instance = new();
            public int Count { get; set; }
            public bool IsReadOnly => true;
            [AllowNull] public T Item { get; set; }
            public void Add(T item) => throw new NotImplementedException();
            public void Clear() => throw new NotImplementedException();
            public bool Contains(T item) => throw new NotImplementedException();
            public bool Remove(T item) => throw new NotImplementedException();
            IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
            public IEnumerator<T> GetEnumerator() => throw new NotImplementedException();

            public void CopyTo(T[] array, int arrayIndex)
            {
                var end = arrayIndex + Count;

                for (var i = arrayIndex; i < end; ++i)
                {
                    array[i] = Item;
                }
            }
        }
    }
}
