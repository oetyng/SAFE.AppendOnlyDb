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
        public async Task TestInitialize()
        {
            await InitClient();
        }

        [TestMethod]
        public async Task DatabaseTests_getoradd_returns_database()
        {
            // Act
            var db = await GetDatabase("theDb");

            // Assert
            Assert.IsNotNull(db);
            Assert.IsInstanceOfType(db, typeof(StreamDb));
        }

        [TestMethod]
        public async Task DatabaseTests_add_returns_pointer()
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
        public async Task DatabaseTests_returns_stored_value()
        {
            // Arrange
            var db = await GetDatabase("theDb");
            var theData = 42;
            _ = await db.AppendAsync("theStream", theData).ConfigureAwait(false);

            // Act
            var findResult = await db.GetVersionAsync<int>("theStream", 0).ConfigureAwait(false);

            // Assert
            Assert.IsNotNull(findResult);
            Assert.IsInstanceOfType(findResult, typeof(Result<int>));
            Assert.IsTrue(findResult.HasValue);
            Assert.AreEqual(theData, findResult.Value);
        }

        [TestMethod] // Quite long running, so do not include in automatic test suite.
        public async Task DatabaseTests_adds_more_than_md_capacity()
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
            var stream = (await db.GetStream<int>(theStream)).ToList();
            Assert.IsNotNull(stream);
            Assert.AreEqual(addCount, stream.Count);
        }
    }
}