﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FotM.Domain;
using FotM.TestingUtilities;
using FotM.Utilities;
using MoreLinq;
using NUnit.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FotM.Cassandra.Tests
{
    /// <summary>
    /// This class will test prediction(derivation?) accuracy of Cassandra based on 
    /// autogenerated history.
    /// </summary>
    [TestFixture]
    [TestClass]
    public class CassandraSuperTests: ArmoryTestingBase
    {
        const int TeamSize = 3;
        private static readonly Random Rng = new Random(367);

        private static readonly Realm[] Realms = new Realm[]
        {
            new Realm() { RealmId = 0, RealmSlug = "0", RealmName = "Zero"},
            new Realm() { RealmId = 1, RealmSlug = "1", RealmName = "Two"},
            new Realm() { RealmId = 2, RealmSlug = "2", RealmName = "Three"},
            new Realm() { RealmId = 3, RealmSlug = "3", RealmName = "Four"},
            new Realm() { RealmId = 4, RealmSlug = "4", RealmName = "Five"},
        };

        private readonly Dictionary<string, IKMeans<PlayerChange>> _clusterers;
        private Dictionary<Leaderboard, HashSet<Team>> _history;

        public CassandraSuperTests() : base(Bracket.Threes)
        {
            _clusterers = new Dictionary<string, IKMeans<PlayerChange>>
            {
                {"Accord with normalization", new AccordKMeans(normalize: true)},
                {"Accord no normalization", new AccordKMeans(normalize: false)},
                {"Numl non-normalized Manhattan distance", new NumlKMeans(new numl.Math.Metrics.ManhattanDistance())},
                {"Numl non-normalized Euclidean distance", new NumlKMeans(new numl.Math.Metrics.EuclidianDistance())},
                {"Numl non-normalized Hamming distance", new NumlKMeans(new numl.Math.Metrics.HammingDistance())},
                {"Numl non-normalized Cosine distance", new NumlKMeans(new numl.Math.Metrics.CosineDistance())},
            };

            LeaderboardEntry[] startingEntries = GeneratePlayers(999);
            Team[] teams = GenerateTeams(startingEntries);
            _history = GenerateHistory(teams, startingEntries,
                length: 500, nWeeksBefore: 3, nMaxGamesPerWeek: 40);
        }

        public static LeaderboardEntry[] GeneratePlayers(int nPlayers)
        {
            var specs = CollectionExtensions.GetValues<CharacterSpec>();

            return Enumerable.Range(0, nPlayers)
                .Select(i =>
                {
                    int nRealm = Rng.Next(5);

                    var guid = new byte[16];
                    Rng.NextBytes(guid);

                    return new LeaderboardEntry()
                    {
                        ClassId = Rng.Next(10),
                        FactionId = 0,
                        GenderId = Rng.Next(2),
                        RaceId = Rng.Next(5),
                        Name = new Guid(guid).ToString().Substring(0, 5),
                        Ranking = Rng.Next(1, 1000),
                        Rating = Rng.Next(2100, 2300),
                        RealmId = Realms[nRealm].RealmId,
                        RealmName = Realms[nRealm].RealmName,
                        RealmSlug = Realms[nRealm].RealmSlug,
                        WeeklyWins = 0,
                        WeeklyLosses = 0,
                        SeasonLosses = Rng.Next(20),
                        SeasonWins = Rng.Next(20),
                        SpecId = (int)specs[Rng.Next(specs.Length)],
                    };
                })
                .ToArray();
        }

        public static Team[] GenerateTeams(LeaderboardEntry[] entries)
        {
            /* Generate teams from players of the same realm */
            var playersPerRealm = entries.GroupBy(p => p.RealmId);

            List<Team> teams = new List<Team>();

            foreach (var realm in playersPerRealm)
            {
                var players = realm.Shuffle(Rng).ToArray();

                int nTeams = players.Length / TeamSize;

                for (int i = 0; i < nTeams; ++i)
                {
                    var teamPlayers = new List<Player>();

                    for (int j = 0; j < TeamSize; ++j)
                    {
                        var playerEntry = players[i*TeamSize + j];

                        teamPlayers.Add(playerEntry.Player());
                    }

                    // if there are no healers in the team make first player play a healer
                    if (!teamPlayers.Any(Healers.IsHealer))
                    {
                        MakeHealer(players[i*TeamSize]);
                    }

                    teams.Add(new Team(teamPlayers));
                }
            }

            return teams.ToArray();
        }

        private static void MakeHealer(LeaderboardEntry player)
        {
            int specNum = Rng.Next(Healers.Specs.Count);

            var specClass = Healers.Specs.ElementAt(specNum);
            player.ClassId = (int) specClass.Value;
            player.SpecId = (int) specClass.Key;
        }

        Leaderboard Play(Leaderboard leaderboard, Team[] playingTeams, Func<LeaderboardEntry, int> ratingChange)
        {
            var updatedEntries = new List<LeaderboardEntry>();

            foreach (var playingTeam in playingTeams)
            {
                var previousPlayerEntries =
                    playingTeam.Players.Select(player => leaderboard[player])
                        .OrderByDescending(p => p.Rating)
                        .ToArray();

                for (int iPlayer = 0; iPlayer < TeamSize; ++iPlayer)
                {
                    var previousEntry = previousPlayerEntries[iPlayer];

                    int playerRatingChange = ratingChange(previousEntry);

                    var updatedEntry = UpdateEntry(previousEntry, playerRatingChange);
                    updatedEntries.Add(updatedEntry);
                }
            }

            // Fill it
            var updatedPlayers = playingTeams.SelectMany(t => t.Players).ToHashSet();
            var oldEntries = leaderboard.Rows.Where(r => !updatedPlayers.Contains(r.Player()));

            var newLeaderboard = new Leaderboard
            {
                Time = DateTime.Now,
                Bracket = this.Bracket,
                Rows = oldEntries.Union(updatedEntries).ToArray()
            };

            newLeaderboard.Order();
            return newLeaderboard;
        }

        Dictionary<Leaderboard, HashSet<Team>> GenerateHistory(Team[] teams, LeaderboardEntry[] startingEntries,
            int length, int nWeeksBefore, int nMaxGamesPerWeek)
        {
            var leaderboard = CreateLeaderboard(startingEntries);

            // make all teams play some number of games for several weeks before simulation
            for (int i = 0; i < nWeeksBefore; ++i)
            {
                var teamsGroupedByGamesPerWeek = teams.GroupBy(t => Rng.Next(nMaxGamesPerWeek));

                foreach (var teamGrouping in teamsGroupedByGamesPerWeek)
                {
                    for (int j = 0; j < teamGrouping.Key; ++j)
                    {
                        bool win = Rng.Next(2) == 0;
                        int opponentRating = 2300 + Rng.Next(-100, 100);

                        leaderboard = Play(leaderboard, teamGrouping.ToArray(),
                            player => RatingUtils.EstimatedRatingChange(player.Rating, opponentRating, win));
                    }
                }
            }

            foreach (var entry in leaderboard.Rows)
            {
                entry.WeeklyLosses /= nWeeksBefore*2;
                entry.WeeklyWins /= nWeeksBefore*2;
            }
            
            // recorded simulation history
            var results = new Dictionary<Leaderboard, HashSet<Team>>();
            results[leaderboard] = new HashSet<Team>();

            for (int i = 0; i < length; ++i)
            {
                // Select subset of teams that will play
                int nTeamsPlayedThisTurn = 5;// 8 + Rng.Next(4);

                Team[] playingTeams = teams.Shuffle(Rng).Take(nTeamsPlayedThisTurn).ToArray();

                bool win = Rng.Next(2) == 0;
                int opponentRating = 2300 + Rng.Next(-100, 100);

                // For each of them generate rating change, update players and create new leaderboard
                var newLeaderboard = Play(leaderboard, playingTeams,
                    player => RatingUtils.EstimatedRatingChange(player.Rating, opponentRating, win)
                );
                
                // Remember who really played this turn
                results[newLeaderboard] = playingTeams.ToHashSet();

                leaderboard = newLeaderboard;
            }

            return results;
        }

        private double RunCassandra(int historyLength, string clustererName, IKMeans<PlayerChange> clusterer,
            bool traceOn)
        {
            var cassandra = new Cassandra(clusterer);

            int nTotalTeams = 0;
            int numerator = 0;
            int nRetrievedTeams = 0;

            var previousLeaderboard = _history.First().Key;

            foreach (var step in _history.Skip(1).Take(historyLength))
            {
                var leaderboard = step.Key;
                var relevantTeams = step.Value;

                nTotalTeams += relevantTeams.Count;

                var retrievedTeams = cassandra.FindTeams(previousLeaderboard, leaderboard);

                nRetrievedTeams += retrievedTeams.Count();
                numerator += retrievedTeams.Intersect(relevantTeams).Count();

                previousLeaderboard = leaderboard;
            }

            double precision = numerator / (double)nRetrievedTeams;
            double recall = numerator / (double)nTotalTeams;

            double f1 = 2 * precision * recall / (precision + recall);
            double f2 = 5 * precision * recall / (4 * precision + recall);

            if (traceOn)
            {
                string msg = string.Format("Cassandra ({0}):\nPrecision {1:F2}, Recall {2:F2}, F1: {3:F2}, F2: {4:F2}",
                    clustererName,
                    precision,
                    recall,
                    f1,
                    f2);

                Trace.WriteLine(msg);
                Trace.WriteLine(cassandra.Stats);
                Trace.WriteLine("");
            }

            return f2;
        }

        [Test]
        [TestMethod]
        public void CalculateDerivationAccuracy()
        {
            foreach (var clusterer in _clusterers)
            {
                RunCassandra(_history.Count, clusterer.Key, clusterer.Value, true);
            }
        }

        [Test]
        [TestMethod]
        public void CalculateWeights()
        {
            int historyLength = 20;
            const double learningRate = 1e-2;

            var descriptor = new FeatureAttributeDescriptor<PlayerChange>();

            var seedWeights = descriptor.Features.Select(f => 1.0).ToArray();

            var results = new Dictionary<double, double[]>();

            var result = Functional.FindMinimum(
                weights =>
                {
                    descriptor.SetWeights(weights);
                    var f2 = RunCassandra(historyLength, "WeightSearch", new AccordKMeans(true), false);

                    results[f2] = weights.ToArray();

                    return f2;
                },
                seedWeights,
                learningRate,
                1e-2,
                400);

            var bestResult = results.MaxBy(p => p.Key);

            string weightStr = string.Join(",", bestResult.Value.Select(w => w.ToString("F2")));

            string msg = string.Format("Best F2={0}, W=[{1}]", bestResult.Key, weightStr);
            Trace.WriteLine(msg);
        }
    }
}
