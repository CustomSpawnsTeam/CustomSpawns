using CustomSpawns.Invasions;
using CustomSpawns.Invasions.CapitalObjectives;
using CustomSpawns.Invasions.Lines;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.Lines
{
    public class InvasionRealmCollapseTests
    {
        private Invasion _invasion = null!;
        private InvasionRealmCollapse _collapse = null!;

        [SetUp]
        public void SetUp()
        {
            _invasion = new Invasion("invasion", "clan") { CapitalSettlementId = "capital" };
            _collapse = new InvasionRealmCollapse();
        }

        [Test]
        public void ALandlessRealmTooWeakForTwoWeeksFalls()
        {
            Weak();

            Assert.False(_collapse.Observe(_invasion, 100, 0, holdsCapital: false));
            Assert.False(_collapse.Observe(_invasion, 113.9, 0, holdsCapital: false));
            Assert.True(_collapse.Observe(_invasion, 114, 0, holdsCapital: false));
        }

        [Test]
        public void ARealmStillHoldingAFiefNeverFalls()
        {
            Weak();

            _collapse.Observe(_invasion, 100, 1, holdsCapital: false);

            Assert.False(_collapse.Observe(_invasion, 200, 1, holdsCapital: false));
        }

        [Test]
        public void ARealmHoldingItsCapitalNeverFalls()
        {
            Weak();

            _collapse.Observe(_invasion, 100, 0, holdsCapital: true);

            Assert.False(_collapse.Observe(_invasion, 200, 0, holdsCapital: true));
        }

        [Test]
        public void ARealmStillTryingForItsCapitalNeverFalls()
        {
            _invasion.BeginCapitalAttack("capital", 0);

            _collapse.Observe(_invasion, 100, 0, holdsCapital: false);

            Assert.False(_collapse.Observe(_invasion, 200, 0, holdsCapital: false));
        }

        [Test]
        public void AnAttemptOnTheCapitalStartsTheTwoWeeksAgain()
        {
            Weak();
            _collapse.Observe(_invasion, 100, 0, holdsCapital: false);

            _invasion.CompleteCapitalObjective();
            _invasion.BeginCapitalAttack("capital", 110);
            _collapse.Observe(_invasion, 110, 0, holdsCapital: false);
            Weak();
            _collapse.Observe(_invasion, 112, 0, holdsCapital: false);

            Assert.False(_collapse.Observe(_invasion, 125.9, 0, holdsCapital: false));
            Assert.True(_collapse.Observe(_invasion, 126, 0, holdsCapital: false));
        }

        [Test]
        public void TheLastLostFiefStartsTheTwoWeeksAgain()
        {
            Weak();
            _collapse.Observe(_invasion, 100, 1, holdsCapital: false);

            _collapse.Observe(_invasion, 110, 0, holdsCapital: false);

            Assert.False(_collapse.Observe(_invasion, 123.9, 0, holdsCapital: false));
            Assert.True(_collapse.Observe(_invasion, 124, 0, holdsCapital: false));
        }

        private void Weak() =>
            _invasion.RestoreCapitalObjective(new InvasionCapitalObjective(InvasionCapitalObjectiveKind.Attack,
                InvasionCapitalObjectivePhase.Dormant, null, null, null,
                InvasionCapitalObjectiveDormantReason.NoViableTarget, null));
    }
}
