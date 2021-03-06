﻿using Newtonsoft.Json;
using SAFE.Data;
using SAFE.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Snapshots
{
    public class SnapshotReading
    {
        /// <summary>
        /// The data map to an immutable data
        /// with the serialized Snapshot instance.
        /// </summary>
        public byte[] SnapshotMap { get; set; }
        
        /// <summary>
        /// All new events since the snapshot was taken.
        /// </summary>
        public IAsyncEnumerable<(ulong, StoredValue)> NewEvents { get; set; }
    }

    public class Snapshot
    {
        [JsonConstructor]
        Snapshot() { }

        public Snapshot(object data)
        {
            Payload = data.GetBytes();
            AssemblyQualifiedName = data.GetType().AssemblyQualifiedName;
        }

        public byte[] Payload { get; set; }
        public string AssemblyQualifiedName { get; set; }

        public static Snapshot Get(byte[] data) => data.Parse<Snapshot>();

        public byte[] Serialize() => this.GetBytes();
        

        public TState GetState<TState>()
            => (TState)Encoding.UTF8.GetString(Payload).Parse(AssemblyQualifiedName);
    }

    public abstract class Snapshotter
    {
        internal abstract Task<Result<byte[]>> StoreSnapshot(IMdNode node);
    }

    class EmptySnapshotter : Snapshotter
    {
        internal override Task<Result<byte[]>> StoreSnapshot(IMdNode node)
            => Task.FromResult(Result.OK(new byte[0]));
    }

    public class Snapshotter<T> : Snapshotter
    {
        readonly Data.Client.IImDStore _store;
        readonly Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> _leftFold;

        /// <summary>
        /// For snapshotting the IMdNode entries.
        /// </summary>
        /// <typeparam name="T">Type of the data to aggregate.</typeparam>
        /// <param name="store">Immutable data store.</param>
        /// <param name="leftFold">The aggregating function.</param>
        public Snapshotter(Data.Client.IImDStore store, Func<Snapshot, IAsyncEnumerable<T>, Task<Snapshot>> leftFold)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _leftFold = leftFold ?? throw new ArgumentNullException(nameof(leftFold)); ;
        }

        /// <summary>
        /// Stores a snapshot of all entries.
        /// </summary>
        /// <returns>Data map (pointer) to immutable data with serialized snapshot.</returns>
        internal override async Task<Result<byte[]>> StoreSnapshot(IMdNode node)
        {
            if (!node.IsFull) // we've settled for this invariant: only snapshotting full Mds
                return new InvalidOperation<byte[]>("Cannot snapshot unless Md is full!");

            var entries = node.FindRangeAsync(node.StartIndex, node.EndIndex);
            var ordered = entries
                .OrderBy(c => c.Item1)
                .Select(c => c.Item2.Parse<T>());

            Snapshot previous = default;
            if (node.Snapshot != null)
                previous = await GetSnapshotAsync(node.Snapshot);

            var snapshot = await _leftFold(previous, ordered);
            var pointer = await _store.StoreImDAsync(snapshot.Serialize());
            return Result.OK(pointer);
        }

        public async Task<Snapshot> GetSnapshotAsync(byte[] pointer)
        {
            var bytes = await _store.GetImDAsync(pointer);
            var snapshot = bytes.Parse<Snapshot>();
            return snapshot;
        }
    }
}