using System;
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
        public async Task TestInitialize()
        {
            await InitClient();
        }

        async Task<IStreamAD> GetStreamADAsync()
        {
            var db = await GetDatabase("theDb");
            var mdHead = await MdAccess.CreateAsync();
            return new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));
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

            // Act 2
            var lastValue = "lastValue";
            var addResult = await stream.TryAppendAsync(new StoredValue(lastValue), expectedVersion);

            // Assert 2
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

            // Act 2
            var lastValue = "lastValue";
            var addResult = await stream.TryAppendAsync(new StoredValue(lastValue), expectedVersion);

            // Assert 2
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