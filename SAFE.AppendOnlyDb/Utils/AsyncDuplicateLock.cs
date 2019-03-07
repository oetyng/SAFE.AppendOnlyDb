﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Utils
{
    /// <summary>
    /// http://stackoverflow.com/questions/31138179/asynchronous-locking-based-on-a-key
    /// </summary>
    public sealed class AsyncDuplicateLock
    {
        private sealed class RefCounted<T>
        {
            public RefCounted(T value)
            {
                RefCount = 1;
                _value = value;
            }

            public int RefCount { get; set; }
            private readonly T _value;
            public T Value => _value;
        }

        private static readonly Dictionary<object, RefCounted<SemaphoreSlim>> SemaphoreSlims
                              = new Dictionary<object, RefCounted<SemaphoreSlim>>();

        private SemaphoreSlim GetOrCreate(object key)
        {
            RefCounted<SemaphoreSlim> item;
            lock (SemaphoreSlims)
            {
                if (SemaphoreSlims.TryGetValue(key, out item))
                {
                    ++item.RefCount;
                }
                else
                {
                    item = new RefCounted<SemaphoreSlim>(new SemaphoreSlim(1, 1));
                    SemaphoreSlims[key] = item;
                }
            }
            return item.Value;
        }

        public IDisposable Lock(object key)
        {
            GetOrCreate(key).Wait();
            return new Releaser { Key = key };
        }

        public async Task<IDisposable> LockAsync(object key)
        {
            await GetOrCreate(key).WaitAsync().ConfigureAwait(false);
            return new Releaser { Key = key };
        }

        private sealed class Releaser : IDisposable
        {
            public object Key { get; set; }

            public void Dispose()
            {
                RefCounted<SemaphoreSlim> item;
                lock (SemaphoreSlims)
                {
                    item = SemaphoreSlims[Key];
                    --item.RefCount;
                    if (item.RefCount == 0)
                        SemaphoreSlims.Remove(Key);
                }
                item.Value.Release();
            }
        }
    }
}