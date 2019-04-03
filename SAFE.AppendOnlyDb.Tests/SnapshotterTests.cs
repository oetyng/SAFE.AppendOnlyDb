using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAFE.AppendOnlyDb.Snapshots;
using SAFE.Data;
using SAFE.Data.Utils;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class SnapshotterTests : TestBase
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await Init((store) => new Snapshotter<int>(store, SnapshotFunc));
        }

        [TestMethod]
        public async Task Snapshotting_stores_and_restores_current_state()
        {
            // Arrange
            var store = _fixture.GetImdStore();
            var stream = await _fixture.GetStreamADAsync();

            // overflow MD once (1099 entries > 999), as to produce 1 snapshot
            var addCount = (int)Math.Round(1.1 * Constants.MdCapacity);

            // Act
            await AppendToStream(stream, addCount);  // adds a sequence from 0 to [addCount]

            // Assert
            var expectedState = ArithmeticSum(addCount - 1);
            var currentState = await GetCurrentState<int>(stream, store);
            Assert.AreEqual(expectedState, currentState);
        }

        [TestMethod]
        public async Task Snapshotting_aggregates_with_previous_snapshot()
        {
            // Arrange
            var store = _fixture.GetImdStore();
            var stream = await _fixture.GetStreamADAsync();

            // overflow MD twice (2198 entries > 2 * 999), as to produce 2 snapshots
            var addCount = (int)Math.Round(2.2 * Constants.MdCapacity);

            // Act
            await AppendToStream(stream, addCount); // adds a sequence from 0 to [addCount]

            // Assert
            var expectedState = ArithmeticSum(addCount - 1);
            var currentState = await GetCurrentState<int>(stream, store);
            Assert.AreEqual(expectedState, currentState);
        }

        int ArithmeticSum(int n) => n * (n + 1) / 2;

        async Task<T> GetCurrentState<T>(IStreamAD stream, Data.Client.IImDStore store)
        {
            var snapshotReading = await stream.ReadFromSnapshot();
            var snapshotData = await store.GetImDAsync(snapshotReading.Value.SnapshotMap);
            var snapshot = snapshotData.Parse<Snapshot>();
            var currentState = await SnapshotFunc(snapshot, snapshotReading.Value.NewEvents.Select(c => c.Item2.Parse<T>()));
            return currentState.GetState<T>();
        }

        async Task AppendToStream(IStreamAD stream, int addCount)
        {
            for (int i = 0; i < addCount; i++)
            {
                var theData = new StoredValue(i);
                var addResult = await stream.AppendAsync(theData).ConfigureAwait(false);

                Assert.IsNotNull(addResult);
                Assert.IsInstanceOfType(addResult, typeof(Result<Pointer>));
                Assert.IsTrue(addResult.HasValue);
            }
        }

        // Simply performs addition of integers
        async Task<Snapshot> SnapshotFunc<T>(Snapshot previousSnapshot, IAsyncEnumerable<T> changes)
        {
            int currentState = 0;
            if (previousSnapshot != null)
                currentState = previousSnapshot.GetState<int>();

            await foreach (var change in changes)
                currentState += Convert.ToInt32(change); // we know it will be int

            var snapshot = new Snapshot(currentState);

            return snapshot;
        }
    }
}