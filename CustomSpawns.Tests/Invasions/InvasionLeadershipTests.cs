using System.Collections.Generic;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionLeadershipTests
    {
        private readonly HashSet<string> _living = new();
        private string? _rulingClan;
        private Invasion _invasion = null!;

        [SetUp]
        public void SetUp()
        {
            _living.Clear();
            _rulingClan = null;
            _invasion = new Invasion("humar", "cs_humar");
            _invasion.AddClan("cs_humar_1");
            _invasion.AddClan("cs_humar_2");
        }

        [Test]
        public void ALivingFounderLeadsItsInvasion()
        {
            _living.UnionWith(new[] { "cs_humar", "cs_humar_1", "cs_humar_2" });
            _rulingClan = "cs_humar_2";

            Assert.That(FindLeadingClan(), Is.EqualTo("cs_humar"));
        }

        [Test]
        public void ADeadFoundersInvasionPassesToTheClanThatRulesInItsPlace()
        {
            _living.UnionWith(new[] { "cs_humar_1", "cs_humar_2" });
            _rulingClan = "cs_humar_2";

            Assert.That(FindLeadingClan(), Is.EqualTo("cs_humar_2"));
        }

        [Test]
        public void ARealmRuledFromOutsideTheInvasionLeavesItToAnySurvivingClan()
        {
            _living.UnionWith(new[] { "cs_humar_2", "vlandia_1" });
            _rulingClan = "vlandia_1";

            Assert.That(FindLeadingClan(), Is.EqualTo("cs_humar_2"));
        }

        [Test]
        public void AnInvasionWithNoSurvivingClanHasNobodyToLeadIt()
        {
            _rulingClan = "cs_humar_2";

            Assert.That(FindLeadingClan(), Is.Null);
        }

        [Test]
        public void ALandingInvasionNeedsNoRealm()
        {
            Assert.That(_invasion.HasRealm(rulesAKingdom: false), Is.True);
        }

        [Test]
        public void AnInvasionThatTookItsCapitalButHasYetToFoundItsKingdomStillFights()
        {
            _invasion.CapitalSettlementId = "town_ES2";

            Assert.That(_invasion.HasRealm(rulesAKingdom: false), Is.True);
        }

        [Test]
        public void AnInvasionThatFoundedItsKingdomFightsForTheRealmItRules()
        {
            _invasion.CapitalSettlementId = "town_ES2";
            _invasion.IsRealmFounded = true;

            Assert.That(_invasion.HasRealm(rulesAKingdom: true), Is.True);
        }

        [Test]
        public void AnInvasionWhoseRealmFellHasNothingLeftToFightFor()
        {
            _invasion.CapitalSettlementId = "town_ES2";
            _invasion.IsRealmFounded = true;

            Assert.That(_invasion.HasRealm(rulesAKingdom: false), Is.False);
        }

        private string? FindLeadingClan() =>
            _invasion.FindLeadingClan(clanId => _living.Contains(clanId), _ => _rulingClan);
    }
}
