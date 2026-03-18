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
    /// 从2打到A的完整升级赛模拟 —— Markdown 详细记录版
    /// 每墩记录：出牌前手牌、出牌、胜负
    /// </summary>
    [Trait("Category", "Campaign")]
    [Trait("Category", "LongRunning")]
    public class FullCampaignDetailedTests
    {
        private readonly ITestOutputHelper _output;

        public FullCampaignDetailedTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Campaign_Level2ToA_DetailedMarkdown()
        {
            // 队伍：玩家0+2 = 🔴红队，玩家1+3 = 🔵蓝队
            int[] teamLevel = { 2, 2 };
            int dealerTeam  = 0;
            int dealerIndex = 0;
            int roundNumber = 0;
            bool campaignOver = false;

            // 每局单独写一个 md 文件，另外写一个总览文件
            var outDir = TestPathHelper.ResolveFromRepoRoot("unittest", "self", "campaign_detail");
            Directory.CreateDirectory(outDir);

            var summary = new StringBuilder();
            summary.AppendLine("# 拖拉机升级赛 · 完整通关记录");
            summary.AppendLine();
            summary.AppendLine("## 队伍说明");
            summary.AppendLine();
            summary.AppendLine("| 队伍 | 玩家 | 座位 |");
            summary.AppendLine("|------|------|------|");
            summary.AppendLine("| 🔴 红队 | 南家 + 北家 | 南 + 北 |");
            summary.AppendLine("| 🔵 蓝队 | 东家 + 西家 | 东 + 西 |");
            summary.AppendLine();
            summary.AppendLine("## 升级规则");
            summary.AppendLine();
            summary.AppendLine("| 闲家得分 | 结果 |");
            summary.AppendLine("|----------|------|");
            summary.AppendLine("| 0 分 | 庄家升 3 级，继续坐庄 |");
            summary.AppendLine("| 5–35 分 | 庄家升 2 级，继续坐庄 |");
            summary.AppendLine("| 40–75 分 | 庄家升 1 级，继续坐庄 |");
            summary.AppendLine("| 80–115 分 | 闲家上台，不升级 |");
            summary.AppendLine("| 120–155 分 | 闲家上台升 1 级 |");
            summary.AppendLine("| 160–195 分 | 闲家上台升 2 级 |");
            summary.AppendLine("| 200 分+ | 闲家上台升 3 级 |");
            summary.AppendLine();
            summary.AppendLine("## 各局概览");
            summary.AppendLine();
            summary.AppendLine("| 局 | 庄家 | 打 | 主花色 | 闲家得分 | 获胜方 | 升级 | 红队 | 蓝队 |");
            summary.AppendLine("|:--:|------|:--:|--------|:--------:|--------|:----:|:----:|:----:|");

            while (!campaignOver && roundNumber < 60)
            {
                roundNumber++;
                int seed = 100 + roundNumber * 7;

                var levelRank  = (Rank)teamLevel[dealerTeam];
                var trumpSuit  = PickTrumpSuit(roundNumber);
                var config     = new GameConfig { LevelRank = levelRank, TrumpSuit = trumpSuit };
                var comparer   = new CardComparer(config);

                // ── 发牌 ────────────────────────────────────────────────────────
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

                // 庄家扣底：扣最小8张
                hands[dealerIndex].AddRange(bottomCards);
                var buried = hands[dealerIndex].OrderBy(c => c, comparer).Take(8).ToList();
                foreach (var c in buried) hands[dealerIndex].Remove(c);

                // ── 本局 Markdown ────────────────────────────────────────────────
                var md = new StringBuilder();
                md.AppendLine($"# 第 {roundNumber} 局");
                md.AppendLine();
                md.AppendLine("## 基本信息");
                md.AppendLine();
                md.AppendLine($"| 项目 | 内容 |");
                md.AppendLine($"|------|------|");
                md.AppendLine($"| 庄家 | {PlayerWithTeam(dealerIndex)}（{TeamName(dealerTeam)}） |");
                md.AppendLine($"| 本局打 | **{RankName(levelRank)}** |");
                md.AppendLine($"| 主花色 | {SuitName(trumpSuit)} |");
                md.AppendLine($"| 红队当前级别 | {RankName((Rank)teamLevel[0])} |");
                md.AppendLine($"| 蓝队当前级别 | {RankName((Rank)teamLevel[1])} |");
                md.AppendLine();

                // 初始手牌
                md.AppendLine("## 初始手牌");
                md.AppendLine();
                md.AppendLine("| 玩家 | 队伍 | 角色 | 手牌（按大小排序） |");
                md.AppendLine("|------|------|------|-------------------|");
                for (int p = 0; p < 4; p++)
                {
                    var sorted = hands[p].OrderByDescending(c => c, comparer).ToList();
                    string team = (p % 2 == 0) ? "🔴红队" : "🔵蓝队";
                    string role = (p % 2 == dealerIndex % 2) ? "庄" : "闲";
                    md.AppendLine($"| {PlayerName(p)} | {team} | {role} | {CardsToString(sorted)} |");
                }
                md.AppendLine();
                md.AppendLine($"**底牌（庄家{PlayerName(dealerIndex)}扣入）：** {CardsToString(buried)}");
                md.AppendLine();

                // ── 出牌阶段 ────────────────────────────────────────────────────
                int defenderScore = 0;
                int currentLeader = dealerIndex;
                int trickNum = 0;

                md.AppendLine("## 出牌过程");
                md.AppendLine();

                while (hands[0].Count > 0)
                {
                    trickNum++;
                    var plays = new List<(int player, Card card)>();

                    // ── 出牌前手牌快照 ──────────────────────────────────────────
                    md.AppendLine($"### 第 {trickNum} 墩");
                    md.AppendLine();
                    md.AppendLine($"**首家：{PlayerWithTeamAndRole(currentLeader, dealerIndex)}**");
                    md.AppendLine();
                    md.AppendLine("**出牌前各家手牌：**");
                    md.AppendLine();
                    md.AppendLine("| 玩家 | 剩余手牌 |");
                    md.AppendLine("|------|---------|");
                    // 按出牌顺序排列
                    for (int seat = 0; seat < 4; seat++)
                    {
                        int p = (currentLeader + seat) % 4;
                        var sorted = hands[p].OrderByDescending(c => c, comparer).ToList();
                        string role = SeatTag(p, dealerIndex);
                        md.AppendLine($"| {PlayerWithRole(p, role)} | {CardsToString(sorted)} |");
                    }
                    md.AppendLine();

                    // ── 出牌 ────────────────────────────────────────────────────
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

                    // ── 判断赢家 ────────────────────────────────────────────────
                    var trickPlays = plays.Select(pl =>
                        new TrickPlay(pl.player, new List<Card> { pl.card })).ToList();
                    var judge = new TrickJudge(config);
                    int winner = judge.DetermineWinner(trickPlays);
                    if (winner < 0) winner = firstPlayer;

                    int trickScore = plays.Sum(pl => pl.card.Score);
                    bool defenderWon = (winner % 2) != (dealerIndex % 2);
                    if (defenderWon) defenderScore += trickScore;
                    currentLeader = winner;

                    // ── 出牌结果表格 ────────────────────────────────────────────
                    md.AppendLine("**本墩出牌：**");
                    md.AppendLine();
                    md.AppendLine("| 出牌顺序 | 玩家 | 队伍 | 角色 | 出牌 | 备注 |");
                    md.AppendLine("|:--------:|------|------|:----:|:----:|------|");
                    for (int i = 0; i < plays.Count; i++)
                    {
                        var (p, card) = plays[i];
                        string team = (p % 2 == 0) ? "🔴红队" : "🔵蓝队";
                        string role = SeatTag(p, dealerIndex);
                        string note = (p == winner) ? "🏆 **赢墩**" : "";
                        if (i == 0) note = (p == winner ? "🏆 **赢墩** · 首家" : "首家");
                        md.AppendLine($"| {i + 1} | {PlayerName(p)} | {team} | {role} | **{CardName(card)}** | {note} |");
                    }
                    md.AppendLine();

                    string scoreNote = trickScore > 0
                        ? $"💰 本墩得分：**{trickScore} 分**（{string.Join("、", plays.Where(pl => pl.card.Score > 0).Select(pl => $"{CardName(pl.card)}={pl.card.Score}分"))}）"
                        : "本墩无分牌";
                    string winnerTeam = (winner % 2 == 0) ? "🔴红队" : "🔵蓝队";
                    string winnerRole = SeatTag(winner, dealerIndex);
                    md.AppendLine($"> **结果：** {PlayerName(winner)}（{winnerTeam}·{winnerRole}）赢得本墩。{scoreNote}");
                    if (defenderWon && trickScore > 0)
                        md.AppendLine($"> 闲家累计得分：**{defenderScore} 分**");
                    md.AppendLine();
                    md.AppendLine("---");
                    md.AppendLine();
                }

                // ── 抠底 ────────────────────────────────────────────────────────
                int bottomScore = 0;
                bool lastWinnerIsDefender = (currentLeader % 2) != (dealerIndex % 2);
                if (lastWinnerIsDefender)
                {
                    bottomScore = buried.Sum(c => c.Score) * 2;
                    defenderScore += bottomScore;
                }

                // ── 结算 ────────────────────────────────────────────────────────
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

                // ── 更新级别和庄家 ───────────────────────────────────────────────
                int prevDealerTeam  = dealerTeam;
                int prevDealerIndex = dealerIndex;   // 保存本局庄家，用于总览行
                if (result.NextDealer == "庄家")
                {
                    teamLevel[dealerTeam] = (int)result.NextLevel;
                    dealerIndex = dealerIndex;
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
                md.AppendLine($"| 新庄家 | {PlayerWithTeam(dealerIndex)}（{TeamName(dealerTeam)}） |");
                md.AppendLine($"| 🔴 红队级别 | {RankName((Rank)teamLevel[0])} |");
                md.AppendLine($"| 🔵 蓝队级别 | {RankName((Rank)teamLevel[1])} |");
                md.AppendLine();

                // 写本局文件
                var roundFile = Path.Combine(outDir, $"round_{roundNumber:D2}.md");
                File.WriteAllText(roundFile, md.ToString(), Encoding.UTF8);

                // 追加总览行（使用本局开始时保存的庄家信息）
                summary.AppendLine($"| {roundNumber} | {PlayerWithTeam(prevDealerIndex)}（{TeamName(prevDealerTeam)}） | {RankName(levelRank)} | {SuitName(trumpSuit)} | {defenderScore} | {result.Winner} | +{result.LevelChange} | {RankName((Rank)teamLevel[0])} | {RankName((Rank)teamLevel[1])} |");

                // ── 通关检测 ─────────────────────────────────────────────────────
                for (int t = 0; t < 2; t++)
                {
                    if (teamLevel[t] >= (int)Rank.Ace)
                    {
                        campaignOver = true;
                        summary.AppendLine();
                        summary.AppendLine($"## 🎉 通关！");
                        summary.AppendLine();
                        summary.AppendLine($"**{TeamName(t)} 打到 A，完成通关！共 {roundNumber} 局。**");
                        summary.AppendLine();
                        summary.AppendLine($"| 最终结果 | |");
                        summary.AppendLine($"|----------|--|");
                        summary.AppendLine($"| 🔴 红队最终级别 | {RankName((Rank)teamLevel[0])} |");
                        summary.AppendLine($"| 🔵 蓝队最终级别 | {RankName((Rank)teamLevel[1])} |");
                        summary.AppendLine($"| 总局数 | {roundNumber} 局 |");
                    }
                }
            }

            // 写总览文件
            var summaryFile = Path.Combine(outDir, "00_summary.md");
            File.WriteAllText(summaryFile, summary.ToString(), Encoding.UTF8);

            _output.WriteLine($"报告已写入：{outDir}");
            _output.WriteLine($"总览：{summaryFile}");
            _output.WriteLine($"共 {roundNumber} 局，每局一个 round_XX.md 文件");

            Assert.True(campaignOver, $"未能在 {roundNumber} 局内完成通关");
        }

        // ── 辅助方法 ─────────────────────────────────────────────────────────

        private static List<Card> BuildDeck(int seed)
        {
            var cards = new List<Card>();
            for (int d = 0; d < 2; d++)
            {
                cards.Add(new Card(Suit.Joker, Rank.BigJoker));
                cards.Add(new Card(Suit.Joker, Rank.SmallJoker));
                foreach (Suit s in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
                    foreach (Rank r in Enum.GetValues(typeof(Rank)).Cast<Rank>()
                        .Where(r => r != Rank.SmallJoker && r != Rank.BigJoker))
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

        private static Card BestCard(List<Card> hand, Card? leadCard,
            GameConfig config, CardComparer comparer, bool isLead)
        {
            if (isLead)
                return hand.OrderByDescending(c => c, comparer).First();
            if (leadCard == null)
                return hand.OrderBy(c => c, comparer).First();

            string leadCat = GetCat(leadCard, config);
            var sameCat = hand.Where(c => GetCat(c, config) == leadCat).ToList();
            if (sameCat.Count > 0)
                return sameCat.OrderByDescending(c => c, comparer).First();
            return hand.OrderBy(c => c, comparer).First();
        }

        private static string GetCat(Card c, GameConfig cfg)
            => cfg.IsTrump(c) ? "Trump" : c.Suit.ToString();

        private static Suit PickTrumpSuit(int round)
        {
            var suits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
            return suits[(round - 1) % 4];
        }

        private static string TeamName(int team) => team == 0 ? "🔴红队" : "🔵蓝队";

        private static string PlayerName(int player) => $"{SeatName(player)}家";

        private static string PlayerWithTeam(int player) => $"{PlayerName(player)}";

        private static string PlayerWithRole(int player, string role) => $"{PlayerName(player)}·{role}";

        private static string PlayerWithTeamAndRole(int player, int dealerIndex)
            => $"{PlayerName(player)}（{TeamName(player % 2 == 0 ? 0 : 1)}·{SeatTag(player, dealerIndex)}）";

        private static string SeatName(int player) => player switch
        {
            0 => "南", 1 => "东", 2 => "北", 3 => "西", _ => "?"
        };

        private static string SeatTag(int player, int dealerIndex)
            => (player % 2 == dealerIndex % 2) ? "庄" : "闲";

        private static string RankName(Rank r) => r switch
        {
            Rank.Two => "2", Rank.Three => "3", Rank.Four => "4",
            Rank.Five => "5", Rank.Six => "6", Rank.Seven => "7",
            Rank.Eight => "8", Rank.Nine => "9", Rank.Ten => "10",
            Rank.Jack => "J", Rank.Queen => "Q", Rank.King => "K",
            Rank.Ace => "A", _ => r.ToString()
        };

        private static string SuitName(Suit s) => s switch
        {
            Suit.Spade => "♠黑桃", Suit.Heart => "♥红桃",
            Suit.Club => "♣梅花", Suit.Diamond => "♦方块",
            _ => s.ToString()
        };

        private static string CardName(Card c)
        {
            if (c.Rank == Rank.BigJoker)   return "大🃏";
            if (c.Rank == Rank.SmallJoker) return "小🃏";
            return $"{SuitShort(c.Suit)}{RankName(c.Rank)}";
        }

        private static string SuitShort(Suit s) => s switch
        {
            Suit.Spade => "♠", Suit.Heart => "♥",
            Suit.Club => "♣", Suit.Diamond => "♦",
            _ => ""
        };

        private static string CardsToString(List<Card> cards)
            => string.Join(" ", cards.Select(CardName));
    }
}
