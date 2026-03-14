using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.GameFlow
{
    /// <summary>
    /// 游戏主控制器
    /// </summary>
    public class Game
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly Deck _deck;
        private DealingPhase? _dealing;
        private TrumpBidding? _bidding;
        private BottomBurying? _burying;
        private readonly TrickJudge _judge;
        private readonly ScoreCalculator _scoreCalc;
        private readonly IGameLogger _logger;
        private readonly Stopwatch _roundTimer;
        private readonly int _seed;
        private readonly string _sessionId;
        private readonly string _gameId;
        private readonly string _roundId;

        private List<TrickPlay> _currentTrick;
        private int _trickLeader;
        private List<Card>? _lastTrickCards; // 最后一墩的牌（用于抠底计算）
        private int _trickNo;
        private int _turnNo;
        private DealStepResult? _lastDealStep;

        public GameState State => _state;
        public List<TrickPlay> CurrentTrick => _currentTrick;
        public string SessionId => _sessionId;
        public string GameId => _gameId;
        public string RoundId => _roundId;
        public bool IsDealingComplete => _dealing != null && _dealing.IsComplete;
        public DealStepResult? LastDealStep => _lastDealStep;

        public Game(
            int seed = 0,
            IGameLogger? logger = null,
            string? sessionId = null,
            string? gameId = null,
            string? roundId = null)
        {
            _seed = seed;
            _logger = logger ?? GameLoggerFactory.CreateDefault();
            _sessionId = sessionId ?? GenerateId("sess");
            _gameId = gameId ?? GenerateId("game");
            _roundId = roundId ?? GenerateId("round");

            _state = new GameState
            {
                DealerIndex = 0,
                LevelRank = Rank.Two,
                Phase = GamePhase.Dealing
            };
            _config = new GameConfig { LevelRank = Rank.Two };
            _deck = seed > 0 ? new Deck(seed) : new Deck();
            _judge = new TrickJudge(_config);
            _scoreCalc = new ScoreCalculator(_config);
            _currentTrick = new List<TrickPlay>();
            _roundTimer = new Stopwatch();
        }

        public void StartGame()
        {
            _roundTimer.Restart();
            _trickNo = 0;
            _turnNo = 0;

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "game.start",
                "system",
                new Dictionary<string, object?>
                {
                    ["seed"] = _seed,
                    ["dealer_index"] = _state.DealerIndex,
                    ["level_rank"] = _state.LevelRank.ToString()
                });

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "dealing.start",
                "system",
                new Dictionary<string, object?>
                {
                    ["deck_count"] = _deck.RemainingCards
                });

            // 发牌
            _dealing = new DealingPhase(_deck);
            for (int i = 0; i < 4; i++)
            {
                _state.PlayerHands[i].Clear();
            }
            _state.BuriedCards.Clear();
            _state.TrumpSuit = null;
            _state.DefenderScore = 0;
            _state.CurrentPlayer = _state.DealerIndex;
            _lastDealStep = null;

            var from = _state.Phase;
            _state.Phase = GamePhase.Bidding;
            LogPhaseTransition(from, _state.Phase);
        }

        public bool BidTrump(int playerIndex, List<Card> cards)
        {
            return BidTrumpEx(playerIndex, cards).Success;
        }

        public OperationResult DealNextCardEx()
        {
            if (_state.Phase != GamePhase.Bidding)
                return OperationResult.Fail(ReasonCodes.PhaseInvalid);

            if (_dealing == null)
                return OperationResult.Fail(ReasonCodes.UnknownError);

            if (_dealing.IsComplete)
                return OperationResult.Fail(ReasonCodes.DealingAlreadyComplete);

            var step = _dealing.DealNext();
            _lastDealStep = step;
            if (!step.IsBottomCard && step.PlayerIndex >= 0 && step.PlayerIndex < _state.PlayerHands.Length)
            {
                _state.PlayerHands[step.PlayerIndex].Add(step.Card);
            }

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "dealing.step",
                step.IsBottomCard ? "system" : $"player_{step.PlayerIndex}",
                new Dictionary<string, object?>
                {
                    ["step_index"] = step.StepIndex,
                    ["is_bottom_card"] = step.IsBottomCard,
                    ["player_index"] = step.IsBottomCard ? -1 : step.PlayerIndex,
                    ["player_card_count"] = step.PlayerCardCount,
                    ["remaining_deck"] = step.RemainingDeck
                });

            if (_dealing.IsComplete)
            {
                LogEvent(
                    LogCategories.Audit,
                    LogLevels.Info,
                    "dealing.complete",
                    "system",
                    new Dictionary<string, object?>
                    {
                        ["hands_count"] = _state.PlayerHands.Select(h => h.Count).ToArray(),
                        ["bottom_count"] = _dealing.GetBottomCards().Count
                    });
            }

            return OperationResult.Ok;
        }

        public OperationResult BidTrumpEx(int playerIndex, List<Card> cards)
        {
            var attemptCards = cards ?? new List<Card>();
            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "trump.bid.attempt",
                $"player_{playerIndex}",
                new Dictionary<string, object?>
                {
                    ["player_index"] = playerIndex,
                    ["cards"] = SerializeCards(attemptCards),
                    ["bid_type"] = GetBidType(attemptCards)
                });

            if (_state.Phase != GamePhase.Bidding)
            {
                LogTrumpBidReject(playerIndex, attemptCards, ReasonCodes.PhaseInvalid);
                return OperationResult.Fail(ReasonCodes.PhaseInvalid);
            }

            _bidding ??= new TrumpBidding();
            var result = _bidding.TryBidEx(playerIndex, _state.LevelRank, attemptCards);
            if (!result.Success)
            {
                LogTrumpBidReject(playerIndex, attemptCards, result.ReasonCode ?? ReasonCodes.UnknownError);
                return result;
            }

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "trump.bid.accept",
                $"player_{playerIndex}",
                new Dictionary<string, object?>
                {
                    ["player_index"] = playerIndex,
                    ["trump_suit"] = _bidding.TrumpSuit?.ToString() ?? "Spade",
                    ["bid_priority"] = GetBidPriority(attemptCards)
                });

            return OperationResult.Ok;
        }

        public void FinalizeTrump(Suit? trumpSuit = null)
        {
            FinalizeTrumpEx(trumpSuit);
        }

        public OperationResult FinalizeTrumpEx(Suit? trumpSuit = null)
        {
            if (_state.Phase != GamePhase.Bidding)
                return OperationResult.Fail(ReasonCodes.PhaseInvalid);

            if (!IsDealingComplete)
                return OperationResult.Fail(ReasonCodes.DealingNotComplete);

            if (trumpSuit.HasValue)
            {
                _state.TrumpSuit = trumpSuit;
            }
            else if (_bidding != null && _bidding.TrumpSuit.HasValue)
            {
                _state.TrumpSuit = _bidding.TrumpSuit;
            }
            else
            {
                _state.TrumpSuit = Suit.Spade; // 默认黑桃
            }

            _config.TrumpSuit = _state.TrumpSuit;

            if (_dealing == null)
                return OperationResult.Fail(ReasonCodes.UnknownError);

            // 底牌加入庄家手牌（让庄家看到完整33张）
            var bottom = _dealing.GetBottomCards();
            _state.PlayerHands[_state.DealerIndex].AddRange(bottom);

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "trump.finalized",
                "system",
                new Dictionary<string, object?>
                {
                    ["trump_suit"] = _state.TrumpSuit?.ToString() ?? Suit.Spade.ToString(),
                    ["trump_player"] = _bidding?.TrumpPlayer ?? _state.DealerIndex,
                    ["is_no_trump"] = false
                });

            var from = _state.Phase;
            _state.Phase = GamePhase.Burying;
            LogPhaseTransition(from, _state.Phase);

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "bury.start",
                "system",
                new Dictionary<string, object?>
                {
                    ["dealer_index"] = _state.DealerIndex,
                    ["dealer_hand_count_before_bottom"] = _state.PlayerHands[_state.DealerIndex].Count
                });

            return OperationResult.Ok;
        }

        public bool BuryBottom(List<Card> cardsToBury)
        {
            return BuryBottomEx(cardsToBury).Success;
        }

        public OperationResult BuryBottomEx(List<Card> cardsToBury)
        {
            var selectedCards = cardsToBury ?? new List<Card>();
            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "bury.attempt",
                $"player_{_state.DealerIndex}",
                new Dictionary<string, object?>
                {
                    ["dealer_index"] = _state.DealerIndex,
                    ["selected_cards"] = SerializeCards(selectedCards)
                });

            if (_state.Phase != GamePhase.Burying)
            {
                LogBuryReject(selectedCards, ReasonCodes.PhaseInvalid);
                return OperationResult.Fail(ReasonCodes.PhaseInvalid);
            }

            if (cardsToBury == null || cardsToBury.Count != 8)
            {
                LogBuryReject(selectedCards, ReasonCodes.BuryNot8Cards);
                return OperationResult.Fail(ReasonCodes.BuryNot8Cards);
            }

            if (_dealing == null)
            {
                LogBuryReject(selectedCards, ReasonCodes.UnknownError);
                return OperationResult.Fail(ReasonCodes.UnknownError);
            }

            var bottom = _dealing.GetBottomCards();
            _burying = new BottomBurying(bottom);

            // 校验扣底是否合法
            var buryResult = _burying.BuryCardsEx(_state.PlayerHands[_state.DealerIndex], cardsToBury);
            if (!buryResult.Success)
            {
                LogBuryReject(selectedCards, buryResult.ReasonCode ?? ReasonCodes.UnknownError);
                return buryResult;
            }

            _state.BuriedCards = _burying.BuriedCards;

            // 从庄家手牌中移除扣底的牌
            foreach (var card in cardsToBury)
            {
                _state.PlayerHands[_state.DealerIndex].Remove(card);
            }

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "bury.accept",
                $"player_{_state.DealerIndex}",
                new Dictionary<string, object?>
                {
                    ["dealer_index"] = _state.DealerIndex,
                    ["buried_cards"] = SerializeCards(_state.BuriedCards),
                    ["dealer_hand_count_after"] = _state.PlayerHands[_state.DealerIndex].Count
                });

            var from = _state.Phase;
            _state.Phase = GamePhase.Playing;
            _state.CurrentPlayer = _state.DealerIndex;
            _trickLeader = _state.DealerIndex;
            _trickNo = 1;
            _turnNo = 1;

            LogPhaseTransition(from, _state.Phase);
            LogTurnStart(_state.CurrentPlayer, isLead: true);
            return OperationResult.Ok;
        }

        public bool PlayCards(int playerIndex, List<Card> cards)
        {
            return PlayCardsEx(playerIndex, cards).Success;
        }

        public OperationResult PlayCardsEx(int playerIndex, List<Card> cards)
        {
            var selectedCards = cards ?? new List<Card>();
            var actor = $"player_{playerIndex}";
            var isLead = _currentTrick.Count == 0;

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "play.attempt",
                actor,
                new Dictionary<string, object?>
                {
                    ["player_index"] = playerIndex,
                    ["cards"] = SerializeCards(selectedCards),
                    ["is_lead"] = isLead
                });

            if (_state.Phase != GamePhase.Playing)
            {
                return LogPlayReject(playerIndex, selectedCards, ReasonCodes.PhaseInvalid, 0);
            }

            if (playerIndex != _state.CurrentPlayer)
            {
                return LogPlayReject(playerIndex, selectedCards, ReasonCodes.NotCurrentPlayer, 0);
            }

            var sw = Stopwatch.StartNew();
            OperationResult validResult;
            if (_currentTrick.Count == 0)
            {
                // 首家出牌，需要验证甩牌
                var validator = new PlayValidator(_config);

                // 获取其他玩家的手牌（用于验证甩牌）
                var otherHands = new List<List<Card>>();
                for (int i = 0; i < 4; i++)
                {
                    if (i != playerIndex)
                        otherHands.Add(_state.PlayerHands[i]);
                }

                validResult = validator.IsValidPlayEx(_state.PlayerHands[playerIndex], selectedCards, otherHands);
            }
            else
            {
                // 跟牌
                var validator = new FollowValidator(_config);
                validResult = validator.IsValidFollowEx(
                    _state.PlayerHands[playerIndex],
                    _currentTrick[0].Cards,
                    selectedCards);
            }
            sw.Stop();

            double validatorElapsedMs = sw.Elapsed.TotalMilliseconds;
            if (!validResult.Success)
            {
                if (isLead && validResult.ReasonCode == ReasonCodes.ThrowNotMax)
                {
                    LogPlayReject(
                        playerIndex,
                        selectedCards,
                        validResult.ReasonCode,
                        validatorElapsedMs);

                    var throwValidator = new ThrowValidator(_config);
                    var fallbackCard = throwValidator.GetSmallestCard(selectedCards);
                    if (fallbackCard == null)
                    {
                        return LogPlayReject(
                            playerIndex,
                            selectedCards,
                            ReasonCodes.UnknownError,
                            validatorElapsedMs);
                    }

                    selectedCards = new List<Card> { fallbackCard };

                    LogEvent(
                        LogCategories.Audit,
                        LogLevels.Warn,
                        "play.throw.fallback",
                        actor,
                        new Dictionary<string, object?>
                        {
                            ["player_index"] = playerIndex,
                            ["reason_code"] = ReasonCodes.ThrowNotMax,
                            ["attempted_cards"] = SerializeCards(cards ?? new List<Card>()),
                            ["fallback_cards"] = SerializeCards(selectedCards),
                            ["throw_fail_penalty"] = _config.ThrowFailPenalty
                        });

                    ApplyThrowFailPenalty(playerIndex);
                    validResult = OperationResult.Ok;
                }
                else
                {
                    return LogPlayReject(
                        playerIndex,
                        selectedCards,
                        validResult.ReasonCode ?? ReasonCodes.UnknownError,
                        validatorElapsedMs);
                }
            }

            // 出牌
            _currentTrick.Add(new TrickPlay(playerIndex, selectedCards));
            foreach (var card in selectedCards)
            {
                _state.PlayerHands[playerIndex].Remove(card);
            }

            // 下一个玩家
            _state.CurrentPlayer = (playerIndex + 1) % 4;

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "play.accept",
                actor,
                new Dictionary<string, object?>
                {
                    ["player_index"] = playerIndex,
                    ["cards"] = SerializeCards(selectedCards),
                    ["next_player"] = _state.CurrentPlayer
                },
                new Dictionary<string, double>
                {
                    ["validator_elapsed_ms"] = validatorElapsedMs
                });

            // 一墩结束
            if (_currentTrick.Count == 4)
            {
                FinishTrick();
            }
            else
            {
                _turnNo++;
                LogTurnStart(_state.CurrentPlayer, isLead: false);
            }

            return OperationResult.Ok;
        }

        private void ApplyThrowFailPenalty(int throwPlayerIndex)
        {
            if (_config.ThrowFailPenalty <= 0)
                return;

            int defenderBefore = _state.DefenderScore;
            int delta = throwPlayerIndex % 2 == _state.DealerIndex % 2
                ? _config.ThrowFailPenalty
                : -_config.ThrowFailPenalty;

            int defenderAfter = System.Math.Max(0, defenderBefore + delta);
            if (defenderAfter == defenderBefore)
                return;

            _state.DefenderScore = defenderAfter;
            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "score.update",
                "system",
                new Dictionary<string, object?>
                {
                    ["defender_score_before"] = defenderBefore,
                    ["defender_score_after"] = defenderAfter,
                    ["delta"] = defenderAfter - defenderBefore
                });
        }

        private void FinishTrick()
        {
            var trickSnapshot = _currentTrick
                .Select(p => new TrickPlay(p.PlayerIndex, new List<Card>(p.Cards)))
                .ToList();

            int winner = _judge.DetermineWinner(_currentTrick);

            // 计算得分
            int score = 0;
            foreach (var play in _currentTrick)
            {
                score += play.Cards.Sum(c => c.Score);
            }

            int defenderBefore = _state.DefenderScore;
            int defenderDelta = 0;

            // 闲家得分
            if (winner % 2 != _state.DealerIndex % 2)
            {
                defenderDelta = score;
                _state.DefenderScore += defenderDelta;
            }

            if (defenderDelta != 0)
            {
                LogEvent(
                    LogCategories.Audit,
                    LogLevels.Info,
                    "score.update",
                    "system",
                    new Dictionary<string, object?>
                    {
                        ["defender_score_before"] = defenderBefore,
                        ["defender_score_after"] = _state.DefenderScore,
                        ["delta"] = defenderDelta
                    });
            }

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "trick.finish",
                "system",
                new Dictionary<string, object?>
                {
                    ["trick_no"] = _trickNo,
                    ["trick_cards"] = SerializePlays(trickSnapshot),
                    ["winner_index"] = winner,
                    ["trick_score"] = score,
                    ["defender_score_before"] = defenderBefore,
                    ["defender_score_after"] = _state.DefenderScore,
                    ["winner_basis"] = BuildWinnerBasis(trickSnapshot, winner),
                    ["play_analysis"] = SerializeTrickAnalysis(trickSnapshot)
                });

            // 保存最后一墩首张牌（用于抠底倍数计算）
            _lastTrickCards = new List<Card>(_currentTrick[0].Cards);

            _currentTrick.Clear();
            _state.CurrentPlayer = winner;
            _trickLeader = winner;

            // 检查游戏是否结束
            if (_state.PlayerHands[0].Count == 0)
            {
                FinishGame(winner);
                return;
            }

            _trickNo++;
            _turnNo++;
            LogTurnStart(_state.CurrentPlayer, isLead: true);
        }

        private void FinishGame(int lastWinner)
        {
            int defenderBefore = _state.DefenderScore;

            // 抠底：最后一墩赢家是闲家队，则闲家得底牌分数
            if (lastWinner % 2 != _state.DealerIndex % 2)
            {
                // 用最后一墩的首张牌作为牌型参考
                var lastTrickLead = _lastTrickCards ?? new List<Card> { new Card(Suit.Spade, Rank.Two) };
                int bottomPoints = _state.BuriedCards.Sum(c => c.Score);
                int appliedScore = _scoreCalc.CalculateBottomScore(_state.BuriedCards, lastTrickLead);
                int multiplier = bottomPoints > 0 ? appliedScore / bottomPoints : 0;
                _state.DefenderScore += appliedScore;

                LogEvent(
                    LogCategories.Audit,
                    LogLevels.Info,
                    "bottom.score.apply",
                    "system",
                    new Dictionary<string, object?>
                    {
                        ["bottom_points"] = bottomPoints,
                        ["multiplier"] = multiplier,
                        ["applied_score"] = appliedScore,
                        ["target_side"] = "defender"
                    });
            }

            if (_state.DefenderScore != defenderBefore)
            {
                LogEvent(
                    LogCategories.Audit,
                    LogLevels.Info,
                    "score.update",
                    "system",
                    new Dictionary<string, object?>
                    {
                        ["defender_score_before"] = defenderBefore,
                        ["defender_score_after"] = _state.DefenderScore,
                        ["delta"] = _state.DefenderScore - defenderBefore
                    });
            }

            var from = _state.Phase;
            _state.Phase = GamePhase.Finished;
            LogPhaseTransition(from, _state.Phase);

            _roundTimer.Stop();
            double durationMs = _roundTimer.Elapsed.TotalMilliseconds;
            string winnerSide = _state.DefenderScore >= 80 ? "defender" : "dealer";

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "round.finish",
                "system",
                new Dictionary<string, object?>
                {
                    ["defender_score"] = _state.DefenderScore,
                    ["winner_side"] = winnerSide
                },
                new Dictionary<string, double>
                {
                    ["duration_ms"] = durationMs
                });

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "game.finish",
                "system",
                new Dictionary<string, object?>
                {
                    ["defender_score"] = _state.DefenderScore,
                    ["winner_side"] = winnerSide
                },
                new Dictionary<string, double>
                {
                    ["duration_ms"] = durationMs
                });
        }

        private void LogTurnStart(int currentPlayer, bool isLead)
        {
            var payload = new Dictionary<string, object?>
            {
                ["current_player"] = currentPlayer,
                ["is_lead"] = isLead,
                ["lead_player"] = _trickLeader
            };

            if (isLead)
            {
                payload["trick_no"] = _trickNo;
                payload["dealer_index"] = _state.DealerIndex;
                payload["level_rank"] = _state.LevelRank.ToString();
                payload["trump_suit"] = _state.TrumpSuit?.ToString() ?? Suit.Spade.ToString();
                payload["defender_score"] = _state.DefenderScore;
                payload["hands_before_trick"] = SerializeHandsSnapshot();
            }

            LogEvent(
                LogCategories.Audit,
                LogLevels.Info,
                "turn.start",
                "system",
                payload);
        }

        private void LogTrumpBidReject(int playerIndex, List<Card> cards, string reasonCode)
        {
            LogEvent(
                LogCategories.Audit,
                LogLevels.Warn,
                "trump.bid.reject",
                $"player_{playerIndex}",
                new Dictionary<string, object?>
                {
                    ["player_index"] = playerIndex,
                    ["cards"] = SerializeCards(cards),
                    ["reason_code"] = reasonCode
                });
        }

        private void LogBuryReject(List<Card> cards, string reasonCode)
        {
            LogEvent(
                LogCategories.Audit,
                LogLevels.Warn,
                "bury.reject",
                $"player_{_state.DealerIndex}",
                new Dictionary<string, object?>
                {
                    ["dealer_index"] = _state.DealerIndex,
                    ["selected_cards"] = SerializeCards(cards),
                    ["reason_code"] = reasonCode
                });
        }

        private OperationResult LogPlayReject(int playerIndex, List<Card> cards, string reasonCode, double validatorElapsedMs)
        {
            var metrics = new Dictionary<string, double>();
            if (validatorElapsedMs > 0)
                metrics["validator_elapsed_ms"] = validatorElapsedMs;

            LogEvent(
                LogCategories.Audit,
                LogLevels.Warn,
                "play.reject",
                $"player_{playerIndex}",
                new Dictionary<string, object?>
                {
                    ["player_index"] = playerIndex,
                    ["cards"] = SerializeCards(cards),
                    ["reason_code"] = reasonCode
                },
                metrics.Count == 0 ? null : metrics);

            return OperationResult.Fail(reasonCode);
        }

        private void LogPhaseTransition(GamePhase from, GamePhase to)
        {
            var payload = new Dictionary<string, object?>
            {
                ["from_phase"] = NormalizePhase(from),
                ["to_phase"] = NormalizePhase(to)
            };

            LogEvent(LogCategories.Audit, LogLevels.Info, "phase.exit", "system", payload);
            LogEvent(LogCategories.Audit, LogLevels.Info, "phase.enter", "system", payload);
        }

        private void LogEvent(
            string category,
            string level,
            string eventName,
            string actor,
            Dictionary<string, object?> payload,
            Dictionary<string, double>? metrics = null)
        {
            _logger.Log(new LogEntry
            {
                Category = category,
                Level = level,
                Event = eventName,
                SessionId = _sessionId,
                GameId = _gameId,
                RoundId = _roundId,
                TrickId = _trickNo > 0 ? $"trick_{_trickNo:D4}" : null,
                TurnId = _turnNo > 0 ? $"turn_{_turnNo:D4}" : null,
                Phase = NormalizePhase(_state.Phase),
                Actor = actor,
                Payload = payload,
                Metrics = metrics ?? new Dictionary<string, double>()
            });
        }

        private static List<Dictionary<string, object?>> SerializeCards(IEnumerable<Card> cards)
        {
            return cards.Select(card => new Dictionary<string, object?>
            {
                ["card_instance_id"] = $"{card.Suit}-{card.Rank}-{RuntimeHelpers.GetHashCode(card):x}",
                ["suit"] = card.Suit.ToString(),
                ["rank"] = card.Rank.ToString(),
                ["score"] = card.Score,
                ["text"] = card.ToString()
            }).ToList();
        }

        private static List<Dictionary<string, object?>> SerializePlays(IEnumerable<TrickPlay> plays)
        {
            return plays.Select(p => new Dictionary<string, object?>
            {
                ["player_index"] = p.PlayerIndex,
                ["cards"] = SerializeCards(p.Cards)
            }).ToList();
        }

        private List<Dictionary<string, object?>> SerializeHandsSnapshot()
        {
            var comparer = new CardComparer(_config);
            var result = new List<Dictionary<string, object?>>();

            for (int i = 0; i < _state.PlayerHands.Length; i++)
            {
                var sorted = _state.PlayerHands[i]
                    .OrderByDescending(c => c, comparer)
                    .ToList();

                result.Add(new Dictionary<string, object?>
                {
                    ["player_index"] = i,
                    ["hand_count"] = sorted.Count,
                    ["cards"] = SerializeCards(sorted)
                });
            }

            return result;
        }

        private List<Dictionary<string, object?>> SerializeTrickAnalysis(List<TrickPlay> plays)
        {
            var comparer = new CardComparer(_config);

            return plays.Select(p =>
            {
                var category = _config.GetCardCategory(p.Cards[0]).ToString();
                var pattern = new CardPattern(p.Cards, _config).Type.ToString();
                var topCard = p.Cards.OrderByDescending(c => c, comparer).FirstOrDefault();
                return new Dictionary<string, object?>
                {
                    ["player_index"] = p.PlayerIndex,
                    ["category"] = category,
                    ["pattern"] = pattern,
                    ["top_card"] = topCard?.ToString() ?? string.Empty,
                    ["cards_score"] = p.Cards.Sum(c => c.Score)
                };
            }).ToList();
        }

        private Dictionary<string, object?> BuildWinnerBasis(List<TrickPlay> plays, int winnerIndex)
        {
            if (plays.Count == 0)
                return new Dictionary<string, object?> { ["reason"] = "empty_trick" };

            var comparer = new CardComparer(_config);
            var lead = plays[0];
            var winner = plays.FirstOrDefault(p => p.PlayerIndex == winnerIndex) ?? lead;

            var leadCategory = _config.GetCardCategory(lead.Cards[0]).ToString();
            var winnerCategory = _config.GetCardCategory(winner.Cards[0]).ToString();
            var leadPattern = new CardPattern(lead.Cards, _config).Type.ToString();
            var winnerPattern = new CardPattern(winner.Cards, _config).Type.ToString();

            var leadTop = lead.Cards.OrderByDescending(c => c, comparer).FirstOrDefault();
            var winnerTop = winner.Cards.OrderByDescending(c => c, comparer).FirstOrDefault();

            string reason;
            if (!string.Equals(leadCategory, winnerCategory, StringComparison.Ordinal))
            {
                reason = $"{winnerCategory} over {leadCategory}";
            }
            else if (!string.Equals(leadPattern, winnerPattern, StringComparison.Ordinal))
            {
                reason = $"pattern {winnerPattern} over {leadPattern}";
            }
            else
            {
                reason = $"{winnerTop} over {leadTop}";
            }

            return new Dictionary<string, object?>
            {
                ["lead_player_index"] = lead.PlayerIndex,
                ["winner_player_index"] = winnerIndex,
                ["lead_category"] = leadCategory,
                ["winner_category"] = winnerCategory,
                ["lead_pattern"] = leadPattern,
                ["winner_pattern"] = winnerPattern,
                ["lead_top_card"] = leadTop?.ToString() ?? string.Empty,
                ["winner_top_card"] = winnerTop?.ToString() ?? string.Empty,
                ["reason"] = reason
            };
        }

        private static string GetBidType(List<Card> cards)
        {
            if (cards.Count == 1)
                return "single";
            if (cards.Count == 2)
                return "pair";
            return "strong";
        }

        private static int GetBidPriority(List<Card> cards)
        {
            if (cards.Count <= 1)
                return 0;
            if (cards.Count == 2)
                return 1;
            return 2;
        }

        private static string NormalizePhase(GamePhase phase)
        {
            return phase switch
            {
                GamePhase.Dealing => "Dealing",
                GamePhase.Bidding => "CallTrump",
                GamePhase.Burying => "BuryBottom",
                GamePhase.Playing => "PlayTricks",
                GamePhase.Finished => "Finished",
                _ => phase.ToString()
            };
        }

        private static string GenerateId(string prefix)
        {
            return $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
        }
    }
}
