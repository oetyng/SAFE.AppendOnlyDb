using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAFE.AppendOnlyDb.Network;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data;
using SAFE.Data.Utils;
using Index = SAFE.AppendOnlyDb.Network.Index;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class SnapshotterTests : TestBase
    {

        const int Interval = 1000;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await Init((store) => new Snapshotter<ulong>(1000, store, SnapshotFunc));
        }

        [TestMethod]
        public void IsSnapshotIndex()
        {
            Assert.IsTrue(IsSnapshotIndex(1000));
            Assert.IsTrue(IsSnapshotIndex(2001));
            Assert.IsTrue(IsSnapshotIndex(3002));
            Assert.IsTrue(IsSnapshotIndex(4003));
            Assert.IsTrue(IsSnapshotIndex(5004));
            Assert.IsTrue(IsSnapshotIndex(6005));
            Assert.IsTrue(IsSnapshotIndex(7006));
            Assert.IsTrue(IsSnapshotIndex(8007));
        }

        bool IsSnapshotIndex(int index)
        {
            if (index == Interval) return true;
            var hum = index / Interval;
            var rest = index % Interval;
            if (hum - rest == 1) return true;
            return false;
        }

        // overflow once (1099 entries > 1000), as to produce 1 snapshot
        [TestMethod]
        public async Task Snapshotting_stores_and_restores_current_state()
            => await Snapshotting_aggregates_with_previous_snapshot(factor: 1.1);

        // overflow twice (2200 entries > 2 * 1000), as to produce 2 snapshots
        [TestMethod]
        public async Task Snapshotting_aggregates_with_previous_snapshot()
            => await Snapshotting_aggregates_with_previous_snapshot(factor: 2.2);

        [TestMethod]
        public async Task Snapshotting_aggregates_with_many_previous_snapshot()
            => await Snapshotting_aggregates_with_previous_snapshot(factor: 234);

        async Task Snapshotting_aggregates_with_previous_snapshot(double factor)
        {
            // Arrange
            var store = _fixture.GetImdStore();
            var stream = await _fixture.GetStreamADAsync();

            // overflow [x = Round(factor)] times, as to produce x snapshots
            var addCount = (ulong)Math.Round(factor * Interval);

            // Act
            await AppendToStream(stream, addCount); // adds a sequence from 0 to [addCount]

            // Assert
            var expectedState = ArithmeticSum(addCount - 1);
            var currentState = await GetCurrentState<ulong>(stream, store);
            Assert.AreEqual(expectedState, currentState);
        }

        ulong ArithmeticSum(ulong n) => n * (n + 1) / 2;

        async Task<T> GetCurrentState<T>(IStreamAD stream, Data.Client.IImDStore store)
        {
            var snapshotReading = (await stream.GetSnapshotReading()).Value;
            var snapshotData = await store.GetImDAsync(snapshotReading.SnapshotPointer.Pointer);
            var snapshot = snapshotData.Parse<Snapshot>();
            var currentState = await SnapshotFunc(snapshot, snapshotReading.NewEvents.Select(c => c.Item2.Parse<T>()));
            return currentState.GetState<T>();
        }

        async Task AppendToStream(IStreamAD stream, ulong addCount)
        {
            for (ulong i = 0; i < addCount; i++)
            {
                var theData = new StoredValue(i);
                var addResult = await stream.AppendAsync(theData).ConfigureAwait(false);

                Assert.IsNotNull(addResult);
                Assert.IsInstanceOfType(addResult, typeof(Result<Index>));
                Assert.IsTrue(addResult.HasValue);
            }
        }

        // Simply performs addition of integers
        async Task<Snapshot> SnapshotFunc<T>(Snapshot previousSnapshot, IAsyncEnumerable<T> changes)
        {
            ulong currentState = 0;
            if (previousSnapshot != null)
                currentState = previousSnapshot.GetState<ulong>();

            await foreach (var change in changes)
                currentState += Convert.ToUInt64(change); // we know it will be ulong

            var snapshot = new Snapshot(currentState);
            return snapshot;
        }
    }
}