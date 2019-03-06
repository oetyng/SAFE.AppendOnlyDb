using System;
using System.Diagnostics;
using System.Linq;
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
            // Act
            var db = await GetDatabase("theDb");

            // Assert
            Assert.IsNotNull(db);
            Assert.IsInstanceOfType(db, typeof(StreamDb));
        }

        [TestMethod]
        public async Task AppendAsync_returns_pointer()
        {
            // Arrange
            var db = await GetDatabase("theDb");
            var theData = 42;

            // Act
            var addResult = await db.AppendAsync("theStream", theData).ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(addResult);
            Assert.IsInstanceOfType(addResult, typeof(Result<Pointer>));
            Assert.IsTrue(addResult.HasValue);
        }

        [TestMethod]
        public async Task GetAtVersionAsync_returns_stored_value()
        {
            // Arrange
            var db = await GetDatabase("theDb");
            var theData = "theData";
            _ = await db.AppendAsync("theStream", theData).ConfigureAwait(false);

            // Act
            var findResult = await db.GetAtVersionAsync<string>("theStream", 0).ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(findResult);
            Assert.IsInstanceOfType(findResult, typeof(Result<string>));
            Assert.IsTrue(findResult.HasValue);
            Assert.AreEqual(theData, findResult.Value);
        }

        [TestMethod]
        public async Task StreamDb_adds_more_than_md_capacity()
        {
            // Arrange
            var db = await GetDatabase("theDb");
            var theStream = $"theStream";

            var addCount = Math.Round(1.3 * Constants.MdCapacity);
            var sw = new Stopwatch();

            for (int i = 0; i < addCount; i++)
            {
                var theData = i;

                // Act
                sw.Restart();
                var addResult = await db.AppendAsync(theStream, theData).ConfigureAwait(false);
                sw.Stop();

                // Assert 1
                Assert.IsNotNull(addResult);
                Assert.IsInstanceOfType(addResult, typeof(Result<Pointer>));
                Assert.IsTrue(addResult.HasValue);
                Debug.WriteLine($"{i}: {sw.ElapsedMilliseconds}");
            }

            // Assert 2
            var stream = await db.GetStreamAsync<int>(theStream).ToListAsync();
            Assert.IsNotNull(stream);
            Assert.AreEqual(addCount, stream.Count);
        }
    }
}