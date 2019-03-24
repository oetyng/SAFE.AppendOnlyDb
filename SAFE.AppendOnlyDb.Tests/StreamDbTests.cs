using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class StreamDbTests : TestBase
    {
        [TestInitialize]
        public async Task TestInitialize() => await Init();

        [TestMethod]
        public async Task Db_is_created()
        {
            // Arrange + Act
            var db = await _fixture.GetDatabase("theDb");

            // Assert
            Assert.IsNotNull(db);
            Assert.IsInstanceOfType(db, typeof(StreamDb));
        }

        [TestMethod]
        public async Task AddStreamAsync_adds_stream()
        {
            // Arrange
            var db = await _fixture.GetDatabase("theDb");

            // Act
            var result_1 = await db.AddStreamAsync("theStream");
            var result_2 = await db.GetStreamAsync("theStream");

            // Assert
            Assert.IsNotNull(result_1);
            Assert.IsInstanceOfType(result_1, typeof(Result<bool>));
            Assert.IsTrue(result_1.HasValue);

            Assert.IsNotNull(result_2);
            Assert.IsInstanceOfType(result_2, typeof(Result<IStreamAD>));
            Assert.IsTrue(result_2.HasValue);
        }

        [TestMethod]
        public async Task GetOrAddStreamAsync_gets_stream_when_exists()
        {
            // Arrange
            var db = await _fixture.GetDatabase("theDb");

            // Act
            var result_1 = await db.AddStreamAsync("theStream");
            var result_2 = await db.GetOrAddStreamAsync("theStream");

            // Assert
            Assert.IsNotNull(result_1);
            Assert.IsInstanceOfType(result_1, typeof(Result<bool>));
            Assert.IsTrue(result_1.HasValue);

            Assert.IsNotNull(result_2);
            Assert.IsInstanceOfType(result_2, typeof(Result<IStreamAD>));
            Assert.IsTrue(result_2.HasValue);
        }

        [TestMethod]
        public async Task GetOrAddStreamAsync_adds_stream_when_not_exists()
        {
            // Arrange
            var db = await _fixture.GetDatabase("theDb");

            // Act
            var result_2 = await db.GetOrAddStreamAsync("theStream");

            // Assert
            Assert.IsNotNull(result_2);
            Assert.IsInstanceOfType(result_2, typeof(Result<IStreamAD>));
            Assert.IsTrue(result_2.HasValue);
        }

        [TestMethod]
        public async Task GetStreamAsync_returns_KeyNotFound_when_stream_does_not_exist()
        {
            // Arrange
            var db = await _fixture.GetDatabase("theDb");

            // Act
            var result = await db.GetStreamAsync("theStream");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(KeyNotFound<IStreamAD>));
            Assert.IsFalse(result.HasValue);
        }

        [TestMethod]
        public async Task Stream_is_added_once_only()
        {
            // Arrange
            var db = await _fixture.GetDatabase("theDb");
            var result_1 = await db.AddStreamAsync("theStream");

            // Act
            var result_2 = await db.AddStreamAsync("theStream");

            // Assert
            Assert.IsNotNull(result_1);
            Assert.IsInstanceOfType(result_1, typeof(Result<bool>));
            Assert.IsTrue(result_1.HasValue);
            Assert.IsTrue(result_1.Value); // <- Added: true

            Assert.IsNotNull(result_2);
            Assert.IsInstanceOfType(result_2, typeof(Result<bool>));
            Assert.IsTrue(result_2.HasValue);
            Assert.IsFalse(result_2.Value); // <- Added: false
        }
    }
}