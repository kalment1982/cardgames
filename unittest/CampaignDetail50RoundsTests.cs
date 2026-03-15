using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;
using Xunit.Abstractions;

namespace TractorGame.Tests
{
    /// <summary>
    /// 生成固定50局的详细对局文档（不提前终止）
    /// </summary>
    public class CampaignDetail50RoundsTests
    {
        private readonly ITestOutputHelper _output;

        public CampaignDetail50RoundsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Campaign_Detailed_Exactly50Rounds()
        {
            int[] teamLevel = { 2, 2 };
            int dealerTeam = 0;
            int dealerIndex = 0;
            const int totalRounds = 50;

            var outDir = TestPathHelper.ResolveFromRepoRoot("unittest", "self", "campaign_detail");
            Directory.CreateDirectory(outDir);

            foreach (var f in Directory.GetFiles(outDir, "round_*.md")) File.Delete(f);
            var oldSummary = Path.Combine(outDir, "00_summary.md");
            if (File.Exists(oldSummary)) File.Delete(oldSummary);

            var summary = new StringBuilder();
            summary.AppendLine("# 拖拉机升级赛 · 50局详细记录");
            summary.AppendLine();
            summary.AppendLine("## 说明");
            summary.AppendLine();
            summary.AppendLine("- 本次固定输出 50 局，不因任一队伍达到 A 提前结束。\n- 每局使用不同 seed 洗牌：`seed = 100 + round * 7`。\n- 该策略属于伪随机且可复现：同一局号同一 seed，会得到同样发牌结果。");
            summary.AppendLine();
            summary.AppendLine("## 各局概览");
            summary.AppendLine();
            summary.AppendLine("| 局 | seed | 庄家 | 打 | 主花色 | 闲家得分 | 获胜方 | 升级 | 红队 | 蓝队 |");
            summary.AppendLine("|:--:|:----:|------|:--:|--------|:--------:|--------|:----:|:----:|:----:|");

            for (int roundNumber = 1; roundNumber <= totalRounds; roundNumber++)
            {
                int seed = 100 + roundNumber * 7;

                var levelRank = (Rank)teamLevel[dealerTeam];
                var trumpSuit = PickTrumpSuit(roundNumber);
                var config = new GameConfig { LevelRank = levelRank, TrumpSuit = trumpSuit };
                var comparer = new CardComparer(config);

                var allCards = BuildDeck(seed);
                var hands = new List<Card>[4];
                for (int i = 0; i < 4; i++) hands[i] = new List<Card>();
                var bottomCards = new List<Card>();

                int idx = 0;
                for (int i = 0; i < 6; i++) bottomCards.Add(allCards[idx++]);
                for (int r = 0; r < 25; r++)
                    for (int p = 0; p < 4; p++)
                        hands[p].Add(allCards[idx++]);
                for (int i = 0; i < 2; i++) bottomCards.Add(allCards[idx++]);

                hands[dealerIndex].AddRange(bottomCards);
                var buried = hands[dealerIndex].OrderBy(c => c, comparer).Take(8).ToList();
                foreach (var c in buried) hands[dealerIndex].Remove(c);

                var md = new StringBuilder();
                md.AppendLine($"# 第 {roundNumber} 局");
                md.AppendLine();
                md.AppendLine("## 基本信息");
                md.AppendLine();
                md.AppendLine("| 项目 | 内容 |");
                md.AppendLine("|------|------|");
                md.AppendLine($"| seed | `{seed}` |");
                md.AppendLine($"| 庄家 | 玩家{dealerIndex}（{TeamName(dealerTeam)}） |");
                md.AppendLine($"| 本局打 | **{RankName(levelRank)}** |");
                md.AppendLine($"| 主花色 | {SuitName(trumpSuit)} |");
                md.AppendLine($"| 红队当前级别 | {RankName((Rank)teamLevel[0])} |");
                md.AppendLine($"| 蓝队当前级别 | {RankName((Rank)teamLevel[1])} |");
                md.AppendLine();

                md.AppendLine("## 初始手牌");
                md.AppendLine();
                md.AppendLine("| 玩家 | 队伍 | 角色 | 手牌（人性化分组） |");
                md.AppendLine("|------|------|------|--------------------|");
                for (int p = 0; p < 4; p++)
                {
                    var sorted = hands[p].OrderByDescending(c => c, comparer).ToList();
                    string team = (p % 2 == 0) ? "🔴红队" : "🔵蓝队";
                    string role = (p % 2 == dealerIndex % 2) ? "庄" : "闲";
                    md.AppendLine($"| 玩家{p}（{SeatName(p)}） | {team} | {role} | {CardsToHumanString(sorted, config)} |");
                }
                md.AppendLine();
                md.AppendLine($"**底牌（庄家玩家{dealerIndex}扣入）：** {CardsToHumanString(buried, config)}");
                md.AppendLine();

                int defenderScore = 0;
                int currentLeader = dealerIndex;
                int trickNum = 0;

                md.AppendLine("## 出牌过程");
                md.AppendLine();

                while (hands[0].Count > 0)
                {
                    trickNum++;
                    var plays = new List<(int player, Card card)>();

                    md.AppendLine($"### 第 {trickNum} 墩");
                    md.AppendLine();
                    md.AppendLine($"**首家：玩家{currentLeader}（{TeamName(currentLeader % 2 == 0 ? 0 : 1)}·{SeatTag(currentLeader, dealerIndex)}）**");
                    md.AppendLine();
                    md.AppendLine("**出牌前各家手牌：**");
                    md.AppendLine();
                    md.AppendLine("| 玩家 | 剩余手牌（人性化分组） |");
                    md.AppendLine("|------|----------------------|");
                    for (int seat = 0; seat < 4; seat++)
                    {
                        int p = (currentLeader + seat) % 4;
                        var sorted = hands[p].OrderByDescending(c => c, comparer).ToList();
                        string role = SeatTag(p, dealerIndex);
                        md.AppendLine($"| 玩家{p}（{SeatName(p)}·{role}） | {CardsToHumanString(sorted, config)} |");
                    }
                    md.AppendLine();

                    int firstPlayer = currentLeader;
                    var leadCard = BestCard(hands[firstPlayer], null, config, comparer, true);
                    plays.Add((firstPlayer, leadCard));
                    hands[firstPlayer].Remove(leadCard);

                    for (int seat = 1; seat < 4; seat++)
                    {
                        int p = (firstPlayer + seat) % 4;
                        var followCard = BestCard(hands[p], leadCard, config, comparer, false);
                        plays.Add((p, followCard));
                        hands[p].Remove(followCard);
                    }

                    var trickPlays = plays.Select(pl => new TrickPlay(pl.player, new List<Card> { pl.card })).ToList();
                    var judge = new TrickJudge(config);
                    int winner = judge.DetermineWinner(trickPlays);
                    if (winner < 0) winner = firstPlayer;

                    int trickScore = plays.Sum(pl => pl.card.Score);
                    bool defenderWon = (winner % 2) != (dealerIndex % 2);
                    if (defenderWon) defenderScore += trickScore;
                    currentLeader = winner;

                    md.AppendLine("**本墩出牌：**");
                    md.AppendLine();
                    md.AppendLine("| 出牌顺序 | 玩家 | 队伍 | 角色 | 出牌 | 牌张属性 | 备注 |");
                    md.AppendLine("|:--------:|------|------|:----:|:----:|----------|------|");
                    for (int i = 0; i < plays.Count; i++)
                    {
                        var (p, card) = plays[i];
                        string team = (p % 2 == 0) ? "🔴红队" : "🔵蓝队";
                        string role = SeatTag(p, dealerIndex);
                        string note = (p == winner) ? "🏆 **赢墩**" : "";
                        if (i == 0) note = (p == winner ? "🏆 **赢墩** · 首家" : "首家");
                        md.AppendLine($"| {i + 1} | 玩家{p}（{SeatName(p)}） | {team} | {role} | **{CardName(card)}** | {CardTraits(card, plays[0].card, config, i == 0)} | {note} |");
                    }
                    md.AppendLine();

                    string scoreNote = trickScore > 0
                        ? $"💰 本墩得分：**{trickScore} 分**（{string.Join("、", plays.Where(pl => pl.card.Score > 0).Select(pl => $"{CardName(pl.card)}={pl.card.Score}分"))}）"
                        : "本墩无分牌";
                    string winnerTeam = (winner % 2 == 0) ? "🔴红队" : "🔵蓝队";
                    string winnerRole = SeatTag(winner, dealerIndex);
                    md.AppendLine($"> **结果：** 玩家{winner}（{winnerTeam}·{winnerRole}）赢得本墩。{scoreNote}");
                    if (defenderWon && trickScore > 0)
                        md.AppendLine($"> 闲家累计得分：**{defenderScore} 分**");
                    md.AppendLine();
                    md.AppendLine("---");
                    md.AppendLine();
                }

                int bottomScore = 0;
                bool lastWinnerIsDefender = (currentLeader % 2) != (dealerIndex % 2);
                if (lastWinnerIsDefender)
                {
                    bottomScore = buried.Sum(c => c.Score) * 2;
                    defenderScore += bottomScore;
                }

                var levelMgr = new LevelManager();
                var result = levelMgr.DetermineLevelChange(defenderScore, levelRank);

                md.AppendLine("## 本局结算");
                md.AppendLine();
                md.AppendLine("| 项目 | 数值 |");
                md.AppendLine("|------|------|");
                md.AppendLine($"| 闲家得分（含抠底） | **{defenderScore} 分** |");
                md.AppendLine($"| 其中抠底得分 | {bottomScore} 分 |");
                md.AppendLine($"| 庄家得分 | {200 - defenderScore} 分 |");
                md.AppendLine($"| 获胜方 | **{result.Winner}** |");
                md.AppendLine($"| 升级级数 | {result.LevelChange} 级 |");
                md.AppendLine($"| 下一局打 | **{RankName(result.NextLevel)}** |");
                md.AppendLine();

                int prevDealerTeam = dealerTeam;
                int prevDealerIndex = dealerIndex;
                if (result.NextDealer == "庄家")
                {
                    teamLevel[dealerTeam] = (int)result.NextLevel;
                    dealerIndex = (dealerIndex + 2) % 4;
                }
                else
                {
                    int defTeam = 1 - dealerTeam;
                    teamLevel[defTeam] = (int)result.NextLevel;
                    dealerTeam = defTeam;
                    dealerIndex = (dealerIndex + 1) % 4;
                }

                md.AppendLine("## 下一局状态");
                md.AppendLine();
                md.AppendLine("| 项目 | 内容 |");
                md.AppendLine("|------|------|");
                md.AppendLine($"| 新庄家 | 玩家{dealerIndex}（{TeamName(dealerTeam)}） |");
                md.AppendLine($"| 🔴 红队级别 | {RankName((Rank)teamLevel[0])} |");
                md.AppendLine($"| 🔵 蓝队级别 | {RankName((Rank)teamLevel[1])} |");
                md.AppendLine();

                var roundFile = Path.Combine(outDir, $"round_{roundNumber:D2}.md");
                File.WriteAllText(roundFile, md.ToString(), Encoding.UTF8);

                summary.AppendLine($"| {roundNumber} | {seed} | 玩家{prevDealerIndex}（{TeamName(prevDealerTeam)}） | {RankName(levelRank)} | {SuitName(trumpSuit)} | {defenderScore} | {result.Winner} | +{result.LevelChange} | {RankName((Rank)teamLevel[0])} | {RankName((Rank)teamLevel[1])} |");
            }

            var summaryFile = Path.Combine(outDir, "00_summary.md");
            File.WriteAllText(summaryFile, summary.ToString(), Encoding.UTF8);

            _output.WriteLine($"50局报告已写入：{outDir}");
            _output.WriteLine($"总览：{summaryFile}");
            Assert.True(File.Exists(summaryFile));
            Assert.Equal(50, Directory.GetFiles(outDir, "round_*.md").Length);
        }

        private static List<Card> BuildDeck(int seed)
        {
            var cards = new List<Card>();
            for (int d = 0; d < 2; d++)
            {
                cards.Add(new Card(Suit.Joker, Rank.BigJoker));
                cards.Add(new Card(Suit.Joker, Rank.SmallJoker));
                foreach (Suit s in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
                    foreach (Rank r in Enum.GetValues(typeof(Rank)).Cast<Rank>().Where(r => r != Rank.SmallJoker && r != Rank.BigJoker))
                        cards.Add(new Card(s, r));
            }
            var rng = new Random(seed);
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
            return cards;
        }

        private static Card BestCard(List<Card> hand, Card? leadCard, GameConfig config, CardComparer comparer, bool isLead)
        {
            if (isLead) return hand.OrderByDescending(c => c, comparer).First();
            if (leadCard == null) return hand.OrderBy(c => c, comparer).First();
            string leadCat = GetCat(leadCard, config);
            var sameCat = hand.Where(c => GetCat(c, config) == leadCat).ToList();
            if (sameCat.Count > 0) return sameCat.OrderByDescending(c => c, comparer).First();
            return hand.OrderBy(c => c, comparer).First();
        }

        private static string GetCat(Card c, GameConfig cfg) => cfg.IsTrump(c) ? "Trump" : c.Suit.ToString();
        private static Suit PickTrumpSuit(int round)
        {
            var suits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
            return suits[(round - 1) % 4];
        }

        private static string TeamName(int team) => team == 0 ? "🔴红队" : "🔵蓝队";
        private static string SeatName(int player) => player switch { 0 => "南", 1 => "东", 2 => "北", 3 => "西", _ => "?" };
        private static string SeatTag(int player, int dealerIndex) => (player % 2 == dealerIndex % 2) ? "庄" : "闲";

        private static string RankName(Rank r) => r switch
        {
            Rank.Two => "2", Rank.Three => "3", Rank.Four => "4", Rank.Five => "5", Rank.Six => "6", Rank.Seven => "7",
            Rank.Eight => "8", Rank.Nine => "9", Rank.Ten => "10", Rank.Jack => "J", Rank.Queen => "Q", Rank.King => "K", Rank.Ace => "A", _ => r.ToString()
        };

        private static string SuitName(Suit s) => s switch
        {
            Suit.Spade => "♠黑桃", Suit.Heart => "♥红桃", Suit.Club => "♣梅花", Suit.Diamond => "♦方块", _ => s.ToString()
        };

        private static string CardName(Card c)
        {
            if (c.Rank == Rank.BigJoker) return "大🃏";
            if (c.Rank == Rank.SmallJoker) return "小🃏";
            return $"{SuitShort(c.Suit)}{RankName(c.Rank)}";
        }

        private static string SuitShort(Suit s) => s switch
        {
            Suit.Spade => "♠", Suit.Heart => "♥", Suit.Club => "♣", Suit.Diamond => "♦", _ => ""
        };

        private static string CardsToString(List<Card> cards) => string.Join(" ", cards.Select(CardName));

        private static string CardsToHumanString(List<Card> cards, GameConfig config)
        {
            if (cards.Count == 0)
                return "(空)";

            var trump = cards.Where(config.IsTrump).OrderByDescending(c => c, new CardComparer(config)).ToList();
            var spades = cards.Where(c => c.Suit == Suit.Spade && !config.IsTrump(c)).OrderByDescending(c => c, new CardComparer(config)).ToList();
            var hearts = cards.Where(c => c.Suit == Suit.Heart && !config.IsTrump(c)).OrderByDescending(c => c, new CardComparer(config)).ToList();
            var clubs = cards.Where(c => c.Suit == Suit.Club && !config.IsTrump(c)).OrderByDescending(c => c, new CardComparer(config)).ToList();
            var diamonds = cards.Where(c => c.Suit == Suit.Diamond && !config.IsTrump(c)).OrderByDescending(c => c, new CardComparer(config)).ToList();

            var segments = new List<string>();
            if (trump.Count > 0) segments.Add($"主[{CardsToString(trump)}]");
            if (spades.Count > 0) segments.Add($"♠[{CardsToString(spades)}]");
            if (hearts.Count > 0) segments.Add($"♥[{CardsToString(hearts)}]");
            if (clubs.Count > 0) segments.Add($"♣[{CardsToString(clubs)}]");
            if (diamonds.Count > 0) segments.Add($"♦[{CardsToString(diamonds)}]");

            var scoreCards = cards.Where(c => c.Score > 0).ToList();
            var scoreText = scoreCards.Count > 0
                ? $"分牌{scoreCards.Count}张/{scoreCards.Sum(c => c.Score)}分"
                : "无分牌";
            return $"{string.Join(" ｜ ", segments)} （共{cards.Count}张，{scoreText}）";
        }

        private static string CardTraits(Card card, Card leadCard, GameConfig config, bool isLead)
        {
            var traits = new List<string>();
            traits.Add(config.IsTrump(card) ? "主牌" : "副牌");
            if (card.Rank == config.LevelRank) traits.Add("级牌");
            if (card.Score > 0) traits.Add($"分牌{card.Score}分");

            if (isLead)
            {
                traits.Add("首攻");
            }
            else
            {
                var leadCat = GetCat(leadCard, config);
                var cat = GetCat(card, config);
                traits.Add(cat == leadCat ? "跟同门" : "垫牌/毙牌");
            }

            return string.Join(" · ", traits);
        }
    }
}
