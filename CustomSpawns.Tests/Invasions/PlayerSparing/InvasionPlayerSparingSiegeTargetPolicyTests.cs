using System.Linq;
using System.Runtime.Serialization;
using CustomSpawns.AI.Adapters;
using CustomSpawns.AI.Model;
using CustomSpawns.AI.Offensives;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Invasions.PlayerSparing.Adapters;
using Moq;
using NUnit.Framework;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace CustomSpawns.Tests.Invasions.PlayerSparing
{
    public class InvasionPlayerSparingSiegeTargetPolicyTests
    {
        private static readonly SpawnSettlementType[] Towns = { SpawnSettlementType.Town };

        [Test]
        public void FirstLanding_SkipsTheNearestTownThePlayerHoldsForTheNextOne()
        {
            MobileParty party = DummyParty();
            Settlement playerTown = DummySettlement();
            Settlement otherTown = DummySettlement();

            Settlement? result = Adapter(Sparing(playerTown),
                    Candidate(playerTown, 1f), Candidate(otherTown, 2f))
                .FindClosestSiegeTarget(party, 100f, 1.5f, Towns);

            Assert.That(result, Is.SameAs(otherTown));
        }

        [Test]
        public void FirstLanding_HasNoTargetWhenThePlayerHoldsEveryCandidate()
        {
            MobileParty party = DummyParty();
            Settlement nearTown = DummySettlement();
            Settlement farTown = DummySettlement();

            Settlement? result = Adapter(Sparing(nearTown, farTown),
                    Candidate(nearTown, 1f), Candidate(farTown, 2f))
                .FindClosestSiegeTarget(party, 100f, 1.5f, Towns);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void FirstLanding_KeepsTheNearestTownThePlayerDoesNotHold()
        {
            MobileParty party = DummyParty();
            Settlement nearTown = DummySettlement();
            Settlement farTown = DummySettlement();

            Settlement? result = Adapter(Sparing(), Candidate(nearTown, 1f), Candidate(farTown, 2f))
                .FindClosestSiegeTarget(party, 100f, 1.5f, Towns);

            Assert.That(result, Is.SameAs(nearTown));
        }

        [Test]
        public void Candidate_StillAnswersToTheWrappedPolicy()
        {
            var inner = new Mock<ISiegeTargetPolicy>();
            inner.Setup(policy => policy.IsCandidateAllowed(It.IsAny<MobileParty>(),
                It.IsAny<SiegeTargetCandidate>())).Returns(false);
            var policy = new InvasionPlayerSparingSiegeTargetPolicy(inner.Object, Sparing());

            Assert.That(policy.IsCandidateAllowed(DummyParty(), Candidate(DummySettlement(), 1f)), Is.False);
        }

        [Test]
        public void ExistingTarget_ThePlayerHoldsIsDroppedBeforeTheFirstLanding()
        {
            Settlement playerTown = DummySettlement();
            var policy = new InvasionPlayerSparingSiegeTargetPolicy(new NoRestrictionSiegeTargetPolicy(),
                Sparing(playerTown));

            Assert.That(policy.IsExistingTargetAllowed(DummyParty(), playerTown, false), Is.False);
        }

        [Test]
        public void ExistingTarget_AnAssaultAlreadyUnderWayIsFoughtOut()
        {
            Settlement playerTown = DummySettlement();
            var policy = new InvasionPlayerSparingSiegeTargetPolicy(new NoRestrictionSiegeTargetPolicy(),
                Sparing(playerTown));

            Assert.That(policy.IsExistingTargetAllowed(DummyParty(), playerTown, true), Is.True);
        }

        private static BannerlordBesiegeClosestSettlementAdapter Adapter(IInvasionLandingSparing sparing,
            params SiegeTargetCandidate[] candidates) =>
            new(_ => 5f, _ => new RoamingPartyStrength[0], _ => candidates, _ => new MobileParty[0],
                new InvasionPlayerSparingSiegeTargetPolicy(new NoRestrictionSiegeTargetPolicy(), sparing),
                (_, _) => new RoamingPartyStrength[0]);

        // Spares the given settlements, as an invasion before its first landing spares those the player holds.
        private static IInvasionLandingSparing Sparing(params Settlement[] playerHeld)
        {
            var sparing = new Mock<IInvasionLandingSparing>();
            sparing.Setup(s => s.MayBesiege(It.IsAny<MobileParty>(), It.IsAny<Settlement>()))
                .Returns((MobileParty _, Settlement settlement) => !playerHeld.Any(held => ReferenceEquals(held, settlement)));
            return sparing.Object;
        }

        private static SiegeTargetCandidate Candidate(Settlement target, float distance) =>
            new(target, true, true, SpawnSettlementType.Town, true, distance, 10f);

        private static MobileParty DummyParty() =>
            (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));

        private static Settlement DummySettlement() =>
            (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
    }
}
