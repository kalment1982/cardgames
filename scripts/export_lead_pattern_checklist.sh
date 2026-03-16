#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT_PATH="$ROOT_DIR/TractorGame.csproj"
ASSEMBLY_PATH="$ROOT_DIR/bin/Debug/net6.0/TractorGame.dll"
OUTPUT_PATH="${1:-$ROOT_DIR/unittest/self/lead_pattern_matrix_checklist.md}"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/tractor-lead-pattern-export.XXXXXX")"

cleanup() {
  rm -rf "$TMP_DIR"
}
trap cleanup EXIT

dotnet build "$PROJECT_PATH" >/dev/null

cat > "$TMP_DIR/LeadPatternExporter.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
EOF

cat > "$TMP_DIR/Program.cs" <<'EOF'
using System.Collections;
using System.Reflection;
using System.Text;

static object? Get(object obj, string name) => obj.GetType().GetProperty(name)!.GetValue(obj);

static string JoinCards(IEnumerable cards)
{
    var parts = new List<string>();
    foreach (var card in cards)
        parts.Add(card?.ToString() ?? "null");
    return string.Join(" ", parts);
}

static string BoolText(bool value) => value ? "OK" : "FAIL";
static bool BoolAt(Array arr, int index) => (bool)arr.GetValue(index)!;
static string? StringAt(Array arr, int index) => (string?)arr.GetValue(index);

var assemblyPath = Environment.GetEnvironmentVariable("TRACTOR_ASSEMBLY_PATH")
    ?? throw new InvalidOperationException("TRACTOR_ASSEMBLY_PATH is required.");
var outputPath = Environment.GetEnvironmentVariable("TRACTOR_CHECKLIST_OUTPUT_PATH")
    ?? throw new InvalidOperationException("TRACTOR_CHECKLIST_OUTPUT_PATH is required.");

var assembly = Assembly.LoadFrom(assemblyPath);
var type = assembly.GetType("TractorGame.Tests.LeadPatternMatrixTests")
    ?? throw new InvalidOperationException("LeadPatternMatrixTests type not found.");

var suites = new (string MethodName, string Title)[]
{
    ("CoreCases", "核心全组合"),
    ("CutCases", "毙牌/主牌压过"),
    ("AllTrumpWeakerCases", "全主但仍压不过首发"),
    ("PartialSuitTrumpCases", "部分同花 + 主牌补齐"),
    ("ThrowBlockedCases", "甩牌失败阻断"),
    ("InvalidFollowCases", "非法跟牌错误码")
};

var sb = new StringBuilder();
sb.AppendLine("# 首发多张牌型矩阵检查清单");
sb.AppendLine();
sb.AppendLine($"- 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
sb.AppendLine("- 来源: `LeadPatternMatrixTests`");
sb.AppendLine("- 目的: 人工审查 4 家手牌/出牌、预期跟牌合法性、赢家、甩牌结果");
sb.AppendLine();

var grandTotal = 0;
foreach (var suite in suites)
{
    var method = type.GetMethod(suite.MethodName, BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Method not found: {suite.MethodName}");
    var cases = ((IEnumerable)method.Invoke(null, null)!)
        .Cast<object[]>()
        .Select(row => row[0]!)
        .ToList();

    grandTotal += cases.Count;
    sb.AppendLine($"## {suite.Title}（{cases.Count} 例）");
    sb.AppendLine();

    var index = 1;
    foreach (var testCase in cases)
    {
        var id = (string)Get(testCase, "Id")!;
        var n = (int)Get(testCase, "N")!;
        var m = (int)Get(testCase, "M")!;
        var j = (int)Get(testCase, "J")!;
        var systemKind = Get(testCase, "SystemKind")!.ToString()!;
        var note = (string)Get(testCase, "Note")!;
        var expectedWinner = (int)Get(testCase, "ExpectedWinner")!;
        var expectedThrow = Get(testCase, "ExpectedThrowSuccess");
        var hands = (Array)Get(testCase, "Hands")!;
        var plays = (Array)Get(testCase, "Plays")!;
        var expectedFollowSuccess = (Array)Get(testCase, "ExpectedFollowSuccess")!;
        var expectedFollowReason = (Array)Get(testCase, "ExpectedFollowReason")!;

        sb.AppendLine($"### {index:000}. {id}");
        sb.AppendLine();
        sb.AppendLine($"- 组合: `n={n}, m={m}, j={j}`");
        sb.AppendLine($"- 首发系统: `{systemKind}`");
        sb.AppendLine($"- 备注: `{note}`");

        for (var player = 0; player < 4; player++)
        {
            var hand = (IEnumerable)hands.GetValue(player)!;
            var play = (IEnumerable)Get(plays.GetValue(player)!, "Cards")!;
            sb.AppendLine($"- 玩家{player}手牌: `{JoinCards(hand)}`");
            sb.AppendLine($"- 玩家{player}出牌: `{JoinCards(play)}`");
        }

        var p1 = BoolAt(expectedFollowSuccess, 1) ? "OK" : $"FAIL/{StringAt(expectedFollowReason, 1)}";
        var p2 = BoolAt(expectedFollowSuccess, 2) ? "OK" : $"FAIL/{StringAt(expectedFollowReason, 2)}";
        var p3 = BoolAt(expectedFollowSuccess, 3) ? "OK" : $"FAIL/{StringAt(expectedFollowReason, 3)}";
        sb.AppendLine($"- 跟牌预期: `P1={p1}, P2={p2}, P3={p3}`");
        sb.AppendLine(expectedWinner >= 0 ? $"- 赢家预期: `玩家{expectedWinner}`" : "- 赢家预期: `不校验`");
        sb.AppendLine(expectedThrow is null
            ? "- 甩牌预期: `不校验`"
            : $"- 甩牌预期: `{BoolText((bool)expectedThrow)}`");
        sb.AppendLine();

        index++;
    }
}

var headerInsert = $"- 用例总数: `{grandTotal}`{Environment.NewLine}{Environment.NewLine}";
sb.Insert(sb.ToString().IndexOf(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal) + 2, headerInsert);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(false));

Console.WriteLine(outputPath);
Console.WriteLine($"cases={grandTotal}");
EOF

TRACTOR_ASSEMBLY_PATH="$ASSEMBLY_PATH" \
TRACTOR_CHECKLIST_OUTPUT_PATH="$OUTPUT_PATH" \
dotnet run --project "$TMP_DIR/LeadPatternExporter.csproj"
