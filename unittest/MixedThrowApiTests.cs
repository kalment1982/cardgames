using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class MixedThrowApiTests
    {
        private static GameConfig BuildConfig()
        {
            return new GameConfig
            {
                LevelRank = Rank.Five,
                TrumpSuit = Suit.Spade
            };
        }

        private static Card C(Suit suit, Rank rank)
        {
            return new Card(suit, rank);
        }

        private static Game BuildPlayingGame(int seed = 1)
        {
            var game = new Game(seed: seed);
            game.StartGame();

            while (!game.IsDealingComplete)
            {
                Assert.True(game.DealNextCardEx().Success);
            }

            Assert.True(game.FinalizeTrumpEx(Suit.Spade).Success);
            Assert.True(game.BuryBottom(game.State.PlayerHands[0].Take(8).ToList()));

            game.CurrentTrick.Clear();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 0;
            return game;
        }

        [Fact]
        public void IsThrowSuccessful_ReturnsFalse_WhenThrowCardsAreNull()
        {
            var validator = new ThrowValidator(BuildConfig());
            Assert.False(validator.IsThrowSuccessful(null!, new List<List<Card>>()));
        }

        [Fact]
        public void IsThrowSuccessful_ReturnsFalse_WhenThrowCardsAreEmpty()
        {
            var validator = new ThrowValidator(BuildConfig());
            Assert.False(validator.IsThrowSuccessful(new List<Card>(), new List<List<Card>>()));
        }

        [Fact]
        public void IsThrowSuccessful_ReturnsFalse_WhenThrowCardsAreNotSameSuitOrTrump()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Club, Rank.King), C(Suit.Heart, Rank.Jack) };
            Assert.False(validator.IsThrowSuccessful(throwCards, new List<List<Card>>()));
        }

        [Fact]
        public void IsThrowSuccessful_ReturnsTrue_WhenFollowPlaysAreNull()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };
            Assert.True(validator.IsThrowSuccessful(throwCards, null!));
        }

        [Fact]
        public void AnalyzeThrow_IgnoresNullOrEmptyFollowerEntries()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };
            var followPlays = new List<List<Card>> { null!, new List<Card>(), new List<Card> { C(Suit.Club, Rank.Ace) } };

            var result = validator.AnalyzeThrow(throwCards, followPlays);
            Assert.True(result.Success);
            Assert.Null(result.Detail);
        }

        [Fact]
        public void AnalyzeThrow_Fails_WhenSingleComponentIsBlocked()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Jack) };
            var followPlays = new List<List<Card>> { new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Club, Rank.Ace) } };

            var result = validator.AnalyzeThrow(throwCards, followPlays);

            Assert.False(result.Success);
            Assert.NotNull(result.Detail);
            Assert.Equal("Single", Assert.IsType<string>(result.Detail!["blocked_component_type"]));
            Assert.Equal("Heart", Assert.IsType<string>(result.Detail["throw_suit_category"]));
        }

        [Fact]
        public void AnalyzeThrow_Fails_WhenPairComponentIsBlocked()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Ace) };
            var followPlays = new List<List<Card>> { new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King) } };

            var result = validator.AnalyzeThrow(throwCards, followPlays);

            Assert.False(result.Success);
            Assert.NotNull(result.Detail);
            Assert.Equal("Pair", Assert.IsType<string>(result.Detail!["blocked_component_type"]));
        }

        [Fact]
        public void AnalyzeThrow_Fails_WhenTractorComponentIsBlocked()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card>
            {
                C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Ten),
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Ace)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    C(Suit.Heart, Rank.Jack), C(Suit.Heart, Rank.Jack),
                    C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Ten)
                }
            };

            var result = validator.AnalyzeThrow(throwCards, followPlays);

            Assert.False(result.Success);
            Assert.NotNull(result.Detail);
            Assert.Equal("Tractor", Assert.IsType<string>(result.Detail!["blocked_component_type"]));
        }

        [Fact]
        public void AnalyzeThrow_UsesFirstBlockingFollowerIndex()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };
            var followPlays = new List<List<Card>>
            {
                new List<Card> { C(Suit.Heart, Rank.Ace) },
                new List<Card> { C(Suit.Heart, Rank.Ace), C(Suit.Heart, Rank.Ace) }
            };

            var result = validator.AnalyzeThrow(throwCards, followPlays);

            Assert.False(result.Success);
            Assert.NotNull(result.Detail);
            Assert.Equal(0, Assert.IsType<int>(result.Detail!["follower_hand_index"]));
            Assert.Equal("Single", Assert.IsType<string>(result.Detail["blocked_component_type"]));
        }

        [Fact]
        public void AnalyzeThrow_Succeeds_WhenFollowersOnlyMatchEqualStrength()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card>
            {
                C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Ace)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card> { C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Ace) }
            };

            var result = validator.AnalyzeThrow(throwCards, followPlays);

            Assert.True(result.Success);
            Assert.Null(result.Detail);
        }

        [Fact]
        public void AnalyzeThrow_Succeeds_WhenFollowerTractorLengthDoesNotMatch()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card>
            {
                C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Ten),
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Eight), C(Suit.Heart, Rank.Eight)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    C(Suit.Heart, Rank.Jack), C(Suit.Heart, Rank.Jack),
                    C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Ten)
                }
            };

            var result = validator.AnalyzeThrow(throwCards, followPlays);

            Assert.True(result.Success);
            Assert.Null(result.Detail);
        }

        [Fact]
        public void IsThrowSuccessful_AllTrumpMixedThrow_Succeeds_WhenNoHigherComponentExists()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card>
            {
                C(Suit.Joker, Rank.BigJoker),
                C(Suit.Joker, Rank.SmallJoker),
                C(Suit.Spade, Rank.Ace)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card> { C(Suit.Spade, Rank.King), C(Suit.Spade, Rank.Queen), C(Suit.Heart, Rank.Ace) }
            };

            Assert.True(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void CanBeatThrow_ReturnsFalse_WhenThrowInvalid()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Club, Rank.King), C(Suit.Heart, Rank.Jack) };
            var sameSuitCards = new List<Card> { C(Suit.Heart, Rank.Ace) };

            Assert.False(validator.CanBeatThrow(sameSuitCards, throwCards));
        }

        [Fact]
        public void CanBeatThrow_ReturnsTrue_WhenAnyComponentCanBeBeaten()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card>
            {
                C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Jack)
            };
            var sameSuitCards = new List<Card> { C(Suit.Heart, Rank.Ace) };

            Assert.True(validator.CanBeatThrow(sameSuitCards, throwCards));
        }

        [Fact]
        public void DecomposeThrow_PrioritizesTractorThenPairThenSingle()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card>
            {
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Eight), C(Suit.Heart, Rank.Eight),
                C(Suit.Heart, Rank.Six), C(Suit.Heart, Rank.Six),
                C(Suit.Heart, Rank.Ace)
            };

            var components = validator.DecomposeThrow(throwCards);

            Assert.Equal(3, components.Count);
            Assert.Equal(4, components[0].Count);
            Assert.Equal(2, components[1].Count);
            Assert.Single(components[2]);
            Assert.Contains(components[0], c => c.Rank == Rank.Nine);
            Assert.Contains(components[0], c => c.Rank == Rank.Eight);
            Assert.All(components[1], card => Assert.Equal(Rank.Six, card.Rank));
            Assert.Equal(Rank.Ace, components[2][0].Rank);
        }

        [Fact]
        public void DecomposeThrow_ReturnsEmpty_WhenThrowInvalid()
        {
            var validator = new ThrowValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Club, Rank.King), C(Suit.Heart, Rank.Jack) };

            var components = validator.DecomposeThrow(throwCards);

            Assert.Empty(components);
        }

        [Fact]
        public void GetFallbackPlay_PrefersSingleOverPairAndTractor()
        {
            var validator = new ThrowValidator(BuildConfig());
            var cards = new List<Card>
            {
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Eight), C(Suit.Heart, Rank.Eight),
                C(Suit.Heart, Rank.Six), C(Suit.Heart, Rank.Six),
                C(Suit.Heart, Rank.Ace)
            };

            var fallback = validator.GetFallbackPlay(cards);

            Assert.Single(fallback);
            Assert.Equal(Rank.Ace, fallback[0].Rank);
        }

        [Fact]
        public void GetFallbackPlay_ReturnsSmallestPair_WhenNoSingle()
        {
            var validator = new ThrowValidator(BuildConfig());
            var cards = new List<Card>
            {
                C(Suit.Heart, Rank.Ace), C(Suit.Heart, Rank.Ace),
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Seven), C(Suit.Heart, Rank.Seven)
            };

            var fallback = validator.GetFallbackPlay(cards);

            Assert.Equal(2, fallback.Count);
            Assert.All(fallback, card => Assert.Equal(Rank.Seven, card.Rank));
        }

        [Fact]
        public void GetFallbackPlay_ReturnsSmallestTractor_WhenOnlyTractors()
        {
            var validator = new ThrowValidator(BuildConfig());
            var cards = new List<Card>
            {
                C(Suit.Heart, Rank.Ace), C(Suit.Heart, Rank.Ace),
                C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King),
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Eight), C(Suit.Heart, Rank.Eight)
            };

            var fallback = validator.GetFallbackPlay(cards);

            Assert.Equal(4, fallback.Count);
            Assert.Equal(2, fallback.Count(card => card.Rank == Rank.Eight));
            Assert.Equal(2, fallback.Count(card => card.Rank == Rank.Nine));
        }

        [Fact]
        public void GetSmallestCard_ReturnsNull_WhenFallbackEmpty()
        {
            var validator = new ThrowValidator(BuildConfig());
            var cards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Club, Rank.King) };

            var smallest = validator.GetSmallestCard(cards);

            Assert.Null(smallest);
        }

        [Fact]
        public void GetSmallestCard_ReturnsSmallestCardFromFallback()
        {
            var validator = new ThrowValidator(BuildConfig());
            var cards = new List<Card> { C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Jack) };

            var smallest = validator.GetSmallestCard(cards);

            Assert.NotNull(smallest);
            Assert.Equal(C(Suit.Heart, Rank.Jack), smallest);
        }

        [Fact]
        public void IsValidPlayEx_MixedThrow_Succeeds_WhenNoFollowerCanBeatAnyComponent()
        {
            var validator = new PlayValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Queen) };
            var hand = throwCards.ToList();
            var otherHands = new List<List<Card>>
            {
                new List<Card> { C(Suit.Heart, Rank.Jack), C(Suit.Heart, Rank.Jack), C(Suit.Heart, Rank.Ten) },
                new List<Card> { C(Suit.Club, Rank.Ace) }
            };

            var result = validator.IsValidPlayEx(hand, throwCards, otherHands);

            Assert.True(result.Success);
            Assert.Null(result.ReasonCode);
        }

        [Fact]
        public void IsValidPlayEx_MixedThrow_FailsWithThrowNotMax_WhenSingleComponentIsBeaten()
        {
            var validator = new PlayValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };
            var hand = throwCards.ToList();
            var otherHands = new List<List<Card>> { new List<Card> { C(Suit.Heart, Rank.Ace), C(Suit.Club, Rank.Two) } };

            var result = validator.IsValidPlayEx(hand, throwCards, otherHands);

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.ThrowNotMax, result.ReasonCode);
            Assert.NotNull(result.Detail);
            Assert.Equal("Single", Assert.IsType<string>(result.Detail!["blocked_component_type"]));
        }

        [Fact]
        public void IsValidPlayEx_MixedThrow_FailsWithThrowNotMax_WhenPairComponentIsBeaten()
        {
            var validator = new PlayValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Ace) };
            var hand = throwCards.ToList();
            var otherHands = new List<List<Card>> { new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King) } };

            var result = validator.IsValidPlayEx(hand, throwCards, otherHands);

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.ThrowNotMax, result.ReasonCode);
            Assert.NotNull(result.Detail);
            Assert.Equal("Pair", Assert.IsType<string>(result.Detail!["blocked_component_type"]));
        }

        [Fact]
        public void IsValidPlayEx_MixedThrow_FailsWithThrowNotMax_WhenTractorComponentIsBeaten()
        {
            var validator = new PlayValidator(BuildConfig());
            var throwCards = new List<Card>
            {
                C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Ten),
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Ace)
            };
            var hand = throwCards.ToList();
            var otherHands = new List<List<Card>>
            {
                new List<Card>
                {
                    C(Suit.Heart, Rank.Jack), C(Suit.Heart, Rank.Jack),
                    C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Ten)
                }
            };

            var result = validator.IsValidPlayEx(hand, throwCards, otherHands);

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.ThrowNotMax, result.ReasonCode);
            Assert.NotNull(result.Detail);
            Assert.Equal("Tractor", Assert.IsType<string>(result.Detail!["blocked_component_type"]));
        }

        [Fact]
        public void IsValidPlayEx_MixedThrow_Succeeds_WhenOtherHandsNotProvided()
        {
            var validator = new PlayValidator(BuildConfig());
            var throwCards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };
            var hand = throwCards.ToList();

            var result = validator.IsValidPlayEx(hand, throwCards, null!);

            Assert.True(result.Success);
            Assert.Null(result.ReasonCode);
        }

        [Fact]
        public void IsValidPlayEx_Pair_BypassesThrowValidation()
        {
            var validator = new PlayValidator(BuildConfig());
            var cardsToPlay = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King) };
            var hand = cardsToPlay.ToList();
            var otherHands = new List<List<Card>> { new List<Card> { C(Suit.Heart, Rank.Ace), C(Suit.Heart, Rank.Ace) } };

            var result = validator.IsValidPlayEx(hand, cardsToPlay, otherHands);

            Assert.True(result.Success);
            Assert.Null(result.ReasonCode);
        }

        [Fact]
        public void IsValidPlayEx_Tractor_BypassesThrowValidation()
        {
            var validator = new PlayValidator(BuildConfig());
            var cardsToPlay = new List<Card>
            {
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Eight), C(Suit.Heart, Rank.Eight)
            };
            var hand = cardsToPlay.ToList();
            var otherHands = new List<List<Card>>
            {
                new List<Card>
                {
                    C(Suit.Heart, Rank.Ace), C(Suit.Heart, Rank.Ace),
                    C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King)
                }
            };

            var result = validator.IsValidPlayEx(hand, cardsToPlay, otherHands);

            Assert.True(result.Success);
            Assert.Null(result.ReasonCode);
        }

        [Fact]
        public void IsValidPlayEx_FailsWithPlayPatternInvalid_WhenCardsNotSameSuitOrTrump()
        {
            var validator = new PlayValidator(BuildConfig());
            var cardsToPlay = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Club, Rank.King), C(Suit.Heart, Rank.Jack) };
            var hand = cardsToPlay.ToList();

            var result = validator.IsValidPlayEx(hand, cardsToPlay, new List<List<Card>>());

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.PlayPatternInvalid, result.ReasonCode);
        }

        [Fact]
        public void IsValidPlayEx_FailsWithCardNotInHand()
        {
            var validator = new PlayValidator(BuildConfig());
            var hand = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King) };
            var cardsToPlay = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };

            var result = validator.IsValidPlayEx(hand, cardsToPlay, new List<List<Card>>());

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.CardNotInHand, result.ReasonCode);
        }

        [Fact]
        public void IsValidPlayEx_FailsWithPlayPatternInvalid_WhenCardsEmpty()
        {
            var validator = new PlayValidator(BuildConfig());
            var result = validator.IsValidPlayEx(new List<Card>(), new List<Card>(), new List<List<Card>>());

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.PlayPatternInvalid, result.ReasonCode);
        }

        [Fact]
        public void ValidatePattern_ReturnsTrue_ForSameSuitNonPairTwoCards()
        {
            var validator = new PlayValidator(BuildConfig());
            var cards = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Queen) };

            Assert.True(validator.ValidatePattern(cards));
        }

        [Fact]
        public void ValidatePattern_ReturnsTrue_ForMixedSameSuitThrowAttempt()
        {
            var validator = new PlayValidator(BuildConfig());
            var cards = new List<Card>
            {
                C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack)
            };

            Assert.True(validator.ValidatePattern(cards));
        }

        [Fact]
        public void Game_PlayCardsEx_MixedThrowBlocked_FallsBackToSmallestSingle()
        {
            var game = BuildPlayingGame(seed: 11);
            var throwAttempt = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };

            game.State.PlayerHands[0] = throwAttempt.ToList();
            game.State.PlayerHands[1] = new List<Card> { C(Suit.Heart, Rank.Ace), C(Suit.Club, Rank.Two) };
            game.State.PlayerHands[2] = new List<Card> { C(Suit.Diamond, Rank.Ace) };
            game.State.PlayerHands[3] = new List<Card> { C(Suit.Spade, Rank.Three) };
            game.State.DefenderScore = 0;

            var result = game.PlayCardsEx(0, throwAttempt);

            Assert.True(result.Success);
            Assert.Single(game.CurrentTrick);
            Assert.Single(game.CurrentTrick[0].Cards);
            Assert.Equal(C(Suit.Heart, Rank.Jack), game.CurrentTrick[0].Cards[0]);
            Assert.Equal(2, game.State.PlayerHands[0].Count);
            Assert.All(game.State.PlayerHands[0], card => Assert.Equal(Rank.King, card.Rank));
            Assert.Equal(0, game.State.DefenderScore);
        }

        [Fact]
        public void Game_PlayCardsEx_MixedThrowBlocked_FallsBackToSmallestPair_WhenNoSingle()
        {
            var game = BuildPlayingGame(seed: 12);
            var throwAttempt = new List<Card>
            {
                C(Suit.Heart, Rank.Ace), C(Suit.Heart, Rank.Ace),
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Seven), C(Suit.Heart, Rank.Seven)
            };

            game.State.PlayerHands[0] = throwAttempt.ToList();
            game.State.PlayerHands[1] = new List<Card> { C(Suit.Heart, Rank.Eight), C(Suit.Heart, Rank.Eight) };
            game.State.PlayerHands[2] = new List<Card> { C(Suit.Diamond, Rank.Ace) };
            game.State.PlayerHands[3] = new List<Card> { C(Suit.Club, Rank.King) };

            var result = game.PlayCardsEx(0, throwAttempt);
            var played = game.CurrentTrick[0].Cards;

            Assert.True(result.Success);
            Assert.Equal(2, played.Count);
            Assert.All(played, card => Assert.Equal(Rank.Seven, card.Rank));
            Assert.Equal(4, game.State.PlayerHands[0].Count);
            Assert.Equal(2, game.State.PlayerHands[0].Count(card => card.Rank == Rank.Ace));
            Assert.Equal(2, game.State.PlayerHands[0].Count(card => card.Rank == Rank.Nine));
        }

        [Fact]
        public void Game_PlayCardsEx_MixedThrowBlocked_FallsBackToSmallestTractor_WhenNoSingleOrPair()
        {
            var game = BuildPlayingGame(seed: 13);
            var throwAttempt = new List<Card>
            {
                C(Suit.Heart, Rank.Ace), C(Suit.Heart, Rank.Ace),
                C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King),
                C(Suit.Heart, Rank.Nine), C(Suit.Heart, Rank.Nine),
                C(Suit.Heart, Rank.Eight), C(Suit.Heart, Rank.Eight)
            };

            game.State.PlayerHands[0] = throwAttempt.ToList();
            game.State.PlayerHands[1] = new List<Card>
            {
                C(Suit.Heart, Rank.Queen), C(Suit.Heart, Rank.Queen),
                C(Suit.Heart, Rank.Jack), C(Suit.Heart, Rank.Jack)
            };
            game.State.PlayerHands[2] = new List<Card> { C(Suit.Diamond, Rank.Ace) };
            game.State.PlayerHands[3] = new List<Card> { C(Suit.Club, Rank.King) };

            var result = game.PlayCardsEx(0, throwAttempt);
            var played = game.CurrentTrick[0].Cards;

            Assert.True(result.Success);
            Assert.Equal(4, played.Count);
            Assert.Equal(2, played.Count(card => card.Rank == Rank.Eight));
            Assert.Equal(2, played.Count(card => card.Rank == Rank.Nine));
            Assert.Equal(4, game.State.PlayerHands[0].Count);
            Assert.Equal(2, game.State.PlayerHands[0].Count(card => card.Rank == Rank.Ace));
            Assert.Equal(2, game.State.PlayerHands[0].Count(card => card.Rank == Rank.King));
        }

        [Fact]
        public void Game_PlayCardsEx_MixedThrowSuccess_PlaysOriginalCards()
        {
            var game = BuildPlayingGame(seed: 14);
            var throwAttempt = new List<Card> { C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.King), C(Suit.Heart, Rank.Jack) };

            game.State.PlayerHands[0] = throwAttempt.ToList();
            game.State.PlayerHands[1] = new List<Card> { C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Ten), C(Suit.Heart, Rank.Nine) };
            game.State.PlayerHands[2] = new List<Card> { C(Suit.Diamond, Rank.Ace) };
            game.State.PlayerHands[3] = new List<Card> { C(Suit.Club, Rank.King) };

            var result = game.PlayCardsEx(0, throwAttempt);
            var played = game.CurrentTrick[0].Cards;

            Assert.True(result.Success);
            Assert.Equal(3, played.Count);
            Assert.Equal(2, played.Count(card => card.Rank == Rank.King));
            Assert.Equal(1, played.Count(card => card.Rank == Rank.Jack));
            Assert.Empty(game.State.PlayerHands[0]);
        }
    }
}
