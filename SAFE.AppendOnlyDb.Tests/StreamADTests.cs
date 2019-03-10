using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class StreamADTests : TestBase
    {
        [TestInitialize]
        public async Task TestInitialize() => await InitClient();

        async Task<IStreamAD> GetStreamADAsync(string streamKey = "theStream")
        {
            var db = await GetDatabase("theDb");
            await db.AddStreamAsync(streamKey);
            return (await db.GetStreamAsync(streamKey)).Value;
        }

        [TestMethod]
        public async Task Read()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            var data = await stream.ReadForwardFromAsync(0).ToListAsync();

            // Assert
            Assert.IsNotNull(data);
            Assert.IsInstanceOfType(data, typeof(List<(ulong, StoredValue)>));
            Assert.AreEqual(0, data.Count);
        }

        // This test fails as the empty AsyncEnumerable
        // throws an indexoutofrange exception. To be solved.
        [TestMethod]
        public async Task ReadsEmptyFindResult()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            var data = stream.ReadForwardFromAsync(0)
                .Select(c => c.Item2);

            await foreach (var item in data)
                Console.WriteLine(item);

            // Assert
        }

        [TestMethod]
        public async Task AppendAsync_returns_pointer()
        {
            // Arrange
            var stream = await GetStreamADAsync();
            var theData = 42;

            // Act
            var addResult = await stream.AppendAsync(new StoredValue(theData)).ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(addResult);
            Assert.IsInstanceOfType(addResult, typeof(Result<Pointer>));
            Assert.IsTrue(addResult.HasValue);
        }

        [TestMethod]
        public async Task TryAppendAsync_with_wrong_version_fails()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            var firstValue = "firstValue";
            await stream.AppendAsync(new StoredValue(firstValue));

            var expectedVersion = ExpectedVersion.None; // Wrong version, should be ExpectedVersion.Specific(0)

            // Act
            var lastValue = "lastValue";
            var addResult = await stream.TryAppendAsync(new StoredValue(lastValue), expectedVersion);

            // Assert
            Assert.IsNotNull(addResult);
            Assert.IsFalse(addResult.HasValue);
            Assert.IsInstanceOfType(addResult, typeof(VersionMismatch<Pointer>));
        }

        [TestMethod]
        public async Task TryAppendAsync_with_correct_version_succeeds()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            var firstValue = "firstValue";
            await stream.AppendAsync(new StoredValue(firstValue));

            var expectedVersion = ExpectedVersion.Specific(0);

            // Act
            var lastValue = "lastValue";
            var addResult = await stream.TryAppendAsync(new StoredValue(lastValue), expectedVersion);

            // Assert
            Assert.IsNotNull(addResult);
            Assert.IsTrue(addResult.HasValue);
            Assert.IsNotInstanceOfType(addResult, typeof(VersionMismatch<Pointer>));
        }

        [TestMethod]
        public async Task Append_is_threadsafe()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            ulong indexStart = 0;
            ulong selectCount = 1997;
            var added = Enumerable.Range(0, (int)selectCount);
            
            // Act
            var results = await Task.WhenAll(added.Select(c => stream.AppendAsync(new StoredValue(c))));
            var errors = results.Where(c => !c.HasValue).ToList();
            var successes = results.Where(c => c.HasValue).ToList();

            Assert.AreEqual(0, errors.Count);
            Assert.AreEqual((int)selectCount, successes.Count);

            var range = await stream.GetRangeAsync(indexStart, indexStart + selectCount - 1)
                .Select(c => c.Item1)
                .OrderBy(c => c)
                .ToListAsync();

            // Assert
            Assert.IsTrue(Enumerable.SequenceEqual(range, Utils.EnumerableExt.LongRange(indexStart, selectCount)));
        }

        [TestMethod]
        public async Task GetAtVersionAsync_returns_correct_value()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            var firstValue = "firstValue";
            var middleValue = "middleValue"; // <= Expected value
            var lastValue = "lastValue";
            await stream.AppendAsync(new StoredValue(firstValue)); // version 0
            await stream.AppendAsync(new StoredValue(middleValue)); // version 1 <= Pick this one
            await stream.AppendAsync(new StoredValue(lastValue)); // version 2

            var middleVersion = ExpectedVersion.Specific(1);

            // Act
            var result = await stream.GetAtVersionAsync((ulong)middleVersion.Value);

            // Assert 2
            Assert.IsNotNull(result);
            Assert.IsTrue(result.HasValue);
            Assert.IsInstanceOfType(result, typeof(Result<StoredValue>));
            Assert.AreEqual(middleValue, result.Value.Parse<string>());
        }

        [TestMethod]
        public async Task GetRangeAsync_returns_expected_range()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            ulong indexStart = 54;
            ulong selectCount = 367;
            var added = Enumerable.Range(0, 500);
            foreach (var item in added)
                await stream.AppendAsync(new StoredValue(item));

            // Act
            var range = await stream.GetRangeAsync(indexStart, indexStart + selectCount - 1)
                .Select(c => c.Item1)
                .OrderBy(c => c)
                .ToListAsync();

            // Assert
            Assert.IsTrue(Enumerable.SequenceEqual(range, Utils.EnumerableExt.LongRange(indexStart, selectCount)));
        }

        [TestMethod]
        public async Task StreamDb_adds_more_than_md_capacity()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            var addCount = Math.Round(1.3 * Constants.MdCapacity);
            var sw = new Stopwatch();

            for (int i = 0; i < addCount; i++)
            {
                var theData = new StoredValue(i);

                // Act
                sw.Restart();
                var addResult = await stream.AppendAsync(theData).ConfigureAwait(false);
                sw.Stop();

                // Assert 1
                Assert.IsNotNull(addResult);
                Assert.IsInstanceOfType(addResult, typeof(Result<Pointer>));
                Assert.IsTrue(addResult.HasValue);
                Debug.WriteLine($"{i}: {sw.ElapsedMilliseconds}");
            }

            // Assert 2
            var events = await stream.ReadForwardFromAsync(0).ToListAsync();
            Assert.IsNotNull(events);
            Assert.AreEqual(addCount, events.Count);
        }

        [TestMethod]
        public async Task StreamDb_reads_ordered_forwards_and_backwards()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            var addCount = Math.Round(1.3 * Constants.MdCapacity);
            var sw = new Stopwatch();

            // Act
            await Task.WhenAll(Enumerable.Range(0, (int)addCount)
                .Select(c => stream.AppendAsync(new StoredValue(c)))).ConfigureAwait(false);

            // Assert 1: Forward
            var forwardEvents = await stream.ReadForwardFromAsync(0).ToListAsync();
            Assert.IsNotNull(forwardEvents);
            Assert.AreEqual(addCount, forwardEvents.Count);
            var fwdVersions = forwardEvents.Select(c => c.Item1);
            var fwdValues = forwardEvents.Select(c => c.Item2.Parse<int>());
            Assert.IsTrue(Enumerable.SequenceEqual(fwdVersions, Utils.EnumerableExt.LongRange(0, (ulong)addCount)));
            Assert.IsTrue(Enumerable.SequenceEqual(fwdValues, Enumerable.Range(0, (int)addCount)));

            // Assert 2: Backwards
            var backwardEvents = await stream.ReadBackwardsFromAsync((ulong)addCount - 1).ToListAsync();
            Assert.IsNotNull(backwardEvents);
            Assert.AreEqual(addCount, backwardEvents.Count);

            var bwdVersions = backwardEvents.Select(c => c.Item1).ToList();
            var bwdValues = backwardEvents.Select(c => c.Item2.Parse<int>()).ToList();
            var reverseInt32AddRange = Enumerable.Range(0, (int)addCount).Reverse().ToList(); // <- Reverse
            var reverseUInt64AddRange = reverseInt32AddRange.Select(c => (ulong)c).ToList();

            Assert.AreEqual(reverseInt32AddRange.Count, reverseUInt64AddRange.Count);
            Assert.IsTrue(Enumerable.SequenceEqual(bwdValues, reverseInt32AddRange));
            Assert.IsTrue(Enumerable.SequenceEqual(bwdVersions, reverseUInt64AddRange));
        }
    }
}