using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [TestMethod]
        public async Task GetRangeAsync_returns_expected_range()
        {
            // Arrange
            var db = await GetDatabase("theDb");
            var mdHead = await MdAccess.CreateAsync();
            IStreamAD head = new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));

            ulong indexStart = 54;
            ulong selectCount = 367;
            var added = Enumerable.Range(0, 500);
            foreach (var item in added)
                await head.AppendAsync(new StoredValue(item));

            // Act
            var range = await head.GetRangeAsync(indexStart, indexStart + selectCount - 1)
                .Select(c => c.Item1)
                .OrderBy(c => c)
                .ToListAsync();

            // Assert
            Assert.IsTrue(Enumerable.SequenceEqual(range, Utils.EnumerableExt.LongRange(indexStart, selectCount)));
        }

        [TestMethod]
        public async Task Append_is_not_threadsafe()
        {
            // Arrange
            var db = await GetDatabase("theDb");
            var mdHead = await MdAccess.CreateAsync();
            IStreamAD head = new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));

            ulong indexStart = 0;
            ulong selectCount = 20;
            var added = Enumerable.Range(0, 500);
            
            // Act
            await Task.WhenAll(added.Select(c => head.AppendAsync(new StoredValue(c))));
            var range = await head.GetRangeAsync(indexStart, indexStart + selectCount - 1)
                .Select(c => c.Item1)
                .OrderBy(c => c)
                .ToListAsync();

            // Assert
            Assert.IsFalse(Enumerable.SequenceEqual(range, Utils.EnumerableExt.LongRange(indexStart, selectCount)));
        }
    }
}