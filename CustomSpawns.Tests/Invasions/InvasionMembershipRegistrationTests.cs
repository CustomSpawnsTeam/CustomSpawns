using System;
using CustomSpawns.Invasions;
using NUnit.Framework;

namespace CustomSpawns.Tests.Invasions
{
    public class InvasionMembershipRegistrationTests
    {
        private InvasionState _invasionState = null!;

        [SetUp]
        public void SetUp()
        {
            _invasionState = new InvasionState();
        }

        [TestCase(false, true, "Original")]
        [TestCase(false, false, "Escort")]
        [TestCase(true, false, "Adopted")]
        [TestCase(true, true, "Adopted")]
        public void ANewInvasionDerivesTheRoleInsteadOfReusingASavedRole(bool adopted, bool primary, string expected)
        {
            string? role = _invasionState.RegisterParty("party", "template", ("clan", false, false),
                ("old-role", adopted, primary), templateId =>
                {
                    Assert.AreEqual("template", templateId);
                    return true;
                });

            Assert.AreEqual(expected, role);
            Assert.That(_invasionState.FindInvasion("clan")!.MemberPartyIds, Contains.Item("party"));
        }

        [TestCase(null, "clan", false, false)]
        [TestCase("template", null, false, false)]
        [TestCase("template", "clan", true, false)]
        [TestCase("template", "clan", false, true)]
        public void AnIneligiblePartyCannotFoundAnInvasionOrQueryConfiguration(string? templateId, string? clanId,
            bool eliminated, bool bandit)
        {
            Assert.IsNull(_invasionState.RegisterParty("party", templateId, (clanId, eliminated, bandit),
                ("old-role", false, true), UnexpectedConfigurationQuery));
            Assert.That(_invasionState.Invasions, Is.Empty);
        }

        [Test]
        public void MissingConfigurationPreventsANewInvasion()
        {
            Assert.IsNull(_invasionState.RegisterParty("party", "template", ("clan", false, false),
                (null, false, true), _ => false));
            Assert.That(_invasionState.Invasions, Is.Empty);
        }

        [TestCase("saved-role")]
        [TestCase("")]
        [TestCase("Adopted")]
        public void SavedMembershipAndRoleSurviveSuspensionEliminationAndRemovedConfiguration(string savedRole)
        {
            Invasion invasion = _invasionState.RegisterMember("template", "clan", "original");
            invasion.IsSuspended = true;

            string? role = _invasionState.RegisterParty("party", "template", ("clan", true, true),
                (savedRole, false, true), UnexpectedConfigurationQuery);

            Assert.AreEqual(savedRole, role);
            Assert.That(invasion.MemberPartyIds, Contains.Item("party"));
            Assert.True(invasion.IsSuspended);
        }

        [TestCase(false, true, "Original")]
        [TestCase(false, false, "Escort")]
        [TestCase(true, false, "Adopted")]
        public void SavedMembershipWithoutARoleDerivesOne(bool adopted, bool primary, string expected)
        {
            _invasionState.RegisterMember("template", "clan", "original");

            Assert.AreEqual(expected, _invasionState.RegisterParty("party", "template", ("clan", false, false),
                (null, adopted, primary), UnexpectedConfigurationQuery));
        }

        [TestCase(null)]
        [TestCase("other-template")]
        public void SharingAClanDoesNotAuthorizeAnotherTemplateOrRemoveExistingMembership(string? templateId)
        {
            Invasion invasion = _invasionState.RegisterMember("template", "clan", "party");

            Assert.IsNull(_invasionState.RegisterParty("party", templateId, ("clan", false, false),
                ("Original", false, true), UnexpectedConfigurationQuery));
            Assert.That(invasion.MemberPartyIds, Is.EquivalentTo(new[] { "party" }));
        }

        [Test]
        public void AnExpandedClanRegistersIntoTheSameSavedInvasion()
        {
            Invasion invasion = _invasionState.RegisterMember("template", "clan", "original");
            invasion.AddClan("expanded-clan");

            Assert.AreEqual("Escort", _invasionState.RegisterParty("escort", "template", ("expanded-clan", false, false),
                (null, false, false), UnexpectedConfigurationQuery));
            Assert.AreSame(invasion, _invasionState.FindInvasionOfParty("escort"));
            Assert.AreEqual(1, _invasionState.Invasions.Count);
        }

        private bool UnexpectedConfigurationQuery(string templateId) =>
            throw new InvalidOperationException("Configuration must not be read for this registration.");
    }
}
