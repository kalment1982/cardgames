using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.RegressionTests
{
    public class BaselineBehaviorTests
    {
        private static AIPlayer CreateDeterministicAi(
            GameConfig config,
            IGameLogger logger,
            RuleAIOptions options,
            int seed = 7)
        {
            var strategy = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
            strategy.EasyRandomnessRate = 0;
            strategy.MediumRandomnessRate = 0;
            strategy.HardRandomnessRate = 0;
            strategy.ExpertRandomnessRate = 0;
            return new AIPlayer(config, AIDifficulty.Hard, seed, strategy, logger, options);
        }

        [Fact]
        public void Lead_RuleAIV21Passthrough_MatchesLegacyDecision()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var legacyLogger = new CoreLogger(new InMemoryLogSink());
            var legacyAi = CreateDeterministicAi(
                config,
                legacyLogger,
                new RuleAIOptions { UseRuleAIV21 = false, EnableShadowCompare = false });

            var newLogger = new CoreLogger(new InMemoryLogSink());
            var newAi = CreateDeterministicAi(
                config,
                newLogger,
                new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = true, ShadowSampleRate = 1.0 });

            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Heart, Rank.Ace)
            };

            var legacy = legacyAi.Lead(hand, AIRole.Opponent);
            var next = newAi.Lead(hand, AIRole.Opponent);

            Assert.Equal(Signature(legacy), Signature(next));
        }

        [Fact]
        public void Follow_RuleAIV21Passthrough_MatchesLegacyDecision()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var legacyAi = CreateDeterministicAi(
                config,
                new CoreLogger(new InMemoryLogSink()),
                new RuleAIOptions { UseRuleAIV21 = false, EnableShadowCompare = false });
            var newAi = CreateDeterministicAi(
                config,
                new CoreLogger(new InMemoryLogSink()),
                new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = true, ShadowSampleRate = 1.0 });

            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Eight)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Seven)
            };
            var currentWinningCards = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Ace)
            };

            var legacy = legacyAi.Follow(hand, leadCards, currentWinningCards, AIRole.Opponent, partnerWinning: false);
            var next = newAi.Follow(hand, leadCards, currentWinningCards, AIRole.Opponent, partnerWinning: false);

            Assert.Equal(Signature(legacy), Signature(next));
        }

        [Fact]
        public void Follow_ShadowMode_EmitsDecisionCompareAndPerfLogs()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var sink = new InMemoryLogSink();
            var logger = new CoreLogger(sink);
            var ai = CreateDeterministicAi(
                config,
                logger,
                new RuleAIOptions { UseRuleAIV21 = false, EnableShadowCompare = true, ShadowSampleRate = 1.0 });

            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Four)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Queen)
            };

            ai.Follow(hand, leadCards, leadCards, AIRole.Opponent, partnerWinning: true);

            Assert.Contains(sink.Entries, entry => entry.Event == "ai.decision");
            Assert.Contains(sink.Entries, entry => entry.Event == "ai.compare");
            Assert.Contains(sink.Entries, entry => entry.Event == "ai.perf");

            var compare = sink.Entries.Single(entry => entry.Event == "ai.compare");
            Assert.False((bool)compare.Payload["divergence"]!);
        }

        [Fact]
        public void Follow_RuleAIV21_MinimizeLoss_SkipsExpensiveTrumpCutOnZeroScore()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var ai = CreateDeterministicAi(
                config,
                new CoreLogger(new InMemoryLogSink()),
                new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = false });

            var hand = new List<Card>
            {
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Diamond, Rank.Three)
            };
            var leadCards = new List<Card> { new Card(Suit.Spade, Rank.Four) };

            var result = ai.Follow(
                hand,
                leadCards,
                leadCards,
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 0);

            Assert.Single(result);
            Assert.Equal(Suit.Diamond, result[0].Suit);
            Assert.Equal(Rank.Three, result[0].Rank);
        }

        [Fact]
        public void Follow_RuleAIV21_TakeScore_UsesTrumpCutOnHighScore()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var ai = CreateDeterministicAi(
                config,
                new CoreLogger(new InMemoryLogSink()),
                new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = false });

            var hand = new List<Card>
            {
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Diamond, Rank.Three)
            };
            var leadCards = new List<Card> { new Card(Suit.Spade, Rank.Four) };

            var result = ai.Follow(
                hand,
                leadCards,
                leadCards,
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 20);

            Assert.Single(result);
            Assert.Equal(Suit.Joker, result[0].Suit);
            Assert.Equal(Rank.BigJoker, result[0].Rank);
        }

        [Fact]
        public void Follow_LogsGameRoundAndPlayerContext()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var sink = new InMemoryLogSink();
            var ai = CreateDeterministicAi(
                config,
                new CoreLogger(sink),
                new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = false });

            ai.Follow(
                new List<Card>
                {
                    new Card(Suit.Heart, Rank.Three),
                    new Card(Suit.Club, Rank.Five)
                },
                new List<Card> { new Card(Suit.Diamond, Rank.Four) },
                new List<Card> { new Card(Suit.Diamond, Rank.Four) },
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 5,
                logContext: new AIDecisionLogContext
                {
                    SessionId = "sess_test",
                    GameId = "game_test",
                    RoundId = "round_test",
                    PlayerIndex = 2
                });

            var decisionLog = sink.Entries.Single(entry => entry.Event == "ai.decision");
            Assert.Equal("sess_test", decisionLog.SessionId);
            Assert.Equal("game_test", decisionLog.GameId);
            Assert.Equal("round_test", decisionLog.RoundId);
            Assert.Equal("player_2", decisionLog.Actor);
            Assert.Equal(2, (int)decisionLog.Payload["player_index"]!);
            Assert.True(decisionLog.Payload.ContainsKey("has_winning_candidate"));
            Assert.True(decisionLog.Payload.ContainsKey("winning_candidate_count"));
            Assert.True(decisionLog.Payload.ContainsKey("has_secure_winning_candidate"));
            Assert.True(decisionLog.Payload.ContainsKey("secure_winning_candidate_count"));
            Assert.True(decisionLog.Payload.ContainsKey("max_candidate_win_security"));
        }

        private static string Signature(List<Card> cards)
        {
            return string.Join(
                ",",
                cards
                    .Select(card => $"{(int)card.Suit}-{(int)card.Rank}")
                    .OrderBy(text => text));
        }
    }
}
