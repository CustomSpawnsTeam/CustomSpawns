using CustomSpawns.Invasions.CapitalObjectives;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions.CapitalObjectives
{
    public class CapitalArmyMobilizationPlannerTests
    {
        private CapitalArmyMobilizationPlanner _planner = null!;

        [SetUp]
        public void SetUp()
        {
            _planner = new CapitalArmyMobilizationPlanner();
        }

        [Test]
        public void StrongestExistingArmyLeaderCommandsAllMobilizableParties()
        {
            var candidates = new[]
            {
                Candidate("standalone", 300f, leadsArmy: false, canLeadArmy: true),
                Candidate("weaker-army", 400f, leadsArmy: true, canLeadArmy: true),
                Candidate("stronger-army", 500f, leadsArmy: true, canLeadArmy: true),
                Candidate("attached", 100f, leadsArmy: false, canLeadArmy: false)
            };

            CapitalArmyMobilizationPlan? plan = _planner.Create("invasion",
                InvasionCapitalObjectiveKind.Defence, "capital", candidates);

            Assert.That(plan, Is.Not.Null);
            Assert.That(plan!.CommanderPartyId, Is.EqualTo("stronger-army"));
            Assert.That(plan.PartyIds, Is.EqualTo(new[]
            {
                "stronger-army", "attached", "standalone", "weaker-army"
            }));
        }

        [Test]
        public void StrongestStandaloneLordCommandsWhenThereIsNoArmyLeader()
        {
            var candidates = new[]
            {
                Candidate("weak", 100f, leadsArmy: false, canLeadArmy: true),
                Candidate("strong", 200f, leadsArmy: false, canLeadArmy: true),
                Candidate("follower", 300f, leadsArmy: false, canLeadArmy: false)
            };

            CapitalArmyMobilizationPlan? plan = _planner.Create("invasion",
                InvasionCapitalObjectiveKind.Attack, "capital", candidates);

            Assert.That(plan!.CommanderPartyId, Is.EqualTo("strong"));
            Assert.That(plan.PartyIds, Does.Contain("follower"));
        }

        [Test]
        public void IneligiblePartiesAreDeferredWithReasons()
        {
            var candidates = new[]
            {
                Candidate("leader", 100f, leadsArmy: false, canLeadArmy: true),
                new CapitalArmyPartyCandidate("player", 500f, false, false, false,
                    "player_led"),
                new CapitalArmyPartyCandidate("battle", 400f, false, false, false,
                    "in_battle")
            };

            CapitalArmyMobilizationPlan? plan = _planner.Create("invasion",
                InvasionCapitalObjectiveKind.Defence, "capital", candidates);

            Assert.That(plan!.PartyIds, Is.EqualTo(new[] { "leader" }));
            Assert.That(plan.DeferredParties, Has.Count.EqualTo(2));
            Assert.That(plan.DeferredParties[0].PartyId, Is.EqualTo("battle"));
            Assert.That(plan.DeferredParties[0].Reason, Is.EqualTo("in_battle"));
            Assert.That(plan.DeferredParties[1].PartyId, Is.EqualTo("player"));
        }

        [Test]
        public void DuplicatePartyRecordsAreCollapsedDeterministically()
        {
            var candidates = new[]
            {
                new CapitalArmyPartyCandidate("leader", 90f, false, false, false,
                    "stale"),
                Candidate("leader", 100f, leadsArmy: false, canLeadArmy: true),
                Candidate("follower", 50f, leadsArmy: false, canLeadArmy: false),
                Candidate("follower", 50f, leadsArmy: false, canLeadArmy: false)
            };

            CapitalArmyMobilizationPlan? plan = _planner.Create("invasion",
                InvasionCapitalObjectiveKind.Attack, "capital", candidates);

            Assert.That(plan!.PartyIds, Is.EqualTo(new[] { "leader", "follower" }));
            Assert.That(plan.DeferredParties, Is.Empty);
        }

        [Test]
        public void NoCommanderLeavesTheObjectiveWaitingForAnotherHourlyAttempt()
        {
            var candidates = new[]
            {
                Candidate("follower", 100f, leadsArmy: false, canLeadArmy: false),
                new CapitalArmyPartyCandidate("blocked", 200f, false, true, true,
                    "in_battle")
            };

            CapitalArmyMobilizationPlan? plan = _planner.Create("invasion",
                InvasionCapitalObjectiveKind.Defence, "capital", candidates);

            Assert.That(plan, Is.Null);
        }

        [Test]
        public void EqualStrengthCommanderChoiceUsesOrdinalPartyId()
        {
            var candidates = new[]
            {
                Candidate("z-leader", 100f, leadsArmy: true, canLeadArmy: true),
                Candidate("a-leader", 100f, leadsArmy: true, canLeadArmy: true)
            };

            CapitalArmyMobilizationPlan? plan = _planner.Create("invasion",
                InvasionCapitalObjectiveKind.Defence, "capital", candidates);

            Assert.That(plan!.CommanderPartyId, Is.EqualTo("a-leader"));
        }

        private static CapitalArmyPartyCandidate Candidate(string partyId, float strength,
            bool leadsArmy, bool canLeadArmy) =>
            new(partyId, strength, true, leadsArmy, canLeadArmy);
    }
}
