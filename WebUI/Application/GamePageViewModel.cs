using System.Collections.Generic;
using TractorGame.Core.Models;

namespace WebUI.Application;

public sealed class GamePageViewModel
{
    public List<Card> PlayerHand { get; set; } = new();
    public HashSet<int> SelectedCardIndices { get; } = new();
    public string? TrumpSuit { get; set; }
    public string? OpponentTrumpSuit { get; set; } = "NT";
    public int DefenderScore { get; set; }
    public string OurLevel { get; set; } = "2";
    public string TheirLevel { get; set; } = "2";
    public bool ShowBiddingPanel { get; set; }
    public bool ShowBuryButton { get; set; }
    public bool ShowViewBottomButton { get; set; }
}

