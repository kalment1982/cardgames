using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Follow
{
    public sealed class FollowCandidateOverlayV30
    {
        public List<FollowCandidateViewV30> BuildAndRank(
            RuleAIContext context,
            IReadOnlyList<List<Card>> legalCandidates)
        {
            var comparer = new CardComparer(context.GameConfig);
            var currentWinning = context.CurrentWinningCards.Count > 0
                ? context.CurrentWinningCards
                : context.LeadCards;

            var views = legalCandidates
                .Where(candidate => candidate != null && candidate.Count == context.LeadCards.Count)
                .Select(candidate => BuildView(context, currentWinning, candidate, comparer))
                .ToList();

            return views
                .OrderByDescending(view => view.OverlayScore)
                .ThenBy(view => view.ControlSpendCost)
                .ThenBy(view => view.CanBeatCurrentWinner ? view.CandidatePoints : 0)
                .ThenBy(view => view.CandidatePoints)
                .ThenBy(view => RuleAIUtility.BuildCandidateKey(view.Cards))
                .ToList();
        }

        public FollowOverlayIntentV30 ResolveIntent(RuleAIContext context, IReadOnlyList<FollowCandidateViewV30> ranked)
        {
            if (context.PartnerWinning)
                return FollowOverlayIntentV30.PassToMate;

            if (ranked.Count == 0)
                return FollowOverlayIntentV30.MinimizeLoss;

            return ranked[0].CanBeatCurrentWinner
                ? FollowOverlayIntentV30.TakeScore
                : FollowOverlayIntentV30.MinimizeLoss;
        }

        private FollowCandidateViewV30 BuildView(
            RuleAIContext context,
            List<Card> currentWinning,
            List<Card> candidate,
            CardComparer comparer)
        {
            bool canBeat = RuleAIUtility.CanBeatCards(context.GameConfig, currentWinning, candidate);
            int points = candidate.Sum(card => card.Score);
            int controlSpend = EstimateControlSpendCost(context, candidate, comparer);
            int highControlCount = RuleAIUtility.CountHighControlCards(context.GameConfig, candidate);
            int margin = canBeat
                ? RuleAIUtility.CalculateWinMargin(context.GameConfig, candidate, currentWinning, comparer)
                : 0;

            var security = ResolveSecurity(margin);
            bool rearOpponentThreatHigh = context.PartnerWinning &&
                HasHighRearOpponentThreat(context, currentWinning);
            int score = ScoreCandidate(
                context,
                canBeat,
                security,
                points,
                controlSpend,
                highControlCount,
                rearOpponentThreatHigh);
            string reason = BuildReason(context, canBeat, security, points, controlSpend, rearOpponentThreatHigh);

            return new FollowCandidateViewV30
            {
                Cards = new List<Card>(candidate),
                CanBeatCurrentWinner = canBeat,
                Security = security,
                OverlayScore = score,
                ControlSpendCost = controlSpend,
                CandidatePoints = points,
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

        private static int ScoreCandidate(
            RuleAIContext context,
            bool canBeat,
            FollowWinSecurityV30 security,
            int points,
            int controlSpend,
            int highControlCount,
            bool rearOpponentThreatHigh)
        {
            int score = 0;
            if (context.PartnerWinning)
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
                return score;
            }

            score -= context.TrickScore * 2;
            score -= points * (context.TrickScore >= 5 ? 8 : 6);
            score -= controlSpend;
            if (points == 0)
                score += context.TrickScore >= 5 ? 18 : 10;
            return score;
        }

        private static string BuildReason(
            RuleAIContext context,
            bool canBeat,
            FollowWinSecurityV30 security,
            int points,
            int controlSpend,
            bool rearOpponentThreatHigh)
        {
            string securityText = security.ToString().ToLowerInvariant();
            if (context.PartnerWinning)
            {
                if (canBeat)
                    return $"pass_to_mate_avoid_overtake_failed_points_{points}_cost_{controlSpend}";

                return rearOpponentThreatHigh
                    ? $"pass_to_mate_hold_points_vs_rear_threat_points_{points}_cost_{controlSpend}"
                    : $"pass_to_mate_points_{points}_cost_{controlSpend}";
            }

            if (canBeat)
                return $"take_score_{securityText}_points_{points}_cost_{controlSpend}";

            return $"minimize_loss_keep_points_off_table_points_{points}_cost_{controlSpend}";
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
