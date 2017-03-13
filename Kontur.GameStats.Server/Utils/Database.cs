﻿using System;
using System.Data.SQLite;
using System.Linq;
using Kontur.GameStats.Server.Domains;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;

namespace Kontur.GameStats.Server.Utils
{
    public class Database
    {
        public Database(string dataSource)
        {
            connectionString =
                new SQLiteConnectionStringBuilder {
                    DataSource = dataSource,
                    Version = 3,
                    SyncMode = SynchronizationModes.Normal,
                    JournalMode = SQLiteJournalModeEnum.Wal,
                    DateTimeKind = DateTimeKind.Utc,
                    DateTimeFormat = SQLiteDateFormats.ISO8601
                }.ConnectionString;
        }

        public void Init()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    CreateTableServer(connection, transaction);
                    CreateTableMatch(connection, transaction);
                    CreateTableScoreboard(connection, transaction);

                    transaction.Commit();
                }
                connection.Close();
            }
        }

        public void DropAll()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    DropTable(Tables.Server, connection, transaction);
                    DropTable(Tables.Match, connection, transaction);
                    DropTable(Tables.Scoreboard, connection, transaction);

                    transaction.Commit();
                }
                connection.Close();
            }
        }

        public void InsertOrUpdateServer(Domains.Server server)
        {
            if (server == null) throw new NullReferenceException();
            if (server.Endpoint == null) throw new ArgumentException($"Не указан {Domains.Server.Properties.Endpoint}");

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var query = $@"
INSERT OR REPLACE INTO {Tables.Server} (
{Domains.Server.Properties.Endpoint},
{ServerInfo.Properties.Name},
{ServerInfo.Properties.GameModes}
) VALUES (
'{server.Endpoint}',
'{server.Info.Name}',
'{JsonConvert.SerializeObject(server.Info.GameModes)}'
);";
                    var command = new SQLiteCommand(query, connection, transaction);
                    command.ExecuteNonQuery();

                    transaction.Commit();
                }
                connection.Close();
            }
        }

        public bool TryInsertOrIgnoreMatch(Match match)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    if (!IsExistsServer(match.Server, connection, transaction)) return false;

                    if (IsExistsMatch(match, connection, transaction)) return true;

                    InsertMatch(match, connection, transaction);
                    var matchId = GetLastInsertId(connection, transaction);
                    InsertScoreboard(match.Results.Scoreboard, connection, transaction, matchId);

                    transaction.Commit();
                }

                connection.Close();
            }

            return true;
        }

        public ServerInfo GetServerInfo(string endpoint)
        {
            ServerInfo serverInfo;
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var query = $@"
SELECT {ServerInfo.Properties.Name}, {ServerInfo.Properties.GameModes}
FROM {Tables.Server} 
WHERE {Domains.Server.Properties.Endpoint} = '{endpoint}'
LIMIT 1";
                    var command = new SQLiteCommand(query, connection, transaction);
                    var reader = command.ExecuteReader();
                    serverInfo = reader.Read() ? GetServerInfoFromReader(reader) : null;

                    reader.Close();
                    transaction.Commit();
                }
                connection.Close();
            }
            return serverInfo;
        }

        public List<Domains.Server> GetAllServers()
        {
            var servers = new List<Domains.Server>();
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var query = $@"
SELECT {Domains.Server.Properties.Endpoint}, {ServerInfo.Properties.Name}, {ServerInfo.Properties.GameModes}
FROM {Tables.Server}";
                    var command = new SQLiteCommand(query, connection, transaction);
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        servers.Add(new Domains.Server((string) reader[Domains.Server.Properties.Endpoint],
                            GetServerInfoFromReader(reader)));
                    }

                    reader.Close();
                    transaction.Commit();
                }
                connection.Close();
            }
            return servers;
        }

        public MatchResult GetMatchResult(string endpoint, DateTime timestamp)
        {
            MatchResult matchResult = null;
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    var query = $@"
SELECT 
{MatchResult.Properties.Id},
{MatchResult.Properties.Map},
{MatchResult.Properties.GameMode},
{MatchResult.Properties.FragLimit},
{MatchResult.Properties.TimeLimit},
{MatchResult.Properties.TimeElapsed}
FROM {Tables.Match}
WHERE {Match.Properties.Server} = '{endpoint}'
AND {Match.Properties.Timestamp} = '{DateTimeForSqLite(timestamp)}'
LIMIT 1";
                    var command = new SQLiteCommand(query, connection, transaction);
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        matchResult = new MatchResult((string) reader[MatchResult.Properties.Map],
                            (string) reader[MatchResult.Properties.GameMode],
                            (int) reader[MatchResult.Properties.FragLimit],
                            (int) reader[MatchResult.Properties.TimeLimit],
                            (decimal) reader[MatchResult.Properties.TimeElapsed], new List<Scoreboard>(),
                            (long) reader[MatchResult.Properties.Id]);
                        FillScoreboard(matchResult, connection, transaction);
                    }
                    reader.Close();
                    transaction.Commit();
                }
                connection.Close();
            }
            return matchResult;
        }

        public ServerStat GetServerStat(string endpoint)
        {
            DateTime? lastMatchDate;
            List<Match> matches = null;
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    lastMatchDate = GetLastMatchDateTime(connection, transaction);
                    if (lastMatchDate != null) { matches = GetMatchesForServerStat(endpoint, connection, transaction); }
                    transaction.Commit();
                }
                connection.Close();
            }
            if (matches == null || matches.Count == 0) return new ServerStat();

            var firstMatchDateTime = matches.Min(i => i.Timestamp);
            var countDays = (lastMatchDate.Value.Date - firstMatchDateTime.Date).Days + 1;
            var totalMatchesPlayed = matches.Count;
            var maximumMatchesPerDay = matches.GroupBy(i => i.Timestamp.Date).Max(i => i.Count());
            var averageMatchesPerDay = (decimal) totalMatchesPlayed / countDays;
            var maximumPopulation = matches.Max(i => i.Results.Population);
            var averagePopulation = (decimal) matches.Sum(i => i.Results.Population) / totalMatchesPlayed;
            var top5GameModes =
                matches.GroupBy(i => i.Results.GameMode).OrderByDescending(i => i.Count()).ThenBy(i => i.Key).Take(5)
                    .Select(i => i.Key).ToArray();
            var top5Maps =
                matches.GroupBy(i => i.Results.Map).OrderByDescending(i => i.Count()).ThenBy(i => i.Key).Take(5).Select(
                    i => i.Key).ToArray();
            var serverStat = new ServerStat(totalMatchesPlayed, maximumMatchesPerDay, averageMatchesPerDay,
                maximumPopulation, averagePopulation, top5GameModes, top5Maps);
            return serverStat;
        }

        public PlayerStat GetPlayerStat(string name)
        {
            DateTime? lastMatchDate;
            List<Scoreboard> scoreboards = null;
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    lastMatchDate = GetLastMatchDateTime(connection, transaction);
                    if (lastMatchDate != null) {
                        scoreboards = GetScoreboardForPlayerStat(name, connection, transaction);
                    }
                    transaction.Commit();
                }
                connection.Close();
            }
            if (scoreboards == null || scoreboards.Count == 0) return new PlayerStat();

            var playerStat = GetPlayerStat(lastMatchDate.Value, scoreboards);
            return playerStat;
        }

        public List<Match> GetRecentMatches(int count)
        {
            Monitor.Enter(RecentMathcesCash.Locker);
            try
            {
                var matches = new List<Match>();
                if (count <= 0) 
                    return matches;

                if (RecentMathcesCash.TryGetItems(count, ref matches)) 
                    return matches;

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        var query = $@"
SELECT
{MatchResult.Properties.Id},
{Match.Properties.Server},
{Match.Properties.Timestamp},
{MatchResult.Properties.Map},
{MatchResult.Properties.GameMode},
{MatchResult.Properties.FragLimit},
{MatchResult.Properties.TimeLimit},
{MatchResult.Properties.TimeElapsed}
FROM {Tables.Match}
ORDER BY {Tables.Match}.{Match.Properties.Timestamp} DESC
LIMIT {MaxCountItemsInReport}";
                        var command = new SQLiteCommand(query, connection, transaction);
                        var reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            matches.Add(new Match((string) reader[Match.Properties.Server],
                                (DateTime) reader[Match.Properties.Timestamp],
                                new MatchResult((string) reader[MatchResult.Properties.Map],
                                    (string) reader[MatchResult.Properties.GameMode],
                                    (int) reader[MatchResult.Properties.FragLimit],
                                    (int) reader[MatchResult.Properties.TimeLimit],
                                    (decimal) reader[MatchResult.Properties.TimeElapsed], new List<Scoreboard>(),
                                    (long) reader[MatchResult.Properties.Id])));
                        }
                        reader.Close();
                        matches.ForEach(i => FillScoreboard(i.Results, connection, transaction));
                        
                        transaction.Commit();
                    }
                    connection.Close();
                }
                RecentMathcesCash.Update(matches);
                return matches.Take(count).ToList();
            }
            finally {
                Monitor.Exit(RecentMathcesCash.Locker);
            }
        }

        public List<BestPlayer> GetBestPlayers(int count)
        {
            Monitor.Enter(BestPlayersCash.Locker);
            try
            {
                var bestPlayers = new List<BestPlayer>();
                if (count <= 0) 
                    return bestPlayers;

                if(BestPlayersCash.TryGetItems(count, ref bestPlayers))
                    return bestPlayers;

                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        var query = $@"
SELECT {BestPlayer.Properties.Name},
1.0*SUM({Scoreboard.Properties.Kills})/SUM({Scoreboard.Properties.Deaths}) AS {BestPlayer.Properties.KillToDeathRatio}
FROM {Tables.Scoreboard}
GROUP BY {Scoreboard.Properties.SearchName}
HAVING SUM({Scoreboard.Properties.Deaths}) > 0 AND COUNT({Scoreboard.Properties.MatchId}) >= 10
ORDER BY {BestPlayer.Properties.KillToDeathRatio} DESC
LIMIT {MaxCountItemsInReport}";
                        var command = new SQLiteCommand(query, connection, transaction);
                        var reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            bestPlayers.Add(new BestPlayer((string) reader[Scoreboard.Properties.Name],
                                (decimal) (double) reader[BestPlayer.Properties.KillToDeathRatio]));
                        }
                        reader.Close();

                        transaction.Commit();
                    }
                    connection.Close();
                }
                BestPlayersCash.Update(bestPlayers);

                return bestPlayers.Take(count).ToList();
            }
            finally
            {
                Monitor.Exit(BestPlayersCash.Locker);
            }
        }

        public List<PopularServer> GetPopularServers(int count)
        {
            Monitor.Enter(PopulareServersCash.Locker);
            try
            {
                var popularServers = new List<PopularServer>();
                if (count <= 0) return popularServers;
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        var lastMatchDateTime = GetLastMatchDateTime(connection, transaction);
                        if (lastMatchDateTime != null)
                        {
                            var query = $@"
SELECT 
{PopularServer.Properties.Endpoint},
{PopularServer.Properties.Name},
1.0*COUNT({Tables.Match}.{Match.Properties.Server})/
(julianday(date('{DateTimeForSqLite(lastMatchDateTime.Value)}'))-
julianday(date(MIN({Tables.Match}.{Match.Properties.Timestamp}))) + 1)
AS {PopularServer.Properties.AverageMatchesPerDay}
FROM {Tables.Server}
LEFT JOIN {Tables.Match} ON {Tables.Server}.{Domains.Server.Properties.Endpoint} = {Tables.Match}.{Match.Properties
                                .Server}
GROUP BY {Tables.Match}.{Match.Properties.Server}
ORDER BY {PopularServer.Properties.AverageMatchesPerDay} DESC
LIMIT {MaxCountItemsInReport}";
                            var command = new SQLiteCommand(query, connection, transaction);
                            var reader = command.ExecuteReader();
                            while (reader.Read())
                            {
                                popularServers.Add(new PopularServer(
                                    (string) reader[PopularServer.Properties.Endpoint],
                                    (string) reader[PopularServer.Properties.Name],
                                    (decimal) (double) reader[PopularServer.Properties.AverageMatchesPerDay]));
                            }
                            reader.Close();
                        }
                        transaction.Commit();
                    }
                    connection.Close();
                }
                PopulareServersCash.Update(popularServers);

                return popularServers.Take(count).ToList();
            }
            finally
            {
                Monitor.Exit(PopulareServersCash.Locker);
            }
        }

        private List<Scoreboard> GetScoreboardForPlayerStat(string name, SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            var scoreboard1 = "s1";
            var scoreboard2 = "s2";
            var query = $@"
SELECT
{scoreboard1}.{Scoreboard.Properties.Kills},
{scoreboard1}.{Scoreboard.Properties.Deaths},
{scoreboard1}.{Scoreboard.Properties.Place},
{Tables.Match}.{Match.Properties.Server},
{Tables.Match}.{Match.Properties.Timestamp},
{Tables.Match}.{MatchResult.Properties.GameMode},
COUNT({Tables.Match}.{MatchResult.Properties.Id}) AS {MatchResult.Properties.Population}
FROM {Tables.Scoreboard} as {scoreboard1}
LEFT JOIN {Tables.Match}
ON {scoreboard1}.{Scoreboard.Properties.MatchId} = {Tables.Match}.{MatchResult.Properties.Id}
LEFT JOIN {Tables.Scoreboard} as {scoreboard2}
ON {Tables.Match}.{MatchResult.Properties.Id} = {scoreboard2}.{Scoreboard.Properties.MatchId}
WHERE {scoreboard1}.{Scoreboard.Properties.SearchName} = '{name.ToLower()}'
GROUP BY {Tables.Match}.{MatchResult.Properties.Id}";
            var command = new SQLiteCommand(query, connection, transaction);
            var reader = command.ExecuteReader();
            var scoreboards = new List<Scoreboard>();
            while (reader.Read())
            {
                scoreboards.Add(new Scoreboard((int) reader[Scoreboard.Properties.Kills],
                    (int) reader[Scoreboard.Properties.Deaths], (int) reader[Scoreboard.Properties.Place],
                    new Match((string) reader[Match.Properties.Server], (DateTime) reader[Match.Properties.Timestamp],
                        new MatchResult((string) reader[MatchResult.Properties.GameMode],
                            (int) (long) reader[MatchResult.Properties.Population]))));
            }
            reader.Close();
            return scoreboards;
        }

        private static DateTime? GetLastMatchDateTime(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"
SELECT {Match.Properties.Timestamp} FROM {Tables.Match}
ORDER BY {Match.Properties.Timestamp} DESC
LIMIT 1";
            var command = new SQLiteCommand(query, connection, transaction);
            return (DateTime?) command.ExecuteScalar();
        }

        private static void DropTable(string name, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"DROP TABLE IF EXISTS {name}";
            var command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();
        }

        private static ServerInfo GetServerInfoFromReader(SQLiteDataReader reader)
        {
            return new ServerInfo((string) reader[ServerInfo.Properties.Name],
                JsonConvert.DeserializeObject<string[]>((string) reader[ServerInfo.Properties.GameModes]));
        }

        private static void CreateTableServer(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"
CREATE TABLE IF NOT EXISTS {Tables.Server} (
{Domains.Server.Properties.Endpoint} VARCHAR(100) PRIMARY KEY,
{ServerInfo.Properties.Name} VARCHAR(100),
{ServerInfo.Properties.GameModes} VARCHAR(100)
) WITHOUT ROWID";
            var command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();
        }

        private static void CreateTableMatch(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"
CREATE TABLE IF NOT EXISTS {Tables.Match} (
{Match.Properties.Server} VARCHAR(100),
{Match.Properties.Timestamp} DATETIME,
{MatchResult.Properties.Map} VARCHAR(100),
{MatchResult.Properties.GameMode} VARCHAR(6),
{MatchResult.Properties.FragLimit} INT,
{MatchResult.Properties.TimeLimit} INT,
{MatchResult.Properties.TimeElapsed} DECIMAL(10,6)
)";
            var command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();

            query = $@"
CREATE INDEX IF NOT EXISTS {Tables.Match}_{Match.Properties.Server}
ON {Tables.Match}({Match.Properties.Server} ASC)";
            command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();

            query = $@"
CREATE INDEX IF NOT EXISTS {Tables.Match}_{Match.Properties.Timestamp}
ON {Tables.Match}({Match.Properties.Timestamp} DESC)";
            command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();
        }

        private static void CreateTableScoreboard(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"
CREATE TABLE IF NOT EXISTS {Tables.Scoreboard} (
{Scoreboard.Properties.Name} VARCHAR(100),
{Scoreboard.Properties.SearchName} VARCHAR(100),
{Scoreboard.Properties.Frags} INT,
{Scoreboard.Properties.Kills} INT,
{Scoreboard.Properties.Deaths} INT,
{Scoreboard.Properties.MatchId} INTEGER,
{Scoreboard.Properties.Place} INT
)";
            var command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();

            query = $@"
CREATE INDEX IF NOT EXISTS {Tables.Scoreboard}_{Scoreboard.Properties.SearchName}
ON {Tables.Scoreboard}({Scoreboard.Properties.SearchName} ASC)";
            command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();

            query = $@"
CREATE INDEX IF NOT EXISTS {Tables.Scoreboard}_{Scoreboard.Properties.MatchId}
ON  {Tables.Scoreboard}({Scoreboard.Properties.MatchId} ASC)";
            command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();
        }

        private static bool IsExistsMatch(Match match, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"
SELECT {MatchResult.Properties.Id} FROM {Tables.Match}
WHERE {Match.Properties.Server} = '{match.Server}'
AND {Match.Properties.Timestamp} = '{DateTimeForSqLite(match.Timestamp)}'
LIMIT 1";
            var command = new SQLiteCommand(query, connection, transaction);
            var isExistMatch = command.ExecuteScalar() != null;
            return isExistMatch;
        }

        private static bool IsExistsServer(string endpoint, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"
SELECT {Domains.Server.Properties.Endpoint} FROM {Tables.Server}
WHERE {Domains.Server.Properties.Endpoint} = '{endpoint}'
LIMIT 1";
            var command = new SQLiteCommand(query, connection, transaction);
            var isExistServer = command.ExecuteScalar() != null;
            return isExistServer;
        }

        private static void InsertMatch(Match match, SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = $@"
INSERT INTO {Tables.Match} (
{Match.Properties.Server},
{Match.Properties.Timestamp},
{MatchResult.Properties.Map},
{MatchResult.Properties.GameMode},
{MatchResult.Properties.FragLimit},
{MatchResult.Properties.TimeLimit},
{MatchResult.Properties.TimeElapsed}
) VALUES (
'{match.Server}',
'{DateTimeForSqLite(match.Timestamp)}',
'{match.Results.Map}',
'{match.Results.GameMode}',
{match.Results.FragLimit},
{match.Results.TimeLimit},
{DecimalForSqLite(match.Results.TimeElapsed)}
)";
            var command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();
        }

        private static long GetLastInsertId(SQLiteConnection connection, SQLiteTransaction transaction)
        {
            var query = "SELECT last_insert_rowid()";
            var command = new SQLiteCommand(query, connection, transaction);
            return (long) command.ExecuteScalar();
        }

        private static void InsertScoreboard(List<Scoreboard> scoreboard, SQLiteConnection connection,
            SQLiteTransaction transaction, long matchId)
        {
            var query = $@"
INSERT INTO {Tables.Scoreboard} (
{Scoreboard.Properties.Name},
{Scoreboard.Properties.SearchName},
{Scoreboard.Properties.Frags},
{Scoreboard.Properties.Kills},
{Scoreboard.Properties.Deaths},
{Scoreboard.Properties.MatchId},
{Scoreboard.Properties.Place}
) VALUES (
{string.Join("),(", scoreboard.Select((s, i) => $@"
'{s.Name}',
'{s.Name.ToLower()}',
{s.Frags},
{s.Kills},
{s.Deaths},
{matchId},
{i + 1}"))}
)";
            var command = new SQLiteCommand(query, connection, transaction);
            command.ExecuteNonQuery();
        }

        private static void FillScoreboard(MatchResult matchResult, SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            var query = $@"
SELECT 
{Scoreboard.Properties.Name},
{Scoreboard.Properties.Frags},
{Scoreboard.Properties.Kills},
{Scoreboard.Properties.Deaths} 
FROM {Tables.Scoreboard}
WHERE {Scoreboard.Properties.MatchId} = {matchResult.Id}
ORDER BY {Scoreboard.Properties.Place}";
            var command = new SQLiteCommand(query, connection, transaction);
            var reader = command.ExecuteReader();
            while (reader.Read())
            {
                matchResult.Scoreboard.Add(new Scoreboard((string) reader[Scoreboard.Properties.Name],
                    (int) reader[Scoreboard.Properties.Frags], (int) reader[Scoreboard.Properties.Kills],
                    (int) reader[Scoreboard.Properties.Deaths]));
            }
        }

        private static List<Match> GetMatchesForServerStat(string endpoint, SQLiteConnection connection,
            SQLiteTransaction transaction)
        {
            var query = $@"
SELECT
{Match.Properties.Timestamp},
COUNT({Tables.Match}.{MatchResult.Properties.Id}) AS {MatchResult.Properties.Population},
{MatchResult.Properties.GameMode},
{MatchResult.Properties.Map}
FROM {Tables.Match}
LEFT JOIN {Tables.Scoreboard} ON {Tables.Scoreboard}.{Scoreboard.Properties.MatchId} = {Tables.Match}.{MatchResult
                .Properties.Id}
WHERE {Match.Properties.Server} = '{endpoint}'
GROUP BY {Tables.Match}.{MatchResult.Properties.Id}";
            var command = new SQLiteCommand(query, connection, transaction);
            var reader = command.ExecuteReader();
            var matches = new List<Match>();
            while (reader.Read())
            {
                matches.Add(new Match((DateTime) reader[Match.Properties.Timestamp],
                    new MatchResult((string) reader[MatchResult.Properties.Map],
                        (string) reader[MatchResult.Properties.GameMode],
                        (int) (long) reader[MatchResult.Properties.Population])));
            }
            reader.Close();
            return matches;
        }

        private static PlayerStat GetPlayerStat(DateTime lastMatchDate, List<Scoreboard> scoreboards)
        {
            var firstMatchDateTime = scoreboards.Min(i => i.Match.Timestamp);
            var countDays = (lastMatchDate.Date - firstMatchDateTime.Date).Days + 1;
            var totalMatchesPlayed = scoreboards.Count;
            var totalMatchesWon = scoreboards.Count(i => i.Place == 1);
            var favoriteServer =
                scoreboards.GroupBy(i => i.Match.Server).OrderByDescending(i => i.Count()).ThenBy(i => i.Key).First()
                    .Select(i => i.Match.Server).First();
            var uniqueServers = scoreboards.GroupBy(i => i.Match.Server).Count();
            var favoriteGameMode =
                scoreboards.GroupBy(i => i.Match.Results.GameMode).OrderByDescending(i => i.Count()).ThenBy(i => i.Key)
                    .First().Select(i => i.Match.Results.GameMode).First();
            var averageScoreboardPercent =
                scoreboards.Select(
                        i =>
                            i.Match.Results.Population == 1
                                ? 100
                                : (decimal) (i.Match.Results.Population - i.Place) / (i.Match.Results.Population - 1) * 100)
                    .Sum() / totalMatchesPlayed;
            var maximumMatchesPerDay = scoreboards.GroupBy(i => i.Match.Timestamp.Date).Max(i => i.Count());
            var averageMatchesPerDay = (decimal) totalMatchesPlayed / countDays;
            var lastMatchPlayed = scoreboards.Max(i => i.Match.Timestamp);
            var totalDeaths = scoreboards.Sum(i => i.Deaths);
            var killToDeathRatio = totalDeaths > 0 ? (decimal) scoreboards.Sum(i => i.Kills) / totalDeaths : 0;
            var playerStat = new PlayerStat(totalMatchesPlayed, totalMatchesWon, favoriteServer, uniqueServers,
                favoriteGameMode, averageScoreboardPercent, maximumMatchesPerDay, averageMatchesPerDay, lastMatchPlayed,
                killToDeathRatio);
            return playerStat;
        }

        private static string DateTimeForSqLite(DateTime dateTime)
            => $"{dateTime.ToUniversalTime():yyyy-MM-dd HH:mm:ss}";

        private static string DecimalForSqLite(decimal num) => num.ToString(CultureInfo.InvariantCulture);

        private readonly string connectionString;

        private Cash<Match> RecentMathcesCash = new Cash<Match>(CashTime);

        private Cash<BestPlayer> BestPlayersCash = new Cash<BestPlayer>(CashTime);

        private Cash<PopularServer> PopulareServersCash = new Cash<PopularServer>(CashTime);

        /// <summary>
        /// Время актуальности кэша в секундах
        /// </summary>
        private const int CashTime = 60;

        /// <summary>
        /// Максимальное количество элементов для вывода в отчеты
        /// </summary>
        private const int MaxCountItemsInReport = 50;

        private static class Tables
        {
            public const string Server = "server";

            public const string Match = "match";

            public const string Scoreboard = "scoreboard";
        }

        private class Cash<T>
        {
            private List<T> list;

            private DateTime lastUpdateDateTime;

            private readonly int cashTime;
            
            public object Locker = new object();

            public Cash(int cashTime)
            {
                this.cashTime = cashTime;
            }

            public bool TryGetItems(int count, ref List<T> result)
            {
                if ((DateTime.Now - lastUpdateDateTime).TotalSeconds < cashTime)
                {
                    result = list.Take(count).ToList();
                    return true;
                }

                return false;
            }

            public void Update(List<T> newResult)
            {
                list = newResult;
                lastUpdateDateTime = DateTime.Now;
            }
        }
    }
}
