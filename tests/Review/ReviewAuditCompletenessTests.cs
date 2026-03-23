using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.Bidding;
using TractorGame.Core.AI.V21;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Review;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.Review
{
    public class ReviewAuditCompletenessTests
    {
        [Fact]
        public void AuditLog_ContainsCompletePerTrickFacts_ForFiveGames()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "tractor-review-audit", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var inMemory = new InMemoryLogSink();
                var logger = new CoreLogger(new CompositeLogSink(
                    inMemory,
                    new JsonLineLogSink(tempRoot, "review-audit")));

                for (var i = 0; i < 5; i++)
                    PlaySingleGame(31000 + i, logger, v30OnEvenSeats: i % 2 == 0);

                var entries = inMemory.Entries
                    .OrderBy(entry => entry.RoundId, StringComparer.Ordinal)
                    .ThenBy(entry => entry.Seq)
                    .ToList();

                var roundIds = entries
                    .Select(entry => entry.RoundId)
                    .Where(roundId => !string.IsNullOrWhiteSpace(roundId))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                Assert.Equal(5, roundIds.Count);

                foreach (var roundId in roundIds)
                {
                    var roundEntries = entries
                        .Where(entry => string.Equals(entry.RoundId, roundId, StringComparison.Ordinal))
                        .ToList();

                    Assert.Contains(roundEntries, entry => entry.Event == "bury.accept");
                    Assert.Contains(roundEntries, entry => entry.Event == "game.finish");

                    var trickFinishes = roundEntries.Where(entry => entry.Event == "trick.finish").ToList();
                    Assert.NotEmpty(trickFinishes);

                    foreach (var finish in trickFinishes)
                    {
                        var trickNo = ReadInt(finish.Payload, "trick_no");
                        Assert.True(trickNo > 0, $"trick.finish missing trick_no in {roundId}");

                        var leadTurn = roundEntries.FirstOrDefault(entry =>
                            entry.Event == "turn.start" &&
                            ReadInt(entry.Payload, "trick_no") == trickNo &&
                            ReadBool(entry.Payload, "is_lead"));
                        Assert.NotNull(leadTurn);
                        AssertArrayCount(leadTurn!.Payload, "hands_before_trick", 4);

                        var plays = roundEntries
                            .Where(entry => entry.Event == "play.accept" && ReadInt(entry.Payload, "trick_no") == trickNo)
                            .ToList();
                        Assert.Equal(4, plays.Count);

                        foreach (var play in plays)
                        {
                            Assert.True(play.Payload.ContainsKey("player_hand_before_play"));
                            Assert.True(play.Payload.ContainsKey("player_hand_after_play"));
                            Assert.True(play.Payload.ContainsKey("trick_cards_before_play"));
                            Assert.True(play.Payload.ContainsKey("trick_cards_after_play"));
                            Assert.True(play.Payload.ContainsKey("current_trick_score_before"));
                            Assert.True(play.Payload.ContainsKey("current_winner_before"));
                            Assert.True(play.Payload.ContainsKey("current_winning_cards_before"));
                        }

                        Assert.True(finish.Payload.ContainsKey("winner_index"));
                        Assert.True(finish.Payload.ContainsKey("trick_score"));
                        Assert.True(finish.Payload.ContainsKey("defender_score_before"));
                        Assert.True(finish.Payload.ContainsKey("defender_score_after"));
                        Assert.True(finish.Payload.ContainsKey("trick_cards"));
                        Assert.True(finish.Payload.ContainsKey("hands_after_trick"));
                        AssertArrayCount(finish.Payload, "hands_after_trick", 4);
                    }
                }

                var logFile = Directory.EnumerateFiles(tempRoot, "*.jsonl", SearchOption.AllDirectories).Single();
                var parser = new ReviewLogParser();
                var sessions = parser.ParseFile(logFile, "audit", "review-audit");

                Assert.Equal(5, sessions.Count);
                Assert.All(sessions, session =>
                {
                    Assert.Equal(8, session.BottomCards.Count);
                    Assert.NotEmpty(session.Tricks);
                    Assert.Equal(4, session.Summary.PlayerAiLines.Count);
                    Assert.All(session.Summary.PlayerAiLines, item =>
                    {
                        Assert.InRange(item.PlayerIndex, 0, 3);
                        Assert.False(string.IsNullOrWhiteSpace(item.AiLine));
                    });
                    Assert.All(session.Tricks, trick =>
                    {
                        Assert.Equal(4, trick.HandsBefore.Count);
                        Assert.Equal(4, trick.Plays.Count);
                        Assert.True(trick.WinnerIndex >= 0);
                    });
                });

                foreach (var session in sessions)
                {
                    var roundSeed = ExtractSeed(session.Summary.RoundId);
                    var v30OnEvenSeats = roundSeed % 2 == 0;
                    for (var playerIndex = 0; playerIndex < 4; playerIndex++)
                    {
                        var expected = (playerIndex % 2 == 0) == v30OnEvenSeats ? "V30" : "V21";
                        var actual = session.Summary.PlayerAiLines.Single(item => item.PlayerIndex == playerIndex).AiLine;
                        Assert.Equal(expected, actual, ignoreCase: true);
                    }
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static int ExtractSeed(string roundId)
        {
            var token = roundId.Split('_').Last();
            return int.TryParse(token, out var seed) ? seed : -1;
        }

        private static void PlaySingleGame(int seed, IGameLogger logger, bool v30OnEvenSeats)
        {
            var roundId = $"review_audit_{seed}";
            var game = new Game(seed, logger, sessionId: roundId, gameId: roundId, roundId: roundId);

            game.StartGame();
            RunAutoBidding(game, seed);
            var finalize = game.FinalizeTrumpEx();
            if (!finalize.Success)
                game.FinalizeTrump(PickTrumpSuit(seed));

            var config = new GameConfig
            {
                LevelRank = game.State.LevelRank,
                TrumpSuit = game.State.TrumpSuit
            };

            var strategy = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
            var players = new AIPlayer[4];
            for (var i = 0; i < 4; i++)
            {
                bool useV30 = (i % 2 == 0) == v30OnEvenSeats;
                players[i] = new AIPlayer(
                    config,
                    AIDifficulty.Hard,
                    seed: seed + i + 97,
                    strategyParameters: strategy,
                    decisionLogger: logger,
                    ruleAIOptions: new RuleAIOptions
                    {
                        UseRuleAIV30 = useV30,
                        UseRuleAIV21 = true,
                        EnableShadowCompare = false
                    });
            }

            var dealer = game.State.DealerIndex;
            var buryCards = players[dealer].BuryBottom(
                game.State.PlayerHands[dealer],
                AIRole.Dealer,
                game.BottomCardsSnapshot);
            if (buryCards.Count != 8)
                buryCards = game.State.PlayerHands[dealer].Take(8).ToList();

            var buryResult = game.BuryBottomEx(buryCards);
            if (!buryResult.Success)
                game.BuryBottomEx(game.State.PlayerHands[dealer].Take(8).ToList());

            int turnGuard = 0;
            while (game.State.Phase != GamePhase.Finished && turnGuard < 200)
            {
                turnGuard++;

                var playerIndex = game.State.CurrentPlayer;
                var hand = game.State.PlayerHands[playerIndex];
                if (hand.Count == 0)
                    break;

                var role = ResolveRole(playerIndex, game.State.DealerIndex);
                var currentWinningPlayer = ResolveCurrentWinningPlayer(game, config);
                var currentWinningCards = ResolveCurrentWinningCards(game, config);
                var trickScore = game.CurrentTrick.Sum(play => play.Cards.Sum(card => card.Score));
                var partnerWinning = currentWinningPlayer >= 0 && currentWinningPlayer % 2 == playerIndex % 2 && game.CurrentTrick.Count > 0;
                var knownBottom = playerIndex == dealer ? new List<Card>(game.State.BuriedCards) : new List<Card>();

                var logContext = new AIDecisionLogContext
                {
                    SessionId = game.SessionId,
                    GameId = game.GameId,
                    RoundId = game.RoundId,
                    TrickId = game.CurrentTrickId,
                    TurnId = $"turn_{turnGuard:D4}",
                    PlayerIndex = playerIndex,
                    Actor = $"player_{playerIndex}",
                    TrickIndex = game.CurrentTrickNo,
                    TurnIndex = turnGuard,
                    PlayPosition = game.CurrentTrick.Count + 1,
                    DealerIndex = game.State.DealerIndex,
                    CurrentWinningPlayer = currentWinningPlayer,
                    DefenderScore = game.State.DefenderScore,
                    BottomPoints = game.State.BuriedCards.Sum(card => card.Score)
                };

                List<Card> decision = game.CurrentTrick.Count == 0
                    ? players[playerIndex].Lead(hand, role, playerIndex, null, knownBottom, logContext)
                    : players[playerIndex].Follow(
                        hand,
                        game.CurrentTrick[0].Cards,
                        new List<Card>(currentWinningCards),
                        role,
                        partnerWinning,
                        trickScore,
                        logContext,
                        knownBottom);

                if (game.PlayCardsEx(playerIndex, decision).Success)
                    continue;

                if (!TryFallbackPlay(game, playerIndex, hand, config))
                    throw new InvalidOperationException($"Fallback failed: {roundId}/{game.CurrentTrickId}/p{playerIndex}");
            }

            Assert.Equal(GamePhase.Finished, game.State.Phase);
        }

        private static void RunAutoBidding(Game game, int seed)
        {
            var levelRank = game.State.LevelRank;
            var bidPolicy = new BidPolicy(seed + 5003);
            var visibleHands = new[] { new List<Card>(), new List<Card>(), new List<Card>(), new List<Card>() };

            while (!game.IsDealingComplete)
            {
                var dealResult = game.DealNextCardEx();
                if (!dealResult.Success)
                    break;

                var step = game.LastDealStep;
                if (step == null || step.IsBottomCard)
                    continue;

                var player = step.PlayerIndex;
                visibleHands[player].Add(step.Card);
                var decision = bidPolicy.Decide(new BidPolicy.DecisionContext
                {
                    PlayerIndex = player,
                    DealerIndex = game.State.DealerIndex,
                    LevelRank = levelRank,
                    VisibleCards = new List<Card>(visibleHands[player]),
                    RoundIndex = step.PlayerCardCount - 1,
                    CurrentBidPriority = game.CurrentBidPriority,
                    CurrentBidPlayer = game.CurrentBidPlayer
                });

                var bidCards = decision.AttemptCards;
                if (bidCards.Count == 0)
                    continue;

                var bidDetail = decision.ToLogDetail();
                var bidResult = game.BidTrumpEx(player, bidCards, bidDetail);
                if (bidResult.Success || bidCards.Count <= 1)
                    continue;

                var single = new List<Card> { bidCards[0] };
                if (game.CanBidTrumpEx(player, single).Success)
                    game.BidTrumpEx(player, single, bidDetail);
            }
        }

        private static Suit PickTrumpSuit(int seed)
        {
            return (seed % 4) switch
            {
                0 => Suit.Spade,
                1 => Suit.Heart,
                2 => Suit.Club,
                _ => Suit.Diamond
            };
        }

        private static AIRole ResolveRole(int playerIndex, int dealerIndex)
        {
            if (playerIndex == dealerIndex)
                return AIRole.Dealer;

            return playerIndex % 2 == dealerIndex % 2 ? AIRole.DealerPartner : AIRole.Opponent;
        }

        private static int ResolveCurrentWinningPlayer(Game game, GameConfig config)
        {
            if (game.CurrentTrick.Count == 0)
                return -1;

            return new TrickJudge(config).DetermineWinner(game.CurrentTrick);
        }

        private static IReadOnlyList<Card> ResolveCurrentWinningCards(Game game, GameConfig config)
        {
            if (game.CurrentTrick.Count == 0)
                return Array.Empty<Card>();

            var winner = ResolveCurrentWinningPlayer(game, config);
            return game.CurrentTrick.First(play => play.PlayerIndex == winner).Cards;
        }

        private static bool TryFallbackPlay(Game game, int playerIndex, List<Card> hand, GameConfig config)
        {
            if (game.CurrentTrick.Count == 0)
            {
                foreach (var card in hand)
                {
                    if (game.PlayCardsEx(playerIndex, new List<Card> { card }).Success)
                        return true;
                }

                return false;
            }

            var leadCards = game.CurrentTrick[0].Cards;
            var validator = new FollowValidator(config);
            foreach (var card in hand)
            {
                var simple = new List<Card> { card };
                if (validator.IsValidFollowEx(hand, leadCards, simple).Success && game.PlayCardsEx(playerIndex, simple).Success)
                    return true;
            }

            return false;
        }

        private static void AssertArrayCount(Dictionary<string, object?> payload, string key, int expected)
        {
            Assert.True(payload.ContainsKey(key), $"missing payload key: {key}");
            var enumerable = Assert.IsAssignableFrom<IEnumerable>(payload[key]);
            Assert.Equal(expected, enumerable.Cast<object>().Count());
        }

        private static int ReadInt(Dictionary<string, object?> payload, string key)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
                return 0;

            return value switch
            {
                int number => number,
                long number => (int)number,
                _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
            };
        }

        private static bool ReadBool(Dictionary<string, object?> payload, string key)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
                return false;

            return value switch
            {
                bool booleanValue => booleanValue,
                _ => bool.TryParse(value.ToString(), out var parsed) && parsed
            };
        }
    }
}
