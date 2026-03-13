namespace TractorGame.Core.Logging
{
    /// <summary>
    /// 日志/规则统一原因码（v1.1）。
    /// </summary>
    public static class ReasonCodes
    {
        public const string PhaseInvalid = "PHASE_INVALID";
        public const string NotCurrentPlayer = "NOT_CURRENT_PLAYER";
        public const string CardNotInHand = "CARD_NOT_IN_HAND";
        public const string PlayPatternInvalid = "PLAY_PATTERN_INVALID";
        public const string ThrowNotMax = "THROW_NOT_MAX";
        public const string FollowCountMismatch = "FOLLOW_COUNT_MISMATCH";
        public const string FollowSuitRequired = "FOLLOW_SUIT_REQUIRED";
        public const string FollowPairRequired = "FOLLOW_PAIR_REQUIRED";
        public const string FollowTractorRequired = "FOLLOW_TRACTOR_REQUIRED";
        public const string BidNotLevelCard = "BID_NOT_LEVEL_CARD";
        public const string BidPriorityTooLow = "BID_PRIORITY_TOO_LOW";
        public const string BuryNot8Cards = "BURY_NOT_8_CARDS";
        public const string BuryCardNotFound = "BURY_CARD_NOT_FOUND";
        public const string AiNoValidCandidate = "AI_NO_VALID_CANDIDATE";
        public const string LogSinkIoError = "LOG_SINK_IO_ERROR";
        public const string UnknownError = "UNKNOWN_ERROR";
    }
}
