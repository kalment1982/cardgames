import fs from 'node:fs/promises';
import path from 'node:path';
import process from 'node:process';
import { spawn } from 'node:child_process';
import { chromium } from 'playwright';

const SCRIPT_DIR = path.dirname(new URL(import.meta.url).pathname);
const REPO_ROOT = path.resolve(SCRIPT_DIR, '..', '..');

const SUIT = {
  Spade: 0,
  Heart: 1,
  Club: 2,
  Diamond: 3,
  Joker: 4,
};

const RANK = {
  Two: 2,
  Three: 3,
  Four: 4,
  Five: 5,
  Six: 6,
  Seven: 7,
  Eight: 8,
  Nine: 9,
  Ten: 10,
  Jack: 11,
  Queen: 12,
  King: 13,
  Ace: 14,
  SmallJoker: 15,
  BigJoker: 16,
};

const RANK_DESC = [14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

function parseArgs(argv) {
  const args = {
    games: 10,
    startSeed: 3000,
    baseUrl: 'http://127.0.0.1:5167',
    headful: false,
    timeoutMs: 180000,
  };

  for (let i = 0; i < argv.length; i += 1) {
    const token = argv[i];
    if (token === '--games' && argv[i + 1]) {
      args.games = Number.parseInt(argv[++i], 10);
      continue;
    }
    if (token === '--start-seed' && argv[i + 1]) {
      args.startSeed = Number.parseInt(argv[++i], 10);
      continue;
    }
    if (token === '--base-url' && argv[i + 1]) {
      args.baseUrl = argv[++i];
      continue;
    }
    if (token === '--headful') {
      args.headful = true;
      continue;
    }
    if (token === '--timeout-ms' && argv[i + 1]) {
      args.timeoutMs = Number.parseInt(argv[++i], 10);
      continue;
    }
  }

  if (!Number.isFinite(args.games) || args.games <= 0) {
    throw new Error('参数 --games 必须是正整数');
  }

  return args;
}

function nowStamp() {
  return new Date().toISOString().replace(/[:.]/g, '-');
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

async function waitForServer(baseUrl, timeoutMs) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(baseUrl, { method: 'GET' });
      if (res.ok || res.status === 404) {
        return;
      }
    } catch {
      // ignore
    }
    await sleep(500);
  }
  throw new Error(`WebUI 启动超时: ${baseUrl}`);
}

function startWebUi(baseUrl) {
  const args = ['run', '--project', 'WebUI/WebUI.csproj', '--urls', baseUrl];
  const server = spawn('dotnet', args, {
    cwd: REPO_ROOT,
    stdio: ['ignore', 'pipe', 'pipe'],
    env: process.env,
  });

  server.stdout.on('data', () => {});
  server.stderr.on('data', () => {});

  return server;
}

async function stopWebUi(server) {
  if (!server || server.exitCode !== null) {
    return;
  }

  const waitExit = (timeoutMs) => new Promise((resolve) => {
    const timer = setTimeout(() => resolve(false), timeoutMs);
    server.once('exit', () => {
      clearTimeout(timer);
      resolve(true);
    });
  });

  server.kill('SIGINT');
  const exitedGracefully = await waitExit(4000);
  if (exitedGracefully || server.exitCode !== null) {
    return;
  }

  server.kill('SIGKILL');
  await waitExit(4000);
}

function toSuitValue(raw) {
  if (typeof raw === 'number') return raw;
  if (typeof raw === 'string' && raw in SUIT) return SUIT[raw];
  throw new Error(`未知花色: ${raw}`);
}

function toRankValue(raw) {
  if (typeof raw === 'number') return raw;
  if (typeof raw === 'string' && raw in RANK) return RANK[raw];
  throw new Error(`未知点数: ${raw}`);
}

function normalizeCard(rawCard) {
  const suit = toSuitValue(rawCard.suit);
  const rank = toRankValue(rawCard.rank);
  const score = Number.isFinite(rawCard.score)
    ? rawCard.score
    : (rank === 5 ? 5 : (rank === 10 || rank === 13 ? 10 : 0));

  return {
    suit,
    rank,
    score,
    text: rawCard.text ?? `${suit}-${rank}`,
  };
}

function sameCard(a, b) {
  return a.suit === b.suit && a.rank === b.rank;
}

function isTrump(card, config) {
  if (card.rank === RANK.SmallJoker || card.rank === RANK.BigJoker) return true;
  if (card.rank === config.levelRank) return true;
  if (config.trumpSuit != null && card.suit === config.trumpSuit) return true;
  return false;
}

function cardCategory(card, config) {
  return isTrump(card, config) ? 'Trump' : 'Suit';
}

function compareTrump(a, b, config) {
  if (a.rank === RANK.BigJoker && b.rank === RANK.BigJoker) return 0;
  if (a.rank === RANK.BigJoker) return 1;
  if (b.rank === RANK.BigJoker) return -1;

  if (a.rank === RANK.SmallJoker && b.rank === RANK.SmallJoker) return 0;
  if (a.rank === RANK.SmallJoker) return 1;
  if (b.rank === RANK.SmallJoker) return -1;

  const aIsLevel = a.rank === config.levelRank;
  const bIsLevel = b.rank === config.levelRank;

  if (aIsLevel && bIsLevel) {
    const aMainSuit = config.trumpSuit != null && a.suit === config.trumpSuit;
    const bMainSuit = config.trumpSuit != null && b.suit === config.trumpSuit;
    if (aMainSuit && !bMainSuit) return 1;
    if (!aMainSuit && bMainSuit) return -1;
    return 0;
  }

  if (aIsLevel) return 1;
  if (bIsLevel) return -1;

  return Math.sign(a.rank - b.rank);
}

function compareSuit(a, b) {
  if (a.suit !== b.suit) return 0;
  return Math.sign(a.rank - b.rank);
}

function cardCompare(a, b, config) {
  const aTrump = isTrump(a, config);
  const bTrump = isTrump(b, config);

  if (aTrump && !bTrump) return 1;
  if (!aTrump && bTrump) return -1;

  if (aTrump && bTrump) return compareTrump(a, b, config);

  return compareSuit(a, b);
}

function isPair(cards) {
  return cards.length === 2 && sameCard(cards[0], cards[1]);
}

function getTrumpOrder(levelRank) {
  const order = [RANK.BigJoker, RANK.SmallJoker, levelRank];
  for (const rank of RANK_DESC) {
    if (rank !== levelRank) {
      order.push(rank);
    }
  }
  return order;
}

function isAdjacentInTrump(a, b, config) {
  const order = getTrumpOrder(config.levelRank);
  const idxA = order.indexOf(a.rank);
  const idxB = order.indexOf(b.rank);
  if (idxA === -1 || idxB === -1) return false;
  return Math.abs(idxA - idxB) === 1;
}

function isAdjacentInSuit(a, b, config) {
  if (a.suit !== b.suit) return false;
  const diff = Math.abs(a.rank - b.rank);
  if (diff === 1) return true;
  if (diff === 2) {
    const min = Math.min(a.rank, b.rank);
    return config.levelRank === min + 1;
  }
  return false;
}

function isAdjacent(a, b, config) {
  const aTrump = isTrump(a, config);
  const bTrump = isTrump(b, config);
  if (aTrump !== bTrump) return false;

  return aTrump
    ? isAdjacentInTrump(a, b, config)
    : isAdjacentInSuit(a, b, config);
}

function isTractor(cards, config) {
  if (cards.length < 4 || cards.length % 2 !== 0) return false;

  const sorted = [...cards].sort((a, b) => cardCompare(b, a, config));
  const pairAnchors = [];

  for (let i = 0; i < sorted.length; i += 2) {
    if (i + 1 >= sorted.length || !sameCard(sorted[i], sorted[i + 1])) {
      return false;
    }
    pairAnchors.push(sorted[i]);
  }

  if (pairAnchors.length < 2) return false;

  for (let i = 0; i < pairAnchors.length - 1; i += 1) {
    if (!isAdjacent(pairAnchors[i], pairAnchors[i + 1], config)) {
      return false;
    }
  }

  return true;
}

function patternType(cards, config) {
  if (cards.length === 1) return 'Single';
  if (cards.length === 2 && isPair(cards)) return 'Pair';
  if (isTractor(cards, config)) return 'Tractor';
  return 'Mixed';
}

function patternPriority(type) {
  switch (type) {
    case 'Tractor':
      return 3;
    case 'Pair':
      return 2;
    case 'Single':
      return 1;
    case 'Mixed':
      return 0;
    default:
      return 0;
  }
}

function compareTrumpCards(cards1, cards2, config) {
  const t1 = patternType(cards1, config);
  const t2 = patternType(cards2, config);

  if (t1 !== t2) {
    return patternPriority(t1) > patternPriority(t2);
  }

  const sorted1 = [...cards1].sort((a, b) => cardCompare(b, a, config));
  const sorted2 = [...cards2].sort((a, b) => cardCompare(b, a, config));
  return cardCompare(sorted1[0], sorted2[0], config) > 0;
}

function compareSuitCards(cards1, cards2, config) {
  const t1 = patternType(cards1, config);
  const t2 = patternType(cards2, config);

  if (t1 !== t2) {
    return patternPriority(t1) > patternPriority(t2);
  }

  const max1 = Math.max(...cards1.map((c) => c.rank));
  const max2 = Math.max(...cards2.map((c) => c.rank));
  return max1 > max2;
}

function isStronger(play1, play2, leadSuit, leadCategory, config) {
  const cat1 = cardCategory(play1.cards[0], config);
  const cat2 = cardCategory(play2.cards[0], config);

  if (cat1 === 'Trump' && cat2 === 'Suit') return true;
  if (cat1 === 'Suit' && cat2 === 'Trump') return false;

  if (cat1 === 'Trump' && cat2 === 'Trump') {
    return compareTrumpCards(play1.cards, play2.cards, config);
  }

  const suit1 = play1.cards[0].suit;
  const suit2 = play2.cards[0].suit;
  const isLeadSuit1 = suit1 === leadSuit && cat1 === leadCategory;
  const isLeadSuit2 = suit2 === leadSuit && cat2 === leadCategory;

  if (isLeadSuit1 && !isLeadSuit2) return true;
  if (!isLeadSuit1 && isLeadSuit2) return false;

  if (isLeadSuit1 && isLeadSuit2) {
    return compareSuitCards(play1.cards, play2.cards, config);
  }

  return false;
}

function determineWinner(plays, config) {
  if (!plays || plays.length === 0) {
    return -1;
  }

  const leadPlay = plays[0];
  const leadCard = leadPlay.cards[0];
  const leadCategory = cardCategory(leadCard, config);
  const leadSuit = leadCard.suit;

  let winner = leadPlay;

  for (let i = 1; i < plays.length; i += 1) {
    const current = plays[i];
    if (isStronger(current, winner, leadSuit, leadCategory, config)) {
      winner = current;
    }
  }

  return winner.playerIndex;
}

function calculateMultiplier(leadCards, config) {
  const type = patternType(leadCards, config);
  if (type === 'Tractor') {
    const pairCount = leadCards.length / 2;
    return 2 ** pairCount;
  }
  if (type === 'Pair') {
    return 4;
  }
  return 2;
}

function sumScore(cards) {
  return cards.reduce((acc, c) => acc + c.score, 0);
}

function analyzeGame(events) {
  const issues = [];

  const gameStart = events.find((e) => e.type === 'game_start');
  if (!gameStart) {
    return {
      pass: false,
      issues: ['缺少 game_start 事件'],
      metrics: {},
    };
  }

  const dealerIndex = Number(gameStart.dealerIndex ?? 0);
  const levelRank = toRankValue(gameStart.levelRank ?? 'Two');

  const trumpEvent = [...events].reverse().find((e) => e.type === 'trump_finalized');
  if (!trumpEvent) {
    issues.push('缺少 trump_finalized 事件');
  }

  const trumpSuit = trumpEvent ? toSuitValue(trumpEvent.trumpSuit) : SUIT.Spade;
  const config = { levelRank, trumpSuit };

  const trickEnds = events
    .filter((e) => e.type === 'trick_end')
    .sort((a, b) => Number(a.trickIndex) - Number(b.trickIndex));

  let runningDefenderScore = 0;
  for (const trick of trickEnds) {
    const plays = (trick.plays ?? []).map((p) => ({
      playerIndex: Number(p.playerIndex),
      cards: (p.cards ?? []).map(normalizeCard),
    }));

    if (plays.length !== 4) {
      issues.push(`第 ${trick.trickIndex} 墩出牌数不是4家`);
      continue;
    }

    const expectedWinner = determineWinner(plays, config);
    const actualWinner = Number(trick.winner);
    if (expectedWinner !== actualWinner) {
      issues.push(`第 ${trick.trickIndex} 墩赢家不一致: 期望 ${expectedWinner}, 实际 ${actualWinner}`);
    }

    const expectedTrickScore = plays.reduce((acc, p) => acc + sumScore(p.cards), 0);
    if (expectedTrickScore !== Number(trick.trickScore)) {
      issues.push(`第 ${trick.trickIndex} 墩分值不一致: 期望 ${expectedTrickScore}, 实际 ${trick.trickScore}`);
    }

    const expectedDelta = (actualWinner % 2 !== dealerIndex % 2) ? expectedTrickScore : 0;
    const actualDelta = Number(trick.defenderScoreDelta);
    if (expectedDelta !== actualDelta) {
      issues.push(`第 ${trick.trickIndex} 墩闲家得分增量不一致: 期望 ${expectedDelta}, 实际 ${actualDelta}`);
    }

    const before = Number(trick.defenderScoreBefore);
    const after = Number(trick.defenderScoreAfter);
    if (before !== runningDefenderScore) {
      issues.push(`第 ${trick.trickIndex} 墩前闲家分累计异常: 期望 ${runningDefenderScore}, 实际 ${before}`);
    }

    if (after !== before + actualDelta) {
      issues.push(`第 ${trick.trickIndex} 墩后闲家分累计异常: 期望 ${before + actualDelta}, 实际 ${after}`);
    }

    runningDefenderScore = after;
  }

  const finalEvent = [...events].reverse().find((e) => e.type === 'game_finished');
  if (!finalEvent) {
    issues.push('缺少 game_finished 事件');
  }

  const buryEvent = events.find((e) => e.type === 'bury_bottom');
  const lastTrick = trickEnds.length > 0 ? trickEnds[trickEnds.length - 1] : null;

  let expectedFinalScore = runningDefenderScore;
  let bottomExpected = 0;

  if (lastTrick && buryEvent) {
    const lastWinner = Number(lastTrick.winner);
    const defendersWonLastTrick = lastWinner % 2 !== dealerIndex % 2;

    if (defendersWonLastTrick) {
      const buriedCards = (buryEvent.cards ?? []).map(normalizeCard);
      const leadCards = (lastTrick.plays?.[0]?.cards ?? []).map(normalizeCard);
      const base = sumScore(buriedCards);
      const multiplier = calculateMultiplier(leadCards, config);
      bottomExpected = base * multiplier;
      expectedFinalScore += bottomExpected;
    }
  }

  if (finalEvent) {
    const actualFinalScore = Number(finalEvent.defenderScore);
    if (actualFinalScore !== expectedFinalScore) {
      issues.push(`终局闲家分不一致: 期望 ${expectedFinalScore}, 实际 ${actualFinalScore}`);
    }
  }

  const rejected = events.filter((e) => e.type === 'play_rejected').length;

  return {
    pass: issues.length === 0,
    issues,
    metrics: {
      trickCount: trickEnds.length,
      expectedFinalScore,
      bottomExpected,
      rejectedPlays: rejected,
      finalDefenderScore: finalEvent ? Number(finalEvent.defenderScore) : null,
    },
  };
}

function rand(min, max) {
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

async function humanClick(page, locator, options = {}) {
  const { force = false } = options;
  const count = await locator.count();
  if (count === 0) {
    return false;
  }

  const target = locator.first();
  if (!force) {
    await target.waitFor({ state: 'visible', timeout: 5000 });
    const box = await target.boundingBox();
    if (box) {
      await page.mouse.move(
        box.x + box.width * (0.2 + Math.random() * 0.6),
        box.y + box.height * (0.2 + Math.random() * 0.6),
        { steps: rand(4, 10) },
      );
      await page.waitForTimeout(rand(50, 140));
    }
  }

  try {
    await target.click({ force, timeout: 5000 });
  } catch (error) {
    if (force) {
      throw error;
    }
    await target.click({ force: true, timeout: 5000 });
  }
  await page.waitForTimeout(rand(90, 220));
  return true;
}

async function triggerTestButton(page, selector) {
  await page.evaluate((sel) => {
    const btn = document.querySelector(sel);
    if (!btn) {
      throw new Error(`找不到测试按钮: ${sel}`);
    }
    btn.click();
  }, selector);
  await page.waitForTimeout(rand(70, 160));
}

async function clickActiveBidButton(page) {
  const clicked = await page.evaluate(() => {
    const selector = [
      '[data-testid="bid-spade"].active',
      '[data-testid="bid-heart"].active',
      '[data-testid="bid-club"].active',
      '[data-testid="bid-diamond"].active',
      '[data-testid="bid-joker"].active',
    ].join(',');

    const btn = document.querySelector(selector);
    if (!btn) {
      return false;
    }

    btn.click();
    return true;
  });

  if (clicked) {
    await page.waitForTimeout(rand(90, 180));
  }

  return clicked;
}

async function readState(page) {
  return page.evaluate(() => {
    const el = document.querySelector('[data-testid="test-state"]');
    if (!el) return null;

    return {
      phase: el.getAttribute('data-phase') ?? 'None',
      currentPlayer: Number(el.getAttribute('data-current-player') ?? '-1'),
      defenderScore: Number(el.getAttribute('data-defender-score') ?? '0'),
      selectedCount: Number(el.getAttribute('data-selected-count') ?? '0'),
      handCount: Number(el.getAttribute('data-hand-count') ?? '0'),
      trickCount: Number(el.getAttribute('data-trick-count') ?? '0'),
    };
  });
}

async function readLastEvent(page) {
  return page.evaluate(() => window.tractorTest?.getLastEvent?.() ?? null);
}

async function recoverHumanSelection(page, rejectEvent) {
  if (!rejectEvent) {
    return false;
  }

  return page.evaluate((rej) => {
    const testApi = window.tractorTest;
    const events = testApi?.getEvents?.() ?? [];
    const trickIndex = Number(rej.trickIndex ?? 0);

    const leadPlay = events.find((e) => e.type === 'play'
      && Number(e.trickIndex) === trickIndex
      && Number(e.trickPosition) === 1);

    const need = Math.max(
      1,
      Number(leadPlay?.cards?.length ?? 0) || Number(rej.cards?.length ?? 0) || 1,
    );
    const leadSuit = leadPlay?.cards?.[0]?.suit ?? null;

    const handCards = Array.from(document.querySelectorAll('[data-testid="hand-card"]'));
    if (handCards.length === 0) {
      return false;
    }

    for (const card of handCards) {
      if (card.classList.contains('selected')) {
        card.click();
      }
    }

    const sameSuit = [];
    const otherSuit = [];
    for (const card of handCards) {
      const key = card.getAttribute('data-card-key') ?? '';
      if (leadSuit && key.startsWith(`${leadSuit}-`)) {
        sameSuit.push(card);
      } else {
        otherSuit.push(card);
      }
    }

    const picked = [...sameSuit, ...otherSuit].slice(0, Math.min(need, handCards.length));
    for (const card of picked) {
      card.click();
    }

    return picked.length > 0;
  }, rejectEvent);
}

async function playOneGame(page, baseUrl, seed, timeoutMs, screenshotDir) {
  const gameUrl = `${baseUrl}/game?autotest=1&seed=${seed}`;
  await page.goto(gameUrl, { waitUntil: 'networkidle' });
  await page.waitForSelector('[data-testid="test-state"]', { timeout: 60000, state: 'attached' });

  const started = Date.now();
  let loops = 0;
  let rejectStreak = 0;
  let rejectKey = '';

  while (Date.now() - started < timeoutMs) {
    loops += 1;
    if (loops > 2500) {
      break;
    }

    const state = await readState(page);
    if (!state) {
      await page.waitForTimeout(120);
      continue;
    }

    if (state.phase === 'Finished') {
      break;
    }

    if (state.phase === 'Bidding') {
      const clicked = await clickActiveBidButton(page);
      if (!clicked) {
        await triggerTestButton(page, '[data-testid="btn-test-force-finalize-bid"]');
      }

      await page.waitForTimeout(180);
      continue;
    }

    if (state.phase === 'Burying') {
      const buryBtn = page.locator('[data-testid="btn-bury"]');
      if (await buryBtn.count() > 0) {
        await triggerTestButton(page, '[data-testid="btn-test-auto-bury-select"]');
        await humanClick(page, buryBtn);
      } else {
        await page.waitForTimeout(150);
      }
      continue;
    }

    if (state.phase === 'Playing') {
      if (state.currentPlayer === 0) {
        const lastEvent = await readLastEvent(page);
        if (lastEvent?.type === 'play_rejected' && lastEvent?.actor === 'human') {
          const key = `${lastEvent.trickIndex}:${lastEvent.trickPosition}:${lastEvent.reasonCode ?? ''}:${JSON.stringify(lastEvent.cards ?? [])}`;
          if (key === rejectKey) {
            rejectStreak += 1;
          } else {
            rejectKey = key;
            rejectStreak = 1;
          }
        } else if (lastEvent?.type === 'play' && lastEvent?.actor === 'human') {
          rejectStreak = 0;
          rejectKey = '';
        }

        if (rejectStreak >= 3) {
          const recovered = await recoverHumanSelection(page, lastEvent);
          if (!recovered) {
            await triggerTestButton(page, '[data-testid="btn-test-auto-select"]');
          }
          rejectStreak = 0;
        } else {
          await triggerTestButton(page, '[data-testid="btn-test-auto-select"]');
        }

        const playBtn = page.locator('[data-testid="btn-play"]');
        const enabled = await playBtn.isEnabled().catch(() => false);
        if (enabled) {
          await humanClick(page, playBtn);
        } else {
          await page.waitForTimeout(120);
        }
      } else {
        await page.waitForTimeout(180);
      }
      continue;
    }

    await page.waitForTimeout(120);
  }

  const finalState = await readState(page);
  const events = await page.evaluate(() => window.tractorTest?.getEvents?.() ?? []);

  const screenshotPath = path.join(screenshotDir, `game_seed_${seed}.png`);
  await page.screenshot({ path: screenshotPath, fullPage: true });

  const timedOut = !finalState || finalState.phase !== 'Finished';

  return {
    seed,
    timedOut,
    finalState,
    events,
    screenshot: screenshotPath,
  };
}

function renderSummaryMarkdown(results, outputDir) {
  const lines = [];
  const passed = results.filter((r) => r.analysis.pass && !r.timedOut).length;
  const failed = results.length - passed;

  lines.push('# UI 自动化测试报告');
  lines.push('');
  lines.push(`- 执行时间: ${new Date().toISOString()}`);
  lines.push(`- 总局数: ${results.length}`);
  lines.push(`- 通过: ${passed}`);
  lines.push(`- 失败: ${failed}`);
  lines.push(`- 报告目录: ${outputDir}`);
  lines.push('');

  lines.push('## 分局结果');
  lines.push('');

  for (const r of results) {
    lines.push(`### Seed ${r.seed} - ${r.analysis.pass && !r.timedOut ? 'PASS' : 'FAIL'}`);
    lines.push('');
    lines.push(`- 是否超时: ${r.timedOut ? '是' : '否'}`);
    lines.push(`- 墩数: ${r.analysis.metrics.trickCount ?? 'N/A'}`);
    lines.push(`- 终局闲家分: ${r.analysis.metrics.finalDefenderScore ?? 'N/A'}`);
    lines.push(`- 预期终局闲家分: ${r.analysis.metrics.expectedFinalScore ?? 'N/A'}`);
    lines.push(`- 抠底预期加分: ${r.analysis.metrics.bottomExpected ?? 'N/A'}`);
    lines.push(`- 非法出牌次数: ${r.analysis.metrics.rejectedPlays ?? 'N/A'}`);
    lines.push(`- 截图: ${r.screenshot}`);

    if (r.analysis.issues.length > 0) {
      lines.push('- 问题:');
      for (const issue of r.analysis.issues) {
        lines.push(`  - ${issue}`);
      }
    } else {
      lines.push('- 问题: 无');
    }

    lines.push('');
  }

  return `${lines.join('\n')}\n`;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const stamp = nowStamp();
  const outputDir = path.join(REPO_ROOT, 'unittest', 'ui', 'reports', `ui_campaign_${stamp}`);
  const screenshotDir = path.join(outputDir, 'screenshots');
  const rawDir = path.join(outputDir, 'raw');

  await ensureDir(outputDir);
  await ensureDir(screenshotDir);
  await ensureDir(rawDir);

  console.log(`[ui-test] 启动 WebUI: ${args.baseUrl}`);
  const server = startWebUi(args.baseUrl);

  try {
    await waitForServer(args.baseUrl, 120000);

    console.log('[ui-test] 启动 Chromium...');
    const browser = await chromium.launch({
      headless: !args.headful,
      slowMo: args.headful ? 80 : 0,
    });

    const context = await browser.newContext({
      viewport: { width: 1600, height: 900 },
    });

    const results = [];

    for (let i = 0; i < args.games; i += 1) {
      const seed = args.startSeed + i;
      console.log(`[ui-test] 开始第 ${i + 1}/${args.games} 局，seed=${seed}`);

      const page = await context.newPage();
      const run = await playOneGame(page, args.baseUrl, seed, args.timeoutMs, screenshotDir);
      const analysis = analyzeGame(run.events);

      if (run.timedOut) {
        analysis.pass = false;
        analysis.issues.unshift('游戏流程超时，未正常结束');
      }

      const result = {
        ...run,
        analysis,
      };

      results.push(result);

      const rawPath = path.join(rawDir, `game_seed_${seed}.json`);
      await fs.writeFile(rawPath, JSON.stringify(result, null, 2), 'utf8');

      await page.close();
    }

    await context.close();
    await browser.close();

    const summary = renderSummaryMarkdown(results, outputDir);
    await fs.writeFile(path.join(outputDir, 'summary.md'), summary, 'utf8');
    await fs.writeFile(path.join(outputDir, 'summary.json'), JSON.stringify(results, null, 2), 'utf8');

    const passCount = results.filter((r) => r.analysis.pass && !r.timedOut).length;
    const failCount = results.length - passCount;

    console.log(`[ui-test] 完成。通过 ${passCount}，失败 ${failCount}`);
    console.log(`[ui-test] 报告：${path.join(outputDir, 'summary.md')}`);

    return failCount > 0 ? 1 : 0;
  } finally {
    await stopWebUi(server);
  }
}

main()
  .then((code) => {
    process.exit(code);
  })
  .catch((err) => {
    console.error('[ui-test] 执行失败:', err);
    process.exit(1);
  });
