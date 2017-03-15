using System.Linq;
using System.Threading;
using Kontur.GameStats.Server.Domains;
using Kontur.GameStats.Server.Tests.Utils;
using Kontur.GameStats.Server.Utils;
using NUnit.Framework;

namespace Kontur.GameStats.Server.Tests
{
    [TestFixture]
    public class StatsCache_Should
    {
        private const int cacheTime = 1;

        private const int millisecondInSecond = 1000;

        private StatsCache<ServerStat> cache;

        [SetUp]
        public void SetUp()
        {
            cache = new StatsCache<ServerStat>(cacheTime);
        }

        [Test]
        public void False_WhenTryGetItem_ForEmptyCache()
        {
            ServerStat serverStat;

            var isSuccess = cache.TryGetItem(ExampleDomains.ServerStat.Endpoint, out serverStat);

            Assert.False(isSuccess);
        }

        [Test]
        public void Null_WhenTryGetItem_ForEmptyCache()
        {
            ServerStat serverStat;

            cache.TryGetItem(ExampleDomains.ServerStat.Endpoint, out serverStat);

            Assert.Null(serverStat);
        }

        [Test]
        public void Success_WhenTryGetItem_AfterInsertItem()
        {
            var expectedServerStat = ExampleDomains.ServerStat;
            ServerStat actualServerStat;

            cache.Insert(expectedServerStat);
            var isSuccess = cache.TryGetItem(expectedServerStat.Endpoint, out actualServerStat);

            Assert.True(isSuccess);
            Assert.AreEqual(expectedServerStat, actualServerStat);
        }

        [Test]
        public void False_WhenTryGetItem_AfterInsertItemAndWaitCacheTime()
        {
            var expectedServerStat = ExampleDomains.ServerStat;
            ServerStat actualServerStat;

            cache.Insert(expectedServerStat);
            Thread.Sleep(cacheTime * millisecondInSecond);
            var isSuccess = cache.TryGetItem(expectedServerStat.Endpoint, out actualServerStat);

            Assert.False(isSuccess);
        }

        [Test]
        public void CountItems5_WhenTryGetItem_After10InsertItemsWithDelay()
        {
            ServerStat actualServerStat;
            var countServerStats = 10;
            var serverStatList = Enumerable.Range(1, countServerStats).Select(i => new ServerStat($"Server{i}")).ToList();

            foreach (var serverStat in serverStatList)
            {
                Thread.Sleep(cacheTime * millisecondInSecond / countServerStats * 2);
                cache.Insert(serverStat);
            }
            cache.TryGetItem(serverStatList.First().Key, out actualServerStat);

            Assert.AreEqual(5, cache.CountItems);
        }
    }
}
