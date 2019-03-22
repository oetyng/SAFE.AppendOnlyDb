using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class MutableCollectionTests : TestBase
    {
        [TestInitialize]
        public async Task TestInitialize() => await InitClient();

        async Task<IValueAD> GetValueADAsync()
        {
            var db = await GetDatabase("theDb");
            var mdHead = await MdAccess.CreateAsync();
            return new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));
        }

        [TestMethod]
        public async Task Created_collection_is_empty()
        {
            // Arrange
            var collection = new MutableCollection<int>(await GetValueADAsync());

            // Act
            var empty = await collection.GetAsync()
                .ToListAsync();
            
            // Assert
            Assert.IsNotNull(empty);
            Assert.IsInstanceOfType(empty, typeof(List<int>));
            Assert.AreEqual(0, empty.Count);
        }

        [TestMethod]
        public async Task GetAsync_returns_added_values()
        {
            // Arrange
            var collection = new MutableCollection<int>(await GetValueADAsync());

            await collection.AddAsync(0);
            await collection.AddAsync(1);
            await collection.AddAsync(2);

            // Act
            var values = await collection.GetAsync()
                .ToListAsync();

            // Assert
            Assert.IsNotNull(values);
            Assert.IsInstanceOfType(values, typeof(List<int>));
            Assert.AreEqual(3, values.Count);
            Assert.IsTrue(values.Contains(0));
            Assert.IsTrue(values.Contains(1));
            Assert.IsTrue(values.Contains(2));
        }

        [TestMethod]
        public async Task SetAsync_replaces_values()
        {
            // Arrange
            var collection = new MutableCollection<int>(await GetValueADAsync());

            await collection.AddAsync(0);
            await collection.AddAsync(1);
            await collection.AddAsync(2);

            // Act
            var newValues = collection.GetAsync()
                .Where(c => c > 1)
                .Prepend(99);

            await collection.SetAsync(newValues);

            var current = await collection.GetAsync()
                .ToListAsync();

            // Assert
            Assert.IsNotNull(current);
            Assert.IsInstanceOfType(current, typeof(List<int>));
            Assert.AreEqual(2, current.Count);
            Assert.IsTrue(current.Contains(99));
            Assert.IsTrue(current.Contains(2));
        }
    }
}