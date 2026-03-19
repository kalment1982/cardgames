using TractorGame.Core.AI.V30.Bottom;
using Xunit;

namespace TractorGame.Tests.V30.Bottom
{
    public class EndgameControlPolicyV30Tests
    {
        [Fact]
        public void DecideJokerOrder_UsesDefaultSmallThenBig_WhenNoExceptionSignal()
        {
            var policy = new EndgameControlPolicyV30();
            var decision = policy.DecideJokerOrder(new JokerControlInputV30
            {
                BigJokerUnplayedLikelyInRearOpponent = false,
                RearOpponentLikelyHasStrongerTrumpStructure = false,
                SmallJokerSecurity = WinSecurityTierV30.StableWin
            });

            Assert.True(decision.ShouldPlaySmallJokerFirst);
            Assert.Equal("Default_SmallThenBig", decision.Reason);
        }

        [Fact]
        public void DecideJokerOrder_DoesNotPlaySmallFirst_WhenBigJokerLikelyInRear()
        {
            var policy = new EndgameControlPolicyV30();
            var decision = policy.DecideJokerOrder(new JokerControlInputV30
            {
                BigJokerUnplayedLikelyInRearOpponent = true,
                RearOpponentLikelyHasStrongerTrumpStructure = false,
                SmallJokerSecurity = WinSecurityTierV30.StableWin
            });

            Assert.False(decision.ShouldPlaySmallJokerFirst);
            Assert.Equal("RearLikelyHasBigJoker", decision.Reason);
        }

        [Fact]
        public void DecideJokerOrder_DoesNotPlaySmallFirst_WhenSmallOnlyFragileWin()
        {
            var policy = new EndgameControlPolicyV30();
            var decision = policy.DecideJokerOrder(new JokerControlInputV30
            {
                BigJokerUnplayedLikelyInRearOpponent = false,
                RearOpponentLikelyHasStrongerTrumpStructure = false,
                SmallJokerSecurity = WinSecurityTierV30.FragileWin
            });

            Assert.False(decision.ShouldPlaySmallJokerFirst);
            Assert.Equal("SmallJokerOnlyFragileWin", decision.Reason);
        }

        [Fact]
        public void ResolveTrumpControl_FreezesTrumps_WhenStrongProtectBottom()
        {
            var policy = new EndgameControlPolicyV30();
            var decision = policy.ResolveTrumpControl(new EndgameControlInputV30
            {
                OperationalMode = BottomOperationalModeV30.StrongProtectBottom,
                CurrentTrickPoints = 5
            });

            Assert.True(decision.FreezeTrumpResources);
            Assert.True(decision.AllowConcedeLowPointTrick);
        }

        [Fact]
        public void ResolveTrumpControl_DoesNotFreeze_WhenNotStrongProtectBottom()
        {
            var policy = new EndgameControlPolicyV30();
            var decision = policy.ResolveTrumpControl(new EndgameControlInputV30
            {
                OperationalMode = BottomOperationalModeV30.ProtectBottomAttention,
                CurrentTrickPoints = 5
            });

            Assert.False(decision.FreezeTrumpResources);
            Assert.False(decision.AllowConcedeLowPointTrick);
        }
    }
}
