using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class ValueADTests : TestBase
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await InitClient();
        }

        async Task<IValueAD> GetValueADAsync()
        {
            var db = await GetDatabase("theDb");
            var mdHead = await MdAccess.CreateAsync();
            return new DataTree(mdHead, (s) => throw new ArgumentOutOfRangeException("Can only add 1k items to this collection."));
        }

        [TestMethod]
        public async Task GetValueAsync_returns_latest_value()
        {
            // Arrange
            var valueAD = await GetValueADAsync();

            var firstValue = "firstValue";
            await valueAD.SetAsync(new StoredValue(firstValue));

            // Act 1
            var currentValue = await valueAD.GetValueAsync();

            // Assert 1
            Assert.IsNotNull(currentValue);
            Assert.IsInstanceOfType(currentValue, typeof(Result<StoredValue>));
            Assert.IsTrue(currentValue.HasValue);
            Assert.AreEqual(firstValue, currentValue.Value.Parse<string>());

            // Act 2
            var lastValue = "lastValue";
            await valueAD.SetAsync(new StoredValue(lastValue));
            currentValue = await valueAD.GetValueAsync();

            // Assert 2
            Assert.IsNotNull(currentValue);
            Assert.IsInstanceOfType(currentValue, typeof(Result<StoredValue>));
            Assert.IsTrue(currentValue.HasValue);
            Assert.AreEqual(lastValue, currentValue.Value.Parse<string>());
        }

        [TestMethod]
        public async Task TrySetValueAsync_with_wrong_version_fails()
        {
            // Arrange
            var valueAD = await GetValueADAsync();

            var firstValue = "firstValue";
            await valueAD.SetAsync(new StoredValue(firstValue));

            var expectedVersion = ExpectedVersion.None; // Wrong version, should be ExpectedVersion.Specific(0)

            // Act 2
            var lastValue = "lastValue";
            var addResult = await valueAD.TrySetAsync(new StoredValue(lastValue), expectedVersion);

            // Assert 2
            Assert.IsNotNull(addResult);
            Assert.IsFalse(addResult.HasValue);
            Assert.IsInstanceOfType(addResult, typeof(VersionMismatch<Pointer>));
        }

        [TestMethod]
        public async Task TrySetValueAsync_with_correct_version_succeeds()
        {
            // Arrange
            var valueAD = await GetValueADAsync();

            var firstValue = "firstValue";
            await valueAD.SetAsync(new StoredValue(firstValue));

            var expectedVersion = ExpectedVersion.Specific(0);

            // Act 2
            var lastValue = "lastValue";
            var addResult = await valueAD.TrySetAsync(new StoredValue(lastValue), expectedVersion);

            // Assert 2
            Assert.IsNotNull(addResult);
            Assert.IsTrue(addResult.HasValue);
            Assert.IsNotInstanceOfType(addResult, typeof(VersionMismatch<Pointer>));
        }
    }
}