using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    public enum WinSecurityLevel
    {
        None = 0,
        Fragile = 1,
        Stable = 2,
        Lock = 3
    }

    public sealed class FollowThreatAssessment
    {
        public bool CanBeat { get; init; }

        public WinSecurityLevel SecurityLevel { get; init; } = WinSecurityLevel.None;

        public double WinSecurityValue { get; init; }

        public double PointProtectionValue { get; init; }

        public double FuturePointRisk { get; init; }

        public double BehindOpponentThreat { get; init; }

        public int BehindOpponentCount { get; init; }

        public int BehindTeammateCount { get; init; }

        public int StrongerThreatCount { get; init; }

        public int WinMargin { get; init; }
    }

    internal sealed class FollowThreatAnalyzer
    {
        private readonly GameConfig _config;

        public FollowThreatAnalyzer(GameConfig config)
        {
            _config = config;
        }

        public FollowThreatAssessment Analyze(
            RuleAIContext context,
            List<Card> candidate,
            List<Card> currentWinningCards)
        {
            if (candidate == null || currentWinningCards == null || candidate.Count != currentWinningCards.Count)
                return new FollowThreatAssessment();

            bool canBeat = currentWinningCards.Count > 0 &&
                RuleAIUtility.CanBeatCards(_config, currentWinningCards, candidate);
            if (!canBeat)
                return new FollowThreatAssessment();

            var remainingPlayers = GetRemainingPlayersAfterCurrent(context);
            int behindOpponents = remainingPlayers.Count(player => !IsTeammate(context.PlayerIndex, player));
            int behindTeammates = remainingPlayers.Count - behindOpponents;
            int winMargin = RuleAIUtility.CalculateWinMargin(_config, candidate, currentWinningCards, new CardComparer(_config));
            int strongerThreatCount = CountStrongerThreats(context, candidate, currentWinningCards);
            double behindThreat = CalculateBehindOpponentThreat(context, remainingPlayers, behindOpponents, currentWinningCards);
            double currentPot = (context.TrickScore + candidate.Sum(card => card.Score)) / 10.0;
            var securityLevel = ResolveSecurityLevel(
                candidate,
                currentWinningCards,
                behindOpponents,
                strongerThreatCount,
                winMargin);

            double fragilityFactor = securityLevel switch
            {
                WinSecurityLevel.Lock => 0.0,
                WinSecurityLevel.Stable => 0.45,
                WinSecurityLevel.Fragile => 1.15,
                _ => 1.40
            };

            double pointProtection = currentPot;
            if (securityLevel >= WinSecurityLevel.Stable)
                pointProtection += 0.8;
            if (securityLevel == WinSecurityLevel.Lock)
                pointProtection += 0.5;

            double futurePointRisk = behindThreat * currentPot * fragilityFactor;
            if (behindOpponents > 0 && securityLevel == WinSecurityLevel.Fragile)
                futurePointRisk += behindOpponents * 0.6;

            return new FollowThreatAssessment
            {
                CanBeat = true,
                SecurityLevel = securityLevel,
                WinSecurityValue = (double)securityLevel,
                PointProtectionValue = Math.Round(pointProtection, 4),
                FuturePointRisk = Math.Round(futurePointRisk, 4),
                BehindOpponentThreat = Math.Round(behindThreat, 4),
                BehindOpponentCount = behindOpponents,
                BehindTeammateCount = behindTeammates,
                StrongerThreatCount = strongerThreatCount,
                WinMargin = winMargin
            };
        }

        public int CountOpponentsBehind(RuleAIContext context)
        {
            return GetRemainingPlayersAfterCurrent(context)
                .Count(player => !IsTeammate(context.PlayerIndex, player));
        }

        private WinSecurityLevel ResolveSecurityLevel(
            List<Card> candidate,
            List<Card> currentWinningCards,
            int behindOpponents,
            int strongerThreatCount,
            int winMargin)
        {
            if (behindOpponents <= 0)
                return WinSecurityLevel.Lock;

            if (strongerThreatCount == 0)
                return WinSecurityLevel.Lock;

            if (candidate.Count == 1)
            {
                int requiredMargin = 1 + behindOpponents * 2 + Math.Min(3, strongerThreatCount);
                if (winMargin >= 180)
                    return WinSecurityLevel.Lock;
                if (winMargin >= requiredMargin)
                    return WinSecurityLevel.Stable;
                return WinSecurityLevel.Fragile;
            }

            if (winMargin >= 120)
                return WinSecurityLevel.Lock;
            if (winMargin >= 45)
                return WinSecurityLevel.Stable;
            return WinSecurityLevel.Fragile;
        }

        private double CalculateBehindOpponentThreat(
            RuleAIContext context,
            List<int> remainingPlayers,
            int behindOpponents,
            List<Card> currentWinningCards)
        {
            if (behindOpponents <= 0)
                return 0;

            double threat = behindOpponents;
            foreach (var player in remainingPlayers.Where(player => !IsTeammate(context.PlayerIndex, player)))
            {
                if (context.InferenceSnapshot.HighTrumpRiskByPlayer.TryGetValue(player, out var risk))
                {
                    threat += risk.Level switch
                    {
                        RiskLevel.High => 0.75,
                        RiskLevel.Medium => 0.35,
                        RiskLevel.Low => 0.10,
                        _ => 0
                    };
                }

                if (currentWinningCards.Count > 0)
                {
                    var lead = currentWinningCards[0];
                    if (!_config.IsTrump(lead) &&
                        context.MemorySnapshot.VoidSuitsByPlayer.TryGetValue(player, out var voids) &&
                        voids.Contains(lead.Suit.ToString()))
                    {
                        threat += 0.6;
                    }
                }
            }

            return threat;
        }

        private int CountStrongerThreats(
            RuleAIContext context,
            List<Card> candidate,
            List<Card> currentWinningCards)
        {
            if (candidate.Count != 1 || currentWinningCards.Count != 1)
                return 0;

            var leadCards = context.LeadCards.Count > 0 ? context.LeadCards : currentWinningCards;
            var leadCard = leadCards[0];
            var leadCategory = _config.GetCardCategory(leadCard);
            var leadSuit = leadCard.Suit;
            var unknownCards = BuildUnknownCards(context);

            return unknownCards.Count(card =>
                IsPotentialThreat(card, candidate[0], leadSuit, leadCategory));
        }

        private bool IsPotentialThreat(Card card, Card candidate, Suit leadSuit, CardCategory leadCategory)
        {
            if (leadCategory == CardCategory.Trump)
            {
                return _config.IsTrump(card) &&
                    RuleAIUtility.CanBeatCards(_config, new List<Card> { candidate }, new List<Card> { card });
            }

            if (RuleAIUtility.MatchesLeadCategory(_config, card, leadSuit, leadCategory) &&
                RuleAIUtility.CanBeatCards(_config, new List<Card> { candidate }, new List<Card> { card }))
            {
                return true;
            }

            return _config.IsTrump(card);
        }

        private List<Card> BuildUnknownCards(RuleAIContext context)
        {
            var unknown = EnumerateDeck().ToList();

            RemoveKnownCards(unknown, context.MyHand);
            RemoveKnownCards(unknown, context.VisibleBottomCards);
            RemoveKnownCards(unknown, context.LeadCards);
            if (context.CurrentWinningCards.Count > 0)
                RemoveKnownCards(unknown, context.CurrentWinningCards);

            foreach (var entry in context.MemorySnapshot.PlayedCountByCard)
            {
                for (int i = 0; i < entry.Value; i++)
                {
                    int index = unknown.FindIndex(card => card.ToString() == entry.Key);
                    if (index < 0)
                        break;
                    unknown.RemoveAt(index);
                }
            }

            return unknown;
        }

        private static void RemoveKnownCards(List<Card> unknown, IEnumerable<Card> cards)
        {
            foreach (var card in cards)
            {
                int index = unknown.FindIndex(existing => existing.Equals(card));
                if (index >= 0)
                    unknown.RemoveAt(index);
            }
        }

        private static IEnumerable<Card> EnumerateDeck()
        {
            for (int deck = 0; deck < 2; deck++)
            {
                yield return new Card(Suit.Joker, Rank.BigJoker);
                yield return new Card(Suit.Joker, Rank.SmallJoker);

                foreach (Suit suit in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
                {
                    foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    {
                        if (rank == Rank.SmallJoker || rank == Rank.BigJoker)
                            continue;

                        yield return new Card(suit, rank);
                    }
                }
            }
        }

        private List<int> GetRemainingPlayersAfterCurrent(RuleAIContext context)
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
