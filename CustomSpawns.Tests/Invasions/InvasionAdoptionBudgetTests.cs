using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using CustomSpawns.Data.Adapter;
using CustomSpawns.Data.Dao;
using CustomSpawns.Data.Dto;
using CustomSpawns.Invasions;
using CustomSpawns.Invasions.Adapters;
using CustomSpawns.Spawn;
using NUnit.Framework;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CustomSpawns.Tests.Invasions
{
    /**
     * Adoption as the campaign runs it: the budget the configuration fields, what that budget counts,
     * and the party data adoption leaves behind. The campaign objects adoption reads are stood up by
     * hand, since what is under test is the registration call itself and not a stub of it.
     */
    public class InvasionAdoptionBudgetTests
    {
        private InvasionState _state = null!;
        private DynamicSpawnData _dynamicSpawnData = null!;
        private Dictionary<MobileParty, CsPartyData> _registry = null!;
        private MBList<MobileParty> _liveParties = null!;

        [SetUp]
        public void SetUp()
        {
            _state = new InvasionState();
            _registry = new Dictionary<MobileParty, CsPartyData>();
            _liveParties = new MBList<MobileParty>();
            _dynamicSpawnData = (DynamicSpawnData)FormatterServices.GetUninitializedObject(typeof(DynamicSpawnData));
            SetField(_dynamicSpawnData, "_dynamicSpawnData", _registry);
            UseCampaign(_liveParties);
        }

        [TearDown]
        public void TearDown()
        {
            SetCurrentCampaign(null);
        }

        [Test]
        public void TheBudgetIsThePrimariesTheSpawnerKeepsPlusTheLandingsTheInvasionMayHold()
        {
            Assert.AreEqual(6, InvasionMembership.PartyBudget(Spawn(maximumOnMap: 2, maxConcurrentParties: 3)));
        }

        [Test]
        public void TheBudgetCountsTheLandingsOfEveryReinforcementRule()
        {
            Assert.AreEqual(9, InvasionMembership.PartyBudget(Spawn(maximumOnMap: 2, new[] { 3, 2, 1 })));
        }

        // The spawner tests its count before incrementing it, so MaximumOnMap names one primary fewer than it keeps.
        [Test]
        public void TheBudgetAllowsThePrimaryTheSpawnerKeepsBeyondMaximumOnMap()
        {
            Assert.AreEqual(2, InvasionMembership.PartyBudget(Spawn(maximumOnMap: 1, maxConcurrentParties: 0)));
        }

        [Test]
        public void ASpawnThatInvadesForNothingBudgetsNothing()
        {
            SpawnDto spawn = Spawn(maximumOnMap: 5, maxConcurrentParties: 5);
            spawn.Invasion = null;

            Assert.AreEqual(0, InvasionMembership.PartyBudget(spawn));
            Assert.AreEqual(0, InvasionMembership.PartyBudget(null));
        }

        [Test]
        public void ARebuiltLordPartyIsRegisteredAsAdoptedAndNotAsAnIndependentReinforcement()
        {
            Member("invasion_party_1", Primary());
            MobileParty lord = LordParty("rebuilt_lord", "clan");

            Adoption(Spawn(maximumOnMap: 0, maxConcurrentParties: 1)).Adopt(lord);

            CsPartyData? adopted = _dynamicSpawnData.GetDynamicSpawnData(lord);
            Assert.NotNull(adopted);
            Assert.IsTrue(adopted!.IsAdopted);
            Assert.IsFalse(adopted.IsIndependentReinforcement);
            Assert.AreEqual("invasion", adopted.OwningSpawnTemplateId);
        }

        /**
         * A dead primary frees its spawner slot but leaves its escorts on the map, so escorts accumulate
         * past anything the configuration names. Counting them would put the invasion over budget for good.
         */
        [Test]
        public void LingeringEscortsDoNotExhaustTheAdoptionBudget()
        {
            Member("invasion_party_1", Primary());
            for (int i = 0; i < 6; i++)
            {
                Member("escort_" + i, Escort());
            }

            MobileParty lord = LordParty("rebuilt_lord", "clan");

            Adoption(Spawn(maximumOnMap: 1, maxConcurrentParties: 0)).Adopt(lord);

            Assert.IsTrue(_dynamicSpawnData.GetDynamicSpawnData(lord)?.IsAdopted);
        }

        [Test]
        public void AnInvasionFieldingAllTheAlliesItBudgetsForAdoptsNothing()
        {
            Member("invasion_party_1", Primary());
            Member("invasion_party_2", Reinforcement());
            MobileParty lord = LordParty("rebuilt_lord", "clan");

            Adoption(Spawn(maximumOnMap: 0, maxConcurrentParties: 1)).Adopt(lord);

            Assert.IsNull(_dynamicSpawnData.GetDynamicSpawnData(lord));
        }

        private InvasionMembership Adoption(SpawnDto spawn)
        {
            var spawnDao = (SpawnDao)FormatterServices.GetUninitializedObject(typeof(SpawnDao));
            SetField(spawnDao, "_spawns", new List<SpawnDto> { spawn });
            return new InvasionMembership(_state, spawnDao, _dynamicSpawnData, null!);
        }

        private static SpawnDto Spawn(int maximumOnMap, int maxConcurrentParties) =>
            Spawn(maximumOnMap, new[] { maxConcurrentParties });

        private static SpawnDto Spawn(int maximumOnMap, IReadOnlyList<int> maxConcurrentPartiesPerRule)
        {
            var template = (PartyTemplateObject)FormatterServices.GetUninitializedObject(typeof(PartyTemplateObject));
            template.StringId = "invasion";
            return new SpawnDto
            {
                PartyTemplate = template,
                MaximumOnMap = maximumOnMap,
                SpawnAlongWith = new ReadOnlyCollection<AccompanyingParty>(new List<AccompanyingParty>()),
                Invasion = new InvasionData(false, 0f, 0f, maxConcurrentPartiesPerRule
                    .Select((maxConcurrentParties, index) => new InvasionReinforcementRuleData("rule_" + index,
                        "reinforcement", 1f, maxConcurrentParties)).ToList())
            };
        }

        private void Member(string partyId, CsPartyData data)
        {
            _state.RegisterMember("invasion", "clan", partyId);
            _registry.Add(LordParty(partyId, "clan"), data);
        }

        private static CsPartyData Primary() => new("invasion", "invasion", true, null, null, "Original");
        private static CsPartyData Escort() => new("escort", "invasion", false, null, null, "Escort");
        private static CsPartyData Reinforcement() => new("invasion", "invasion", false, null, null, "Reinforcement");

        private MobileParty LordParty(string partyId, string clanId)
        {
            var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
            var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
            clan.StringId = clanId;
            party.StringId = partyId;
            party.ActualClan = clan;
            party.IsActive = true;
            SetField(party, "_partyComponent", FormatterServices.GetUninitializedObject(typeof(LordPartyComponent)));
            _liveParties.Add(party);
            return party;
        }

        // Adoption reads MobileParty.All and, through the registration it performs, Settlement.All.
        private static void UseCampaign(MBList<MobileParty> parties)
        {
            var objects = (CampaignObjectManager)FormatterServices.GetUninitializedObject(typeof(CampaignObjectManager));
            SetField(objects, "_mobileParties", parties);
            SetField(objects, "<Settlements>k__BackingField", new MBList<Settlement>());
            var campaign = (Campaign)FormatterServices.GetUninitializedObject(typeof(Campaign));
            SetField(campaign, "<CampaignObjectManager>k__BackingField", objects);
            SetCurrentCampaign(campaign);
        }

        private static void SetCurrentCampaign(Campaign? campaign) =>
            typeof(Campaign).GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!
                .SetValue(null, campaign);

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
