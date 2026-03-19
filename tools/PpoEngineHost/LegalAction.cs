using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace PpoEngineHost;

public class LegalAction
{
    public List<Card> Cards { get; set; } = new();
    public string PatternType { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
    public bool IsLead { get; set; }
    public bool IsFollow { get; set; }
    public bool IsThrow { get; set; }
    public bool IsTrumpCut { get; set; }
    public string DebugKey { get; set; } = string.Empty;

    /// <summary>
    /// Serialize to anonymous object matching the JSON protocol.
    /// </summary>
    public object ToSerializable(int slot)
    {
        return new
        {
            slot,
            cards = Cards.Select(c => new
            {
                suit = c.IsJoker ? "Joker" : c.Suit.ToString(),
                rank = c.Rank.ToString(),
                score = c.Score,
                text = c.ToString()
            }).ToArray(),
            pattern_type = PatternType,
            system = System,
            is_lead = IsLead,
            is_follow = IsFollow,
            is_throw = IsThrow,
            is_trump_cut = IsTrumpCut,
            debug_key = DebugKey
        };
    }
}
