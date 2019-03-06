using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SAFE.Data;

namespace SAFE.AppendOnlyDb.Tests
{
    [TestClass]
    public class StreamDbTests : TestBase
    {
        [TestInitialize]
        public async Task TestInitialize() => await InitClient();

        [TestMethod]
        public async Task Db_is_created()
        {
            // Arrange + Act
            var db = await GetDatabase("theDb");

            // Assert
            Assert.IsNotNull(db);
            Assert.IsInstanceOfType(db, typeof(StreamDb));
        }

        [TestMethod]
        public async Task Stream_is_added()
        {
            // Arrange
            var db = await GetDatabase("theDb");

            // Act
            var result = await db.AddStreamAsync("theStream");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Result<bool>));
            Assert.IsTrue(result.HasValue);
        }

        [TestMethod]
        public async Task Stream_is_added_once_only()
        {
            // Arrange
            var db = await GetDatabase("theDb");
            var result_1 = await db.AddStreamAsync("theStream");

            // Act
            var result_2 = await db.AddStreamAsync("theStream");

            // Assert
            Assert.IsNotNull(result_1);
            Assert.IsInstanceOfType(result_1, typeof(Result<bool>));
            Assert.IsTrue(result_1.HasValue);
            Assert.IsTrue(result_1.Value);

            Assert.IsNotNull(result_2);
            Assert.IsInstanceOfType(result_2, typeof(Result<bool>));
            Assert.IsTrue(result_2.HasValue);
            Assert.IsFalse(result_2.Value);
        }
    }
}