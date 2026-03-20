using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Follow
{
    public sealed class FollowCandidateOverlayV30
    {
        private sealed class CandidateMetrics
        {
            public List<Card> Cards { get; init; } = new();
            public bool CanBeatCurrentWinner { get; init; }
            public FollowWinSecurityV30 Security { get; init; } = FollowWinSecurityV30.Fragile;
            public int ControlSpendCost { get; init; }
            public int CandidatePoints { get; init; }
            public int DiscardStrengthCost { get; init; }
            public int HighControlCount { get; init; }
        }

        public List<FollowCandidateViewV30> BuildAndRank(
            RuleAIContext context,
            IReadOnlyList<List<Card>> legalCandidates)
        {
            var comparer = new CardComparer(context.GameConfig);
            var currentWinning = context.CurrentWinningCards.Count > 0
                ? context.CurrentWinningCards
                : context.LeadCards;

            var metrics = legalCandidates
                .Where(candidate => candidate != null && candidate.Count == context.LeadCards.Count)
                .Select(candidate => BuildMetrics(context, currentWinning, candidate, comparer))
                .ToList();
            bool rearOpponentThreatHigh = context.PartnerWinning &&
                HasHighRearOpponentThreat(context, currentWinning);
            bool tempoReclaimMode = ShouldActivateTempoReclaim(context, metrics);
            var intent = ResolveIntent(context, metrics, tempoReclaimMode);

            var views = metrics
                .Select(metric => BuildView(context, metric, intent, rearOpponentThreatHigh, tempoReclaimMode))
                .ToList();

            return views
                .OrderByDescending(view => view.OverlayScore)
                .ThenBy(view => view.ControlSpendCost)
                .ThenBy(view => view.CanBeatCurrentWinner ? view.CandidatePoints : 0)
                .ThenBy(view => view.CandidatePoints)
                .ThenBy(view => ShouldPreferPassiveDiscardTieBreak(context, view) ? view.DiscardStrengthCost : int.MinValue)
                .ThenBy(view => RuleAIUtility.BuildCandidateKey(view.Cards))
                .ToList();
        }

        public FollowOverlayIntentV30 ResolveIntent(RuleAIContext context, IReadOnlyList<FollowCandidateViewV30> ranked)
        {
            if (context.PartnerWinning)
                return FollowOverlayIntentV30.PassToMate;

            if (ranked.Count == 0)
                return FollowOverlayIntentV30.MinimizeLoss;

            if (ranked[0].CanBeatCurrentWinner)
            {
                if (context.TrickScore == 0 &&
                    IsDealerSide(context) &&
                    ranked[0].ControlSpendCost <= 14 &&
                    ranked[0].DiscardStrengthCost <= 24 &&
                    (context.DecisionFrame.PlayPosition >= 3 || context.CardsLeftMin <= 8))
                {
                    return FollowOverlayIntentV30.TakeLead;
                }

                return FollowOverlayIntentV30.TakeScore;
            }

            return ShouldUsePrepareEndgameIntent(context)
                ? FollowOverlayIntentV30.PrepareEndgame
                : FollowOverlayIntentV30.MinimizeLoss;
        }

        private static FollowOverlayIntentV30 ResolveIntent(
            RuleAIContext context,
            IReadOnlyList<CandidateMetrics> metrics,
            bool tempoReclaimMode)
        {
            if (context.PartnerWinning)
                return FollowOverlayIntentV30.PassToMate;

            bool hasWinningCandidate = metrics.Any(metric => metric.CanBeatCurrentWinner);
            if (tempoReclaimMode && hasWinningCandidate)
                return FollowOverlayIntentV30.TakeLead;

            if (hasWinningCandidate && context.TrickScore >= 5)
                return FollowOverlayIntentV30.TakeScore;

            return ShouldUsePrepareEndgameIntent(context)
                ? FollowOverlayIntentV30.PrepareEndgame
                : FollowOverlayIntentV30.MinimizeLoss;
        }

        private CandidateMetrics BuildMetrics(
            RuleAIContext context,
            List<Card> currentWinning,
            List<Card> candidate,
            CardComparer comparer)
        {
            bool canBeat = RuleAIUtility.CanBeatCards(context.GameConfig, currentWinning, candidate);
            int points = candidate.Sum(card => card.Score);
            int controlSpend = EstimateControlSpendCost(context, candidate, comparer);
            int highControlCount = RuleAIUtility.CountHighControlCards(context.GameConfig, candidate);
            int discardStrengthCost = EstimateDiscardStrengthCost(context, candidate);
            int margin = canBeat
                ? RuleAIUtility.CalculateWinMargin(context.GameConfig, candidate, currentWinning, comparer)
                : 0;

            return new CandidateMetrics
            {
                Cards = new List<Card>(candidate),
                CanBeatCurrentWinner = canBeat,
                Security = ResolveSecurity(margin),
                ControlSpendCost = controlSpend,
                CandidatePoints = points,
                DiscardStrengthCost = discardStrengthCost,
                HighControlCount = highControlCount
            };
        }

        private static FollowCandidateViewV30 BuildView(
            RuleAIContext context,
            CandidateMetrics metric,
            FollowOverlayIntentV30 intent,
            bool rearOpponentThreatHigh,
            bool tempoReclaimMode)
        {
            int score = ScoreCandidate(context, intent, metric, rearOpponentThreatHigh, tempoReclaimMode);
            string reason = BuildReason(intent, metric, rearOpponentThreatHigh);

            return new FollowCandidateViewV30
            {
                Cards = new List<Card>(metric.Cards),
                CanBeatCurrentWinner = metric.CanBeatCurrentWinner,
                Security = metric.Security,
                OverlayScore = score,
                ControlSpendCost = metric.ControlSpendCost,
                CandidatePoints = metric.CandidatePoints,
                DiscardStrengthCost = metric.DiscardStrengthCost,
                Reason = reason
            };
        }

        private static FollowWinSecurityV30 ResolveSecurity(int margin)
        {
            if (margin >= 250)
                return FollowWinSecurityV30.Lock;
            if (margin >= 80)
                return FollowWinSecurityV30.Stable;
            return FollowWinSecurityV30.Fragile;
        }

        private static int EstimateControlSpendCost(RuleAIContext context, List<Card> candidate, CardComparer comparer)
        {
            int structureLoss = RuleAIUtility.EstimateStructureLoss(context.GameConfig, context.MyHand, candidate, comparer);
            int highControlCount = RuleAIUtility.CountHighControlCards(context.GameConfig, candidate);
            int trumpCount = candidate.Count(context.GameConfig.IsTrump);
            return structureLoss + highControlCount * 18 + trumpCount * 6;
        }

        private static int EstimateDiscardStrengthCost(RuleAIContext context, IReadOnlyList<Card> candidate)
        {
            int cost = 0;
            foreach (var card in candidate)
            {
                cost += card.Rank switch
                {
                    Rank.Two => 2,
                    Rank.Three => 3,
                    Rank.Four => 4,
                    Rank.Five => 5,
                    Rank.Six => 6,
                    Rank.Seven => 7,
                    Rank.Eight => 8,
                    Rank.Nine => 9,
                    Rank.Ten => 10,
                    Rank.Jack => 11,
                    Rank.Queen => 14,
                    Rank.King => 16,
                    Rank.Ace => 18,
                    Rank.SmallJoker => 24,
                    Rank.BigJoker => 28,
                    _ => 0
                };

                if (context.GameConfig.IsTrump(card))
                    cost += 12;

                if (card.Score > 0)
                    cost += 8;
            }

            return cost;
        }

        private static bool ShouldPreferPassiveDiscardTieBreak(
            RuleAIContext context,
            FollowCandidateViewV30 view)
        {
            return context.PartnerWinning || !view.CanBeatCurrentWinner;
        }

        private static int ScoreCandidate(
            RuleAIContext context,
            FollowOverlayIntentV30 intent,
            CandidateMetrics metric,
            bool rearOpponentThreatHigh,
            bool tempoReclaimMode)
        {
            bool canBeat = metric.CanBeatCurrentWinner;
            var security = metric.Security;
            int points = metric.CandidatePoints;
            int controlSpend = metric.ControlSpendCost;
            int highControlCount = metric.HighControlCount;
            int score = 0;
            if (intent == FollowOverlayIntentV30.PassToMate)
            {
                if (canBeat)
                    score -= 100;

                if (rearOpponentThreatHigh)
                {
                    score -= controlSpend * 2;
                    score += points == 0
                        ? 24
                        : -(140 + points * 6);
                }
                else
                {
                    score -= controlSpend;
                    score += points * 5;
                }

                if (context.TrickScore == 0)
                    score -= points * 2;

                if (rearOpponentThreatHigh)
                    score += points == 0 ? 12 : -18;

                return score;
            }

            if (canBeat)
            {
                if (intent == FollowOverlayIntentV30.TakeLead)
                {
                    int opponentsBehind = CountOpponentsBehind(context);
                    score += security switch
                    {
                        FollowWinSecurityV30.Lock => 92,
                        FollowWinSecurityV30.Stable => 68,
                        _ => 42
                    };
                    score -= controlSpend * 2;
                    score -= highControlCount * 20;
                    score -= metric.DiscardStrengthCost / 2;
                    score -= opponentsBehind * 16;
                    score -= points * 10;

                    if (controlSpend > 14 || metric.DiscardStrengthCost > 24)
                        score -= 72;

                    if (points == 0)
                        score += 24;

                    if (context.DecisionFrame.PlayPosition >= 3)
                        score += 18;

                    if (opponentsBehind == 0)
                        score += 22;

                    if (context.DecisionFrame.EndgameLevel != EndgameLevel.None)
                        score += 18;

                    if (tempoReclaimMode)
                        score += 12;

                    return score;
                }

                if (context.TrickScore == 0)
                {
                    int opponentsBehind = CountOpponentsBehind(context);
                    int zeroPointWinValue = security switch
                    {
                        FollowWinSecurityV30.Lock => 36,
                        FollowWinSecurityV30.Stable => 20,
                        _ => 8
                    };

                    score += zeroPointWinValue;
                    score -= controlSpend * 4;
                    score -= highControlCount * 28;
                    score -= opponentsBehind * 24;
                    score -= points * 8;

                    if (points == 0)
                        score += 8;

                    if (context.DecisionFrame.EndgameLevel != EndgameLevel.None)
                        score += 18;

                    if (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                        context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High)
                    {
                        score += 22;
                    }

                    if (opponentsBehind == 0)
                        score += 12;

                    return score;
                }

                score += security switch
                {
                    FollowWinSecurityV30.Lock => 160,
                    FollowWinSecurityV30.Stable => 120,
                    _ => 80
                };

                score += context.TrickScore * 3;
                score -= points;
                score -= controlSpend;
                if (ShouldDowngradeFragileThirdHandTake(context, metric))
                {
                    score -= 120;
                    score -= points * 2;
                }
                return score;
            }

            if (intent == FollowOverlayIntentV30.TakeLead)
            {
                score -= points * 8;
                score -= controlSpend;
                score -= metric.DiscardStrengthCost / 2;
                if (points == 0)
                    score += 4;
                return score;
            }

            score -= context.TrickScore * 2;
            score -= points * (context.TrickScore >= 5 ? 8 : 6);
            score -= controlSpend;
            if (intent == FollowOverlayIntentV30.PrepareEndgame)
                score += 8 - metric.DiscardStrengthCost / 3;
            if (points == 0)
                score += context.TrickScore >= 5 ? 18 : 10;
            return score;
        }

        private static string BuildReason(
            FollowOverlayIntentV30 intent,
            CandidateMetrics metric,
            bool rearOpponentThreatHigh)
        {
            bool canBeat = metric.CanBeatCurrentWinner;
            int points = metric.CandidatePoints;
            int controlSpend = metric.ControlSpendCost;
            string securityText = metric.Security.ToString().ToLowerInvariant();
            if (intent == FollowOverlayIntentV30.PassToMate)
            {
                if (canBeat)
                    return $"pass_to_mate_avoid_overtake_failed_points_{points}_cost_{controlSpend}";

                return rearOpponentThreatHigh
                    ? $"pass_to_mate_hold_points_vs_rear_threat_points_{points}_cost_{controlSpend}"
                    : $"pass_to_mate_points_{points}_cost_{controlSpend}";
            }

            if (intent == FollowOverlayIntentV30.TakeLead && canBeat)
                return $"take_lead_{securityText}_points_{points}_cost_{controlSpend}";

            if (canBeat)
                return $"take_score_{securityText}_points_{points}_cost_{controlSpend}";

            if (intent == FollowOverlayIntentV30.PrepareEndgame)
                return $"prepare_endgame_preserve_control_points_{points}_cost_{controlSpend}";

            return $"minimize_loss_keep_points_off_table_points_{points}_cost_{controlSpend}";
        }

        private static bool ShouldDowngradeFragileThirdHandTake(
            RuleAIContext context,
            CandidateMetrics metric)
        {
            if (!metric.CanBeatCurrentWinner ||
                metric.Security != FollowWinSecurityV30.Fragile ||
                context.DecisionFrame.PlayPosition != 3)
            {
                return false;
            }

            if (CountOpponentsBehind(context) <= 0)
                return false;

            bool recutThreatHigh = context.InferenceSnapshot.HighTrumpRiskByPlayer.Any(kv =>
                kv.Key != context.PlayerIndex &&
                kv.Key != context.DecisionFrame.CurrentWinningPlayer &&
                kv.Key % 2 != context.PlayerIndex % 2 &&
                kv.Value.Level >= RiskLevel.High &&
                kv.Value.Confidence >= 0.6);
            if (!recutThreatHigh)
                return false;

            return context.TrickScore >= 5 || metric.CandidatePoints > 0;
        }

        private static bool ShouldActivateTempoReclaim(
            RuleAIContext context,
            IReadOnlyList<CandidateMetrics> metrics)
        {
            if (context.PartnerWinning || context.TrickScore != 0 || !IsDealerSide(context))
                return false;

            if (context.DecisionFrame.PlayPosition < 3 && context.CardsLeftMin > 8)
                return false;

            int opponentsBehind = CountOpponentsBehind(context);
            return metrics.Any(metric =>
                metric.CanBeatCurrentWinner &&
                metric.CandidatePoints == 0 &&
                metric.ControlSpendCost <= 14 &&
                (metric.Security != FollowWinSecurityV30.Fragile || opponentsBehind == 0));
        }

        private static bool IsDealerSide(RuleAIContext context)
        {
            return context.Role == AIRole.Dealer || context.Role == AIRole.DealerPartner;
        }

        private static bool ShouldUsePrepareEndgameIntent(RuleAIContext context)
        {
            return IsDealerSide(context) &&
                context.CardsLeftMin <= 6 &&
                context.DecisionFrame.EndgameLevel != EndgameLevel.None;
        }

        private static bool HasHighRearOpponentThreat(
            RuleAIContext context,
            IReadOnlyList<Card> currentWinning)
        {
            int opponentsBehind = CountOpponentsBehind(context);
            if (opponentsBehind <= 0)
                return false;

            double threat = opponentsBehind;
            if (!currentWinning.Any(context.GameConfig.IsTrump))
                threat += 1.2;

            int winningControlCount = RuleAIUtility.CountHighControlCards(context.GameConfig, currentWinning);
            if (winningControlCount == 0)
                threat += 0.8;
            else if (winningControlCount == 1)
                threat += 0.3;

            foreach (int player in GetRemainingPlayersAfterCurrent(context))
            {
                if (IsTeammate(context.PlayerIndex, player))
                    continue;

                if (!context.InferenceSnapshot.HighTrumpRiskByPlayer.TryGetValue(player, out var risk))
                    continue;

                threat += risk.Level switch
                {
                    RiskLevel.High => 1.0,
                    RiskLevel.Medium => 0.6,
                    RiskLevel.Low => 0.2,
                    _ => 0
                };
            }

            return threat >= 2.2;
        }

        private static int CountOpponentsBehind(RuleAIContext context)
        {
            return GetRemainingPlayersAfterCurrent(context)
                .Count(player => !IsTeammate(context.PlayerIndex, player));
        }

        private static List<int> GetRemainingPlayersAfterCurrent(RuleAIContext context)
        {
            if (context.PlayerIndex < 0 || context.DecisionFrame.PlayPosition <= 0)
                return new List<int>();

            int remaining = Math.Max(0, 4 - context.DecisionFrame.PlayPosition);
            var players = new List<int>(remaining);
            int current = context.PlayerIndex;
            for (int offset = 1; offset <= remaining; offset++)
            {
                current = GetNextPlayerClockwise(current);
                players.Add(current);
            }

            return players;
        }

        private static int GetNextPlayerClockwise(int playerIndex)
        {
            return playerIndex switch
            {
                0 => 3,
                3 => 2,
                2 => 1,
                1 => 0,
                _ => ((playerIndex % 4) + 4) % 4
            };
        }

        private static bool IsTeammate(int myPlayerIndex, int otherPlayerIndex)
        {
            if (myPlayerIndex < 0 || otherPlayerIndex < 0)
                return false;

            return ((otherPlayerIndex - myPlayerIndex + 4) % 4) == 2;
        }
    }
}
