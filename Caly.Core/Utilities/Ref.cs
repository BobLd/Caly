/*
 * The MIT License (MIT)
 *
 * Copyright (c) .NET Foundation and Contributors All Rights Reserved
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
 * documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
 * persons to whom the Software is furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
 * Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
 * NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

// Code from Avalonia
// https://github.com/AvaloniaUI/Avalonia/blob/b15ee692da32dfa349c462c4c491a9cf652325b3/src/Avalonia.Base/Utilities/Ref.cs

using System;
using System.Runtime.ConstrainedExecution;
using System.Threading;

namespace Caly.Core.Utilities
{
    /// <summary>
    /// A ref-counted wrapper for a disposable object.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IRef<out T> : IDisposable where T : class
    {
        /// <summary>
        /// The item that is being ref-counted.
        /// </summary>
        T Item { get; }

        /// <summary>
        /// Create another reference to this object and increment the refcount.
        /// </summary>
        /// <returns>A new reference to this object.</returns>
        IRef<T> Clone();

        /// <summary>
        /// Create another reference to the same object, but cast the object to a different type.
        /// </summary>
        /// <typeparam name="TResult">The type of the new reference.</typeparam>
        /// <returns>A reference to the value as the new type but sharing the refcount.</returns>
        IRef<TResult> CloneAs<TResult>() where TResult : class;

        /// <summary>
        /// The current refcount of the object tracked in this reference. For debugging/unit test use only.
        /// </summary>
        int RefCount { get; }
    }

    internal static class RefCountable
    {
        /// <summary>
        /// Create a reference counted object wrapping the given item.
        /// </summary>
        /// <typeparam name="T">The type of item.</typeparam>
        /// <param name="item">The item to refcount.</param>
        /// <returns>The refcounted reference to the item.</returns>
        public static IRef<T> Create<T>(T item) where T : class, IDisposable
        {
            return new Ref<T>(item, new RefCounter(item));
        }

        private sealed class RefCounter
        {
            private IDisposable? _item;
            private volatile int _refs;

            public RefCounter(IDisposable item)
            {
                _item = item;
                _refs = 1;
            }

            public void AddRef()
            {
                var old = _refs;
                while (true)
                {
                    if (old == 0)
                    {
                        throw new ObjectDisposedException("Cannot add a reference to a nonreferenced item");
                    }
                    var current = Interlocked.CompareExchange(ref _refs, old + 1, old);
                    if (current == old)
                    {
                        break;
                    }
                    old = current;
                }
            }

            public void Release()
            {
                var old = _refs;
                while (true)
                {
                    var current = Interlocked.CompareExchange(ref _refs, old - 1, old);

                    if (current == old)
                    {
                        if (old == 1)
                        {
                            _item?.Dispose();
                            _item = null;
                        }
                        break;
                    }
                    old = current;
                }
            }

            internal int RefCount => _refs;
        }

        private sealed class Ref<T> : CriticalFinalizerObject, IRef<T> where T : class
        {
            private T? _item;
            private readonly RefCounter _counter;
            private readonly object _lock = new object();

            public Ref(T item, RefCounter counter)
            {
                _item = item;
                _counter = counter;
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_item != null)
                    {
                        _counter.Release();
                        _item = null;
                    }
                    GC.SuppressFinalize(this);
                }
            }

            ~Ref()
            {
                Dispose();
                System.Diagnostics.Debug.Assert(Item is null || RefCount == 0);
            }

            public T Item
            {
                get
                {
                    lock (_lock)
                    {
                        return _item!;
                    }
                }
            }

            public IRef<T> Clone()
            {
                lock (_lock)
                {
                    if (_item != null)
                    {
                        var newRef = new Ref<T>(_item, _counter);
                        _counter.AddRef();
                        return newRef;
                    }
                    throw new ObjectDisposedException("Ref<" + typeof(T) + ">");
                }
            }

            public IRef<TResult> CloneAs<TResult>() where TResult : class
            {
                lock (_lock)
                {
                    if (_item != null)
                    {
                        var castRef = new Ref<TResult>((TResult)(object)_item, _counter);
                        Interlocked.MemoryBarrier();
                        _counter.AddRef();
                        return castRef;
                    }
                    throw new ObjectDisposedException("Ref<" + typeof(T) + ">");
                }
            }

            public int RefCount => _counter.RefCount;
        }
    }
}
