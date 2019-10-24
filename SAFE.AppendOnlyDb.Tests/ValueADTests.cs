using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAFE.Data;
using Index = SAFE.AppendOnlyDb.Network.Index;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class ValueADTests : TestBase
    {
        [TestInitialize]
        public async Task TestInitialize() => await Init();

        [TestMethod]
        public async Task GetValueAsync_returns_latest_value()
        {
            // Arrange
            var valueAD = await _fixture.GetValueADAsync();

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
        public async Task TrySetValueAsync_with_wrong_index_fails()
        {
            // Arrange
            var valueAD = await _fixture.GetValueADAsync();

            var firstValue = "firstValue";
            await valueAD.SetAsync(new StoredValue(firstValue));

            var expectedIndex = ExpectedIndex.Specific(0); // Wrong index, should be ExpectedIndex.Specific(1)

            // Act
            var lastValue = "lastValue";
            var addResult = await valueAD.TrySetAsync(new StoredValue(lastValue), expectedIndex);

            // Assert
            Assert.IsNotNull(addResult);
            Assert.IsFalse(addResult.HasValue);
            Assert.IsInstanceOfType(addResult, typeof(VersionMismatch<Index>));
        }

        [TestMethod]
        public async Task TrySetValueAsync_with_correct_index_succeeds()
        {
            // Arrange
            var valueAD = await _fixture.GetValueADAsync();

            var firstValue = "firstValue";
            await valueAD.SetAsync(new StoredValue(firstValue));

            var expectedIndex = ExpectedIndex.Specific(1);

            // Act
            var lastValue = "lastValue";
            var addResult = await valueAD.TrySetAsync(new StoredValue(lastValue), expectedIndex);

            // Assert
            Assert.IsNotNull(addResult);
            Assert.IsTrue(addResult.HasValue);
            Assert.IsNotInstanceOfType(addResult, typeof(VersionMismatch<Index>));
        }
    }
}