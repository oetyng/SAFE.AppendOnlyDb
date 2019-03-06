using System;
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
            //var mdHead = await MdAccess.CreateAsync();
            //return new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));
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
        public async Task GetAtVersionAsync_returns_stored_value()
        {
            // Arrange
            var stream = await GetStreamADAsync();
            var theData = "theData";
            _ = await stream.AppendAsync(new StoredValue(theData)).ConfigureAwait(false);

            // Act
            var findResult = await stream.GetAtVersionAsync(0).ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(findResult);
            Assert.IsInstanceOfType(findResult, typeof(Result<StoredValue>));
            Assert.IsTrue(findResult.HasValue);
            Assert.AreEqual(theData, findResult.Value.Parse<string>());
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
        public async Task Append_is_not_threadsafe()
        {
            // Arrange
            var stream = await GetStreamADAsync();

            ulong indexStart = 0;
            ulong selectCount = 20;
            var added = Enumerable.Range(0, 500);
            
            // Act
            await Task.WhenAll(added.Select(c => stream.AppendAsync(new StoredValue(c))));
            var range = await stream.GetRangeAsync(indexStart, indexStart + selectCount - 1)
                .Select(c => c.Item1)
                .OrderBy(c => c)
                .ToListAsync();

            // Assert
            Assert.IsFalse(Enumerable.SequenceEqual(range, Utils.EnumerableExt.LongRange(indexStart, selectCount)));
        }
    }
}