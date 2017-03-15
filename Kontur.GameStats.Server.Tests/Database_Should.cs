using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Kontur.GameStats.Server.Domains;
using Kontur.GameStats.Server.Tests.Utils;
using Kontur.GameStats.Server.Utils;
using NUnit.Framework;

namespace Kontur.GameStats.Server.Tests
{
    [TestFixture]
    public class Database_Should
    {
        private Database database;
        [SetUp]
        public void DatabaseInit()
        {
            database = new Database("C:\\Test.sqlite");
            database.DropAll();
            database.Init();
        }

        [TearDown]
        public void DatabaseDrop()
        {
            //database.DropAll();
        }

        [Test]
        public void Success_WhenInsertServer()
        {
            Assert.DoesNotThrow(() => database.InsertOrUpdateServer(ExampleDomains.Server));
        }

        [Test]
        public void Success_InsertTwice_OneServer()
        {
            Assert.DoesNotThrow(() => database.InsertOrUpdateServer(ExampleDomains.Server));
            Assert.DoesNotThrow(() => database.InsertOrUpdateServer(ExampleDomains.Server));
        }

        [Test]
        public void Exception_WhenInsertServer_Null()
        {
            Assert.Throws<NullReferenceException>(() => database.InsertOrUpdateServer(null));
        }

        [Test]
        public void Exception_WhenInsertServer_EndpointNull()
        {
            Assert.Throws<ArgumentException>(() => database.InsertOrUpdateServer(new Domains.Server(null, null)));
        }

        [Test]
        public void False_WhenInsertMatch_ForNotAdvertiseServer()
        {
            var isInsertMatch = database.TryInsertOrIgnoreMatch(ExampleDomains.Match);

            Assert.False(isInsertMatch);
        }

        [Test]
        public void True_WhenInsertMatch_ForAdvertiseServer()
        {
            database.InsertOrUpdateServer(ExampleDomains.Server);

            var isInsertMatch = database.TryInsertOrIgnoreMatch(ExampleDomains.Match);

            Assert.True(isInsertMatch);
        }

        [Test]
        public void Null_WhenGetServerInfo_ForNotAdvertiseServer()
        {
            var serverInfo = database.GetServerInfo("example.com-1234");

            Assert.Null(serverInfo);
        }

        [Test]
        public void Success_WhenGetServerInfo_ForAdvertiseServer()
        {
            var server = ExampleDomains.Server;
            database.InsertOrUpdateServer(server);

            var actualServerInfo = database.GetServerInfo(server.Endpoint);

            Assert.AreEqual(server.Info, actualServerInfo);
        }

        [Test]
        public void EmptyList_WhenGetAllServers_ForEmptyDatabase()
        {
            var servers = database.GetAllServers();

            Assert.AreEqual(0, servers.Count);
        }

        [Test]
        public void ServersCount1_WhenGetAllServers_For1ServerInDatabase()
        {
            database.InsertOrUpdateServer(ExampleDomains.Server);

            var servers = database.GetAllServers();

            Assert.AreEqual(1, servers.Count);
        }

        [Test]
        public void ServersCount2_WhenGetAllServers_For2ServersInDatabase()
        {
            var data = new RandomData(2);
            var servers = data.GetServers();
            servers.ForEach(s => database.InsertOrUpdateServer(s));

            var actualServers = database.GetAllServers();

            Assert.AreEqual(servers.Count, actualServers.Count);
        }

        [Test]
        public void Null_WhenGetMatchResult_ForEmptyDatabse()
        {
            var matchResult = database.GetMatchResult("example-1234", DateTime.Now);

            Assert.Null(matchResult);
        }

        [Test]
        public void Success_WhenGetMatchResult_For1MatchInDatabase()
        {
            var server = ExampleDomains.Server;
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(server);
            database.TryInsertOrIgnoreMatch(match);

            var actualMatchResult = database.GetMatchResult(match.Server, match.Timestamp);

            Assert.AreEqual(match.Results, actualMatchResult);
        }

        [Test]
        public void EmptyServerStat_WhenGetServerStat_ForEmptyDatabase()
        {
            var actualServerStat = database.GetServerStat(ExampleDomains.Server.Endpoint);

            Assert.AreEqual(new ServerStat(), actualServerStat);
        }

        [Test]
        public void Success_WhenGetServerStat_For1MatchInDatabase()
        {
            var server = ExampleDomains.Server;
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(server);
            database.TryInsertOrIgnoreMatch(match);

            Assert.DoesNotThrow(() => database.GetServerStat(server.Endpoint));
        }

        [Test]
        public void TotalMatchesPlayed10_WhenGetServerStat_For10MatchesInDatabase()
        {
            var countMatches = 10;
            var countServers = 1;
            var randomData = new RandomData(countServers);
            var server = randomData.GetRandomServer();
            var matches = randomData.GetUniqueRandomMatchesForServer(server, countMatches);
            database.InsertOrUpdateServer(server);
            matches.ForEach(m => database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = database.GetServerStat(server.Endpoint);

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
            database.InsertOrUpdateServer(server);
            matches.ForEach(m => database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = database.GetServerStat(server.Endpoint);
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
            database.InsertOrUpdateServer(server);
            matches.ForEach(m => database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = database.GetServerStat(server.Endpoint);
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
            database.InsertOrUpdateServer(server);
            matches.ForEach(m => database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = database.GetServerStat(server.Endpoint);
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
            database.InsertOrUpdateServer(server);
            matches.ForEach(m => database.TryInsertOrIgnoreMatch(m));

            var actualServerStat = database.GetServerStat(server.Endpoint);

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
            database.InsertOrUpdateServer(server);
            database.TryInsertOrIgnoreMatch(match1);
            database.TryInsertOrIgnoreMatch(match2);

            var actualServerStat = database.GetServerStat(server.Endpoint);

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
            servers.ForEach(s => database.InsertOrUpdateServer(s));
            database.TryInsertOrIgnoreMatch(match1);
            database.TryInsertOrIgnoreMatch(match2);

            var actualServerStat1 = database.GetServerStat(servers[0].Endpoint);
            var actualServerStat2 = database.GetServerStat(servers[1].Endpoint);

            Assert.AreEqual(0.5, actualServerStat1.AverageMatchesPerDay);
            Assert.AreEqual(1, actualServerStat2.AverageMatchesPerDay);
        }

        [Test]
        public void EmptyPlayerStat_WhenGetPlayerStat_ForEmptyDatabase()
        {
            var playerStat = database.GetPlayerStat("player");

            Assert.AreEqual(new PlayerStat(), playerStat);
        }

        [Test]
        public void TotalMatchesPlayer1_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard.First().Name);

            Assert.AreEqual(1, playerStat.TotalMatchesPlayed);
        }

        [Test]
        public void TotalMatchesWon1_WhenGetPlayerStat_ForWinnerInOneMatch()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard.First().Name);

            Assert.AreEqual(1, playerStat.TotalMatchesWon);
        }

        [Test]
        public void TotalMatchesWon0_WhenGetPlayerStat_ForLoserInOneMatch()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(0, playerStat.TotalMatchesWon);
        }

        [Test]
        public void AverageScoreboardPercent100_WhenGetPlayerStat_ForWinnerInOneMatch()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard.First().Name);

            Assert.AreEqual(100, playerStat.AverageScoreboardPercent);
        }

        [Test]
        public void AverageScoreboardPercent0_WhenGetPlayerStat_ForLoserInOneMatch()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(0, playerStat.AverageScoreboardPercent);
        }

        [Test]
        public void FavoriteGameMode_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(match.Results.GameMode, playerStat.FavoriteGameMode);
        }

        [Test]
        public void FavoriteServer_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(match.Server, playerStat.FavoriteServer);
        }

        [Test]
        public void UniqueServers_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(1, playerStat.UniqueServers);
        }

        [Test]
        public void LastMatchPlayed_WhenGetPlayerStat_ForOneMatchInDatabase()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[1].Name);

            Assert.AreEqual(match.Timestamp.ToUniversalTime(), playerStat.LastMatchPlayed);
        }

        [Test]
        public void Success_WhenGetPlayerStat_ForRandomMatches()
        {
            var data = new RandomData(10);
            var servers = data.GetServers();
            var matches = servers.SelectMany(i => data.GetUniqueRandomMatchesForServer(i, 10, 10)).ToList();
            servers.ForEach(i => database.InsertOrUpdateServer(i));
            matches.ForEach(i => database.TryInsertOrIgnoreMatch(i));

            Assert.DoesNotThrow(() => database.GetPlayerStat(matches[0].Results.Scoreboard[0].Name));
        }

        [Test]
        public void AverageScoreboardPercent100_WhenGetPlayerStat_ForOnePlayerInMatch()
        {
            var data = new RandomData(1, 1, 1, 1);
            var server = data.GetRandomServer();
            var match = data.GetRandomMatchForServer(server, DateTime.Now);
            
            database.InsertOrUpdateServer(server);
            database.TryInsertOrIgnoreMatch(match);
            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[0].Name);

            Assert.AreEqual(100, playerStat.AverageScoreboardPercent);
        }

        [Test]
        public void IgnoreCase_WhenGetPlayerStat_ForDifferentCasePlayerName()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);

            var playerStat = database.GetPlayerStat(match.Results.Scoreboard[0].Name.ToUpper());

            Assert.AreEqual(1, playerStat.TotalMatchesPlayed);
        }

        [Test]
        public void Success_WhenGetRecentMatches_For1MatchInDatabase()
        {
            var match = ExampleDomains.Match;
            database.InsertOrUpdateServer(ExampleDomains.Server);
            database.TryInsertOrIgnoreMatch(match);
            
            var matches = database.GetRecentMatches(5);

            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual(match, matches[0]);
        }

        [Test]
        public void MatchesCount5_WhenGet5RecentMatches_For10MatchesInDatabase()
        {
            var data = new RandomData(1);
            var server = data.GetRandomServer();
            var matches = data.GetUniqueRandomMatchesForServer(server, 10);
            
            database.InsertOrUpdateServer(server);
            matches.ForEach(i => database.TryInsertOrIgnoreMatch(i));
            var expectedMatches = matches.OrderByDescending(i => i.Timestamp).Take(5).ToList();
            var actualMatches = database.GetRecentMatches(5);
            
            Assert.AreEqual(5, actualMatches.Count);
            Assert.True(expectedMatches.SequenceEqual(actualMatches));
        }

        [Test]
        public void PlayersCount0_WhenGetBestPlayers_ForEmptyDatabase()
        {
            var actualBestPlayers = database.GetBestPlayers(1);

            Assert.AreEqual(0, actualBestPlayers.Count);
        }

        [Test]
        public void PlayersCount0_WhenGetBestPlayers_For9Mathces1Player()
        {
            var data = new RandomData(1, 1, 1, 1);
            var server = data.GetRandomServer();
            var matches = data.GetUniqueRandomMatchesForServer(server, 9);

            database.InsertOrUpdateServer(server);
            matches.ForEach(i => database.TryInsertOrIgnoreMatch(i));
            var actualBestPlayers = database.GetBestPlayers(1);

            Assert.AreEqual(0, actualBestPlayers.Count);
        }

        [Test]
        public void PlayersCount1_WhenGetBestPlayers_For10Mathces1Player()
        {
            var data = new RandomData(1, 1, 1, 1);
            var server = data.GetRandomServer();
            var matches = data.GetUniqueRandomMatchesForServer(server, 10);

            database.InsertOrUpdateServer(server);
            matches.ForEach(i => database.TryInsertOrIgnoreMatch(i));
            var actualBestPlayers = database.GetBestPlayers(1);

            Assert.AreEqual(1, actualBestPlayers.Count);
        }

        [Test]
        public void Success_WhenGetBestPlayers_For100RandomMathces5Players()
        {
            var data = new RandomData(1);
            var server = data.GetRandomServer();
            var matches = data.GetUniqueRandomMatchesForServer(server, 100);

            database.InsertOrUpdateServer(server);
            matches.ForEach(i => database.TryInsertOrIgnoreMatch(i));
            var actualBestPlayers = database.GetBestPlayers(5);

            Assert.AreNotEqual(0, actualBestPlayers.Count);
        }

        [Test]
        public void CountServers0_WhenGetPopularServers_ForServerWithoutMatches()
        {
            var data = new RandomData(1);
            var server = data.GetRandomServer();

            database.InsertOrUpdateServer(server);
            var actualPopularServers = database.GetPopularServers(5);

            Assert.AreEqual(0, actualPopularServers.Count);
        }

        [Test]
        public void CountServers5_WhenGetPopularServers_For5ServersWithMatches()
        {
            var data = new RandomData(5);
            var servers = data.GetServers();
            var matches = servers.SelectMany(i => data.GetUniqueRandomMatchesForServer(i, 1)).ToList();
            servers.ForEach(i => database.InsertOrUpdateServer(i));
            matches.ForEach(i => database.TryInsertOrIgnoreMatch(i));

            var actualPopularServers = database.GetPopularServers(5);

            Assert.AreEqual(5, actualPopularServers.Count);
        }

        [Test]
        public void CountServers50_WhenGetPopularServers_For51ServersWithMatches()
        {
            var data = new RandomData(51);
            var servers = data.GetServers();
            var matches = servers.SelectMany(i => data.GetUniqueRandomMatchesForServer(i, 1)).ToList();
            servers.ForEach(i => database.InsertOrUpdateServer(i));
            matches.ForEach(i => database.TryInsertOrIgnoreMatch(i));

            var actualPopularServers = database.GetPopularServers(51);

            Assert.AreEqual(50, actualPopularServers.Count);
        }

        [Test]
        public void AverageMatchesPerDay1_WhenGetPopularServers_For2MathcesInNeighboringDays()
        {
            var data = new RandomData(1);
            var server = data.GetRandomServer();
            var match1 = data.GetRandomMatchForServer(server, DateTime.Now);
            var match2 = data.GetRandomMatchForServer(server, DateTime.Now.AddDays(1));
            database.InsertOrUpdateServer(server);
            database.TryInsertOrIgnoreMatch(match1);
            database.TryInsertOrIgnoreMatch(match2);

            var actualPopularServers = database.GetPopularServers(1);

            Assert.AreEqual(1m, actualPopularServers[0].AverageMatchesPerDay);
        }

        [Test]
        public void FillRandomData()
        {
            var data = new RandomData(10, 10, 10, 1000);
            var servers = data.GetServers();
            var total = new Stopwatch();
            total.Start();
            Parallel.ForEach(servers, i =>
            {
                var timer = new Stopwatch();
                timer.Start();
                database.InsertOrUpdateServer(i);
                data.GetUniqueRandomMatchesForServer(i, 1, 14).ForEach(ii => database.TryInsertOrIgnoreMatch(ii));
                var allServers = database.GetAllServers();
                Console.WriteLine($"{allServers.Count}: {timer.Elapsed}");
            });

            Console.WriteLine("Всего: " + total.Elapsed);
        }

        [Test]
        public void GetAllServers()
        {
            var timer = new Stopwatch();
            timer.Start();
            var servers = database.GetAllServers();
            Console.WriteLine(timer.Elapsed);
            timer.Restart();
            var matches = database.GetRecentMatches(50);
            Console.WriteLine(timer.Elapsed);
        }
    }
}
