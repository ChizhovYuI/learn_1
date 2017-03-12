using System;
using System.Linq;
using Kontur.GameStats.Server.Domains;
using Kontur.GameStats.Server.Utils;
using NUnit.Framework;

namespace Kontur.GameStats.Server.Tests
{
    [TestFixture]
    public class Database_Should
    {
        private Database Database { get; } = new Database("I:\\Test.sqlite");
        [SetUp]
        public void DatabaseInit()
        {
            Database.DropAll();
            Database.Init();
        }

        [TearDown]
        public void DatabaseDrop()
        {
            //Database.DropAll();
        }

        [Test]
        public void Success_WhenInsertServer()
        {
            Assert.DoesNotThrow(() => Database.InsertOrUpdateServer(ExampleDomains.Server));
        }

        [Test]
        public void Success_InsertTwice_OneServer()
        {
            Assert.DoesNotThrow(() => Database.InsertOrUpdateServer(ExampleDomains.Server));
            Assert.DoesNotThrow(() => Database.InsertOrUpdateServer(ExampleDomains.Server));
        }

        [Test]
        public void Exception_WhenInsertServer_Null()
        {
            Assert.Throws<NullReferenceException>(() => Database.InsertOrUpdateServer(null));
        }

        [Test]
        public void Exception_WhenInsertServer_EndpointNull()
        {
            Assert.Throws<ArgumentException>(() => Database.InsertOrUpdateServer(new Domains.Server(null, null)));
        }

        [Test]
        public void False_WhenInsertMatch_ForNotAdvertiseServer()
        {
            var isInsertMatch = Database.TryInsertOrIgnoreMatch(ExampleDomains.Match);

            Assert.False(isInsertMatch);
        }

        [Test]
        public void True_WhenInsertMatch_ForAdvertiseServer()
        {
            Database.InsertOrUpdateServer(ExampleDomains.Server);

            var isInsertMatch = Database.TryInsertOrIgnoreMatch(ExampleDomains.Match);

            Assert.True(isInsertMatch);
        }

        [Test]
        public void Null_WhenGetServerInfo_ForNotAdvertiseServer()
        {
            var serverInfo = Database.GetServerInfo("example.com-1234");

            Assert.Null(serverInfo);
        }

        [Test]
        public void Success_WhenGetServerInfo_ForAdvertiseServer()
        {
            var server = ExampleDomains.Server;
            Database.InsertOrUpdateServer(server);

            var actualServerInfo = Database.GetServerInfo(server.Endpoint);

            Assert.AreEqual(server.Info, actualServerInfo);
        }

        [Test]
        public void EmptyList_WhenGetAllServers_ForEmptyDatabase()
        {
            var servers = Database.GetAllServers();

            Assert.AreEqual(0, servers.Count);
        }

        [Test]
        public void ListCount1_WhenGetAllServers_For1ServerInDatabase()
        {
            Database.InsertOrUpdateServer(ExampleDomains.Server);

            var servers = Database.GetAllServers();

            Assert.AreEqual(1, servers.Count);
        }

        [Test]
        public void Null_WhenGetMatchResult_ForEmptyDatabse()
        {
            var matchResult = Database.GetMatchResult("example-1234", DateTime.Now);

            Assert.Null(matchResult);
        }

        [Test]
        public void Success_WhenGetMatchResult_For1MatchInDatabase()
        {
            var server = ExampleDomains.Server;
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(server);
            Database.TryInsertOrIgnoreMatch(match);

            var actualMatchResult = Database.GetMatchResult(match.Server, match.Timestamp);

            Assert.AreEqual(match.Results, actualMatchResult);
        }

        [Test]
        public void EmptyServerStat_WhenGetServerStat_ForEmptyDatabase()
        {
            var actualServerStat = Database.GetServerStat(ExampleDomains.Server.Endpoint);

            Assert.AreEqual(new ServerStat(), actualServerStat);
        }

        [Test]
        public void Success_WhenGetServerStat_For1MatchInDatabase()
        {
            var server = ExampleDomains.Server;
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(server);
            Database.TryInsertOrIgnoreMatch(match);

            Assert.DoesNotThrow(() => Database.GetServerStat(server.Endpoint));
        }

        [Test]
        public void TotalMatchesPlayed10_WhenGetServerStat_For10MatchesInDatabase()
        {
            var countMatches = 10;
            var countServers = 1;
            var randomData = new RandomData(countServers);
            var server = randomData.GetRandomServer();
            var matches = randomData.GetUniqueRandomMatchesForServer(server, countMatches);
            Database.InsertOrUpdateServer(server);
            matches.ForEach(m => Database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = Database.GetServerStat(server.Endpoint);

            Assert.AreEqual(countMatches, actualServerStat.TotalMatchesPlayed);
        }

        [Test]
        public void MaximumMatchesPerDay_WhenGetServerStat_For10Matches()
        {
            var countMatches = 10;
            var countServers = 1;
            var randomData = new RandomData(countServers);
            var server = randomData.GetRandomServer();
            var matches = randomData.GetUniqueRandomMatchesForServer(server, countMatches);
            Database.InsertOrUpdateServer(server);
            matches.ForEach(m => Database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = Database.GetServerStat(server.Endpoint);

            var expectedMaximumMatchesPerDay = matches.GroupBy(i => i.Timestamp.Date).Max(i => i.Count());
            Assert.AreEqual(expectedMaximumMatchesPerDay, actualServerStat.MaximumMatchesPerDay);
        }

        [Test]
        public void AverageMatchesPerDay_WhenGetServerStat_For10Matches()
        {
            var countMatches = 10;
            var countServers = 1;
            var randomData = new RandomData(countServers);
            var server = randomData.GetRandomServer();
            var matches = randomData.GetUniqueRandomMatchesForServer(server, countMatches);
            Database.InsertOrUpdateServer(server);
            matches.ForEach(m => Database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = Database.GetServerStat(server.Endpoint);
            var expectedAverageMatchesPerDay = (decimal)matches.Count /
                                               ((matches.Max(i => i.Timestamp).Date - matches.Min(i => i.Timestamp).Date)
                                                .Days + 1);

            Assert.AreEqual(expectedAverageMatchesPerDay, actualServerStat.AverageMatchesPerDay);
        }

        [Test]
        public void MaximumPopulation_WhenGetServerStat_For10Matches()
        {
            var countMatches = 10;
            var countServers = 1;
            var randomData = new RandomData(countServers);
            var server = randomData.GetRandomServer();
            var matches = randomData.GetUniqueRandomMatchesForServer(server, countMatches);
            Database.InsertOrUpdateServer(server);
            matches.ForEach(m => Database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = Database.GetServerStat(server.Endpoint);
            var expectedMaximumPopulation = matches.Max(i => i.Results.Scoreboard.Count);

            Assert.AreEqual(expectedMaximumPopulation, actualServerStat.MaximumPopulation);
        }

        [Test]
        public void AveragePopulation_WhenGetServerStat_For10Matches()
        {
            var countMatches = 10;
            var countServers = 1;
            var randomData = new RandomData(countServers);
            var server = randomData.GetRandomServer();
            var matches = randomData.GetUniqueRandomMatchesForServer(server, countMatches);
            Database.InsertOrUpdateServer(server);
            matches.ForEach(m => Database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = Database.GetServerStat(server.Endpoint);

            var expectedAveragePopulation = (decimal)matches.Sum(i => i.Results.Scoreboard.Count) / matches.Count;
            Assert.AreEqual(expectedAveragePopulation, actualServerStat.AveragePopulation);
        }

        [Test]
        public void MaximumMatchesPerDay1_WhenGetServerStat_For2MAtchesOnDaysBorder()
        {
            var countServers = 1;
            var randomData = new RandomData(countServers);
            var server = randomData.GetRandomServer();
            var match1 = randomData.GetRandomMatchForServer(server, DateTime.Parse("2017-01-22T23:59:59Z"));
            var match2 = randomData.GetRandomMatchForServer(server, DateTime.Parse("2017-01-23T00:00:00Z"));
            Database.InsertOrUpdateServer(server);
            Database.TryInsertOrIgnoreMatch(match1);
            Database.TryInsertOrIgnoreMatch(match2);

            var actualServerStat = Database.GetServerStat(server.Endpoint);

            Assert.AreEqual(1, actualServerStat.MaximumMatchesPerDay);
        }

        [Test]
        public void AverageMatchesPerDay_WhenGetServerStat_For2ServersWith1MatchOnDifferentDays()
        {
            var countServers = 2;
            var randomData = new RandomData(countServers);
            var servers = randomData.GetServers();
            var match1 = randomData.GetRandomMatchForServer(servers[0], DateTime.Parse("2017-01-22T12:00:00Z"));
            var match2 = randomData.GetRandomMatchForServer(servers[1], DateTime.Parse("2017-01-23T12:00:00Z"));
            servers.ForEach(s => Database.InsertOrUpdateServer(s));
            Database.TryInsertOrIgnoreMatch(match1);
            Database.TryInsertOrIgnoreMatch(match2);

            var actualServerStat1 = Database.GetServerStat(servers[0].Endpoint);
            var actualServerStat2 = Database.GetServerStat(servers[1].Endpoint);

            Assert.AreEqual(0.5, actualServerStat1.AverageMatchesPerDay);
            Assert.AreEqual(1, actualServerStat2.AverageMatchesPerDay);
        }

        [Test]
        public void EmptyPlayerStat_WhenGetPlayerStat_ForEmptyDatabase()
        {
            var playerStat = Database.GetPlayerStat("player");

            Assert.AreEqual(new PlayerStat(), playerStat);
        }

        [Test]
        public void TotalMatchesPlayer1_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard.First().Name);

            Assert.AreEqual(1, playerStat.TotalMatchesPlayed);
        }

        [Test]
        public void TotalMatchesWon1_WhenGetPlayerStat_ForWinnerInOneMatch()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard.First().Name);

            Assert.AreEqual(1, playerStat.TotalMatchesWon);
        }

        [Test]
        public void TotalMatchesWon0_WhenGetPlayerStat_ForLoserInOneMatch()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(0, playerStat.TotalMatchesWon);
        }

        [Test]
        public void AverageScoreboardPercent100_WhenGetPlayerStat_ForWinnerInOneMatch()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard.First().Name);

            Assert.AreEqual(100, playerStat.AverageScoreboardPercent);
        }

        [Test]
        public void AverageScoreboardPercent0_WhenGetPlayerStat_ForLoserInOneMatch()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(0, playerStat.AverageScoreboardPercent);
        }

        [Test]
        public void FavoriteGameMode_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(match.Results.GameMode, playerStat.FavoriteGameMode);
        }

        [Test]
        public void FavoriteServer_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(match.Server, playerStat.FavoriteServer);
        }

        [Test]
        public void UniqueServers_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(1, playerStat.UniqueServers);
        }

        [Test]
        public void LastMatchPlayed_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            Database.InsertOrUpdateServer(ExampleDomains.Server);
            Database.TryInsertOrIgnoreMatch(match);

            var playerStat = Database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(match.Timestamp.ToUniversalTime(), playerStat.LastMatchPlayed);
        }

        [Test]
        public void Succes_WhenGetPlayerStat_ForRandomsMatches()
        {
            var data = new RandomData(10);
            var servers = data.GetServers();
            var matches = servers.SelectMany(i => data.GetUniqueRandomMatchesForServer(i, 10, 10)).ToList();
            servers.ForEach(i => Database.InsertOrUpdateServer(i));
            matches.ForEach(i => Database.TryInsertOrIgnoreMatch(i));

            Assert.DoesNotThrow(() => Database.GetPlayerStat(matches[0].Results.Scoreboard[1].Name));
        }

        [Test]
        public void FillRandomData()
        {
            var data = new RandomData(10000, 10, 10, 100000);
            var servers = data.GetServers();
            servers.ForEach(i => Database.InsertOrUpdateServer(i));
            for(var i = 0; i < 100000; i++)
            {
                foreach(var match in data.GetUniqueRandomMatchesForServer(data.GetRandomServer(), 100, 14))
                {
                    Database.TryInsertOrIgnoreMatch(match);
                }
            }
        }
    }
}
