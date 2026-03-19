using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V30
{
    public class AIPlayerV30IntegrationTests
    {
        [Fact]
        public void Lead_UseRuleAIV30_EmitsV30OverlayPathAndBundle()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var sink = new InMemoryLogSink();
            var ai = new AIPlayer(
                config,
                AIDifficulty.Hard,
                seed: 17,
                strategyParameters: AIStrategyParameters.CreatePreset(AIDifficulty.Hard),
                decisionLogger: new CoreLogger(sink),
                ruleAIOptions: new RuleAIOptions
                {
                    UseRuleAIV30 = true,
                    UseRuleAIV21 = true,
                    EnableShadowCompare = false
                });

            var result = ai.Lead(
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Heart, Rank.Six),
                    new Card(Suit.Diamond, Rank.Three),
                    new Card(Suit.Club, Rank.Four)
                },
                AIRole.Dealer,
                myPosition: 0,
                logContext: new AIDecisionLogContext
                {
                    PlayerIndex = 0,
                    DealerIndex = 0,
                    TrickIndex = 1,
                    TurnIndex = 1,
                    PlayPosition = 1,
                    RoundId = "round_v30_ai_player"
                });

            Assert.Single(result);
            Assert.Equal(Suit.Spade, result[0].Suit);

            var decisionEntry = Assert.Single(sink.Entries.Where(entry => entry.Event == "ai.decision"));
            var bundleEntry = Assert.Single(sink.Entries.Where(entry => entry.Event == "ai.bundle"));

            Assert.Equal("rule_ai_v30_lead_overlay", decisionEntry.Payload["path"]);
            Assert.Equal("RuleAIEngineV30", decisionEntry.Payload["phase_policy"]);
            Assert.Equal("lead001.dealer_stable_side", decisionEntry.Payload["selected_candidate_id"]);

            var bundleV30 = Assert.IsType<JsonElement>(bundleEntry.Payload["bundle_v30"]);
            Assert.Equal("Lead", bundleV30.GetProperty("phase").GetString());
            Assert.Equal("v30_overlay_policy", bundleV30.GetProperty("mode").GetString());
            Assert.Equal("StableSideSuitRun", bundleV30.GetProperty("primary_intent").GetString());
            Assert.Equal("lead001.dealer_stable_side", bundleV30.GetProperty("selected_candidate_id").GetString());
            Assert.Equal("lead001.dealer_stable_side", bundleV30.GetProperty("selected_reason").GetString());
            Assert.Equal("Lead-001", bundleV30.GetProperty("triggered_rules")[0].GetString());
        }

        [Fact]
        public void Follow_UseRuleAIV30_EmitsV30OverlayPath()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var sink = new InMemoryLogSink();
            var ai = new AIPlayer(
                config,
                AIDifficulty.Hard,
                seed: 17,
                strategyParameters: AIStrategyParameters.CreatePreset(AIDifficulty.Hard),
                decisionLogger: new CoreLogger(sink),
                ruleAIOptions: new RuleAIOptions
                {
                    UseRuleAIV30 = true,
                    UseRuleAIV21 = true,
                    EnableShadowCompare = false
                });

            var result = ai.Follow(
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.Three)
                },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.King) },
                AIRole.Opponent,
                partnerWinning: true,
                trickScore: 0,
                logContext: new AIDecisionLogContext
                {
                    PlayerIndex = 0,
                    DealerIndex = 2,
                    TrickIndex = 4,
                    TurnIndex = 16,
                    PlayPosition = 3,
                    CurrentWinningPlayer = 2,
                    RoundId = "round_v30_follow"
                });

            Assert.Single(result);
            Assert.Equal(Rank.Three, result[0].Rank);

            var decisionEntry = sink.Entries.Single(entry => entry.Event == "ai.decision");
            Assert.Equal("rule_ai_v30_follow_overlay", decisionEntry.Payload["path"]);
            Assert.Equal("RuleAIEngineV30", decisionEntry.Payload["phase_policy"]);
            Assert.Equal("PassToMate", decisionEntry.Payload["primary_intent"]);
        }

        [Fact]
        public void Follow_UseRuleAIV30_IncludesOnlyAuthoritativeLegalCandidates()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Heart };
            var sink = new InMemoryLogSink();
            var ai = new AIPlayer(
                config,
                AIDifficulty.Hard,
                seed: 23,
                strategyParameters: AIStrategyParameters.CreatePreset(AIDifficulty.Hard),
                decisionLogger: new CoreLogger(sink),
                ruleAIOptions: new RuleAIOptions
                {
                    UseRuleAIV30 = true,
                    UseRuleAIV21 = true,
                    EnableShadowCompare = false
                });

            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Club, Rank.Five),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Diamond, Rank.King),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Spade, Rank.Jack)
            };
            var lead = new List<Card> { new Card(Suit.Spade, Rank.Three) };

            var result = ai.Follow(
                hand,
                lead,
                lead,
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 0,
                logContext: new AIDecisionLogContext
                {
                    PlayerIndex = 3,
                    DealerIndex = 0,
                    TrickIndex = 5,
                    TurnIndex = 18,
                    PlayPosition = 2,
                    CurrentWinningPlayer = 0,
                    RoundId = "round_v30_follow_authoritative_legal"
                });

            var validator = new FollowValidator(config);
            Assert.True(validator.IsValidFollow(hand, lead, result));
            Assert.All(result, card => Assert.True(card.Suit == Suit.Spade && card.Rank != Rank.Two));

            var decisionEntry = sink.Entries.Single(entry => entry.Event == "ai.decision");
            Assert.Equal("rule_ai_v30_follow_overlay", decisionEntry.Payload["path"]);
        }

        [Fact]
        public void Follow_UseRuleAIV30_WhenShortageHasZeroPointFiller_AvoidsPointDump()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var sink = new InMemoryLogSink();
            var ai = new AIPlayer(
                config,
                AIDifficulty.Hard,
                seed: 29,
                strategyParameters: AIStrategyParameters.CreatePreset(AIDifficulty.Hard),
                decisionLogger: new CoreLogger(sink),
                ruleAIOptions: new RuleAIOptions
                {
                    UseRuleAIV30 = true,
                    UseRuleAIV21 = true,
                    EnableShadowCompare = false
                });

            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Diamond, Rank.Four)
            };
            var lead = new List<Card> { new Card(Suit.Club, Rank.Four), new Card(Suit.Club, Rank.Four) };

            var result = ai.Follow(
                hand,
                lead,
                lead,
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 10,
                logContext: new AIDecisionLogContext
                {
                    PlayerIndex = 0,
                    DealerIndex = 2,
                    TrickIndex = 5,
                    TurnIndex = 19,
                    PlayPosition = 3,
                    CurrentWinningPlayer = 1,
                    RoundId = "round_v30_follow_zero_point_filler"
                });

            Assert.Equal(2, result.Count);
            Assert.DoesNotContain(result, card => card.Score > 0);

            var decisionEntry = sink.Entries.Single(entry => entry.Event == "ai.decision");
            Assert.Equal("MinimizeLoss", decisionEntry.Payload["primary_intent"]);
        }
    }
}
