using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Spawn.PartyComponents
{
    /**
     * This class is the implementation of a WarPartyComponent for all Custom Spawns parties.
     * This will make spawned parties part of their respective clan strength calculation.
     */
    public class CustomSpawnsPartyComponent : WarPartyComponent
    {
        [SaveableField(1)]
        private Hero _partyOwner = null!;

        [SaveableField(2)]
        private TextObject _name = null!;

        [SaveableField(3)]
        private Settlement _homeSettlement = null!;

        [SaveableField(4)]
        private Hero? _leader;

        private bool _isRegisteredWithClan;

        public CustomSpawnsPartyComponent(Hero partyOwner, TextObject name, Settlement homeSettlement)
        {
            _partyOwner = partyOwner;
            _name = name;
            _homeSettlement = homeSettlement;
        }

        /**
         * This is the clan that owns the party.
         * Generally this would be the clan leader, but when a lord has been generated to lead the
         * party it has to be that lord: Bannerlord dereferences Party.Owner.PartyBelongedTo when the
         * party captures a fortification, and the clan leader belongs to no party.
         * Vanilla keeps the same invariant in LordPartyComponent.OnChangePartyLeader.
         */
        public override Hero PartyOwner => _leader ?? _partyOwner;

        /**
         * This is the name of the party on the campaign map.
         */
        public override TextObject Name => _name;

        /**
         * This is the settlement the party has spawned at.
         */
        public override Settlement HomeSettlement => _homeSettlement;

        /**
         * This is the hero leading the party on the campaign map.
         * It is null unless the party was given a generated lord, which sieges require.
         */
        public override Hero Leader => _leader!;

        /**
         * Makes the given hero lead this party, enrolling it at the front of the member roster
         * so that Bannerlord tracks the hero as belonging to this party.
         */
        public void AssignLeader(Hero leader)
        {
            _leader = leader;
            if (MobileParty.MemberRoster.FindIndexOfTroop(leader.CharacterObject) < 0)
            {
                MobileParty.AddElementToMemberRoster(leader.CharacterObject, 1, true);
            }

            ApplyPartyInvariants();
        }

        protected override void OnMobilePartySetOnCreation()
        {
            ApplyPartyInvariants();
        }

        protected override void OnInitialize()
        {
            RestoreLegacyState();
            ApplyPartyInvariants();

            if (!_isRegisteredWithClan && ShouldRegisterWithClan(MobileParty.ActualClan))
            {
                base.OnInitialize();
                _isRegisteredWithClan = true;
            }
        }

        protected override void OnFinalize()
        {
            if (ShouldFinalizeWithClan(_isRegisteredWithClan))
            {
                base.OnFinalize();
                _isRegisteredWithClan = false;
            }
        }

        private void RestoreLegacyState()
        {
            LegacyPartyState state = RepairLegacyState(_partyOwner, MobileParty.Party.Owner, MobileParty.ActualClan,
                _name, MobileParty.Party.CustomName, _homeSettlement, MobileParty.HomeSettlement);
            _partyOwner = state.PartyOwner!;
            _name = state.Name!;
            _homeSettlement = state.HomeSettlement!;
        }

        public static LegacyPartyState RepairLegacyState(Hero? savedOwner, Hero? currentOwner, Clan? actualClan,
            TextObject? savedName, TextObject? currentName, Settlement? savedHomeSettlement, Settlement? currentHomeSettlement)
        {
            Hero? partyOwner = savedOwner ?? currentOwner;
            if (partyOwner?.Clan == null && actualClan?.Leader != null)
            {
                partyOwner = actualClan.Leader;
            }

            return new LegacyPartyState(partyOwner,
                savedName ?? currentName, savedHomeSettlement ?? currentHomeSettlement);
        }

        public static bool ShouldRegisterWithClan(Clan? clan)
        {
            return clan != null;
        }

        public static bool ShouldFinalizeWithClan(bool isRegisteredWithClan)
        {
            return isRegisteredWithClan;
        }

        private void ApplyPartyInvariants()
        {
            Hero? partyOwner = PartyOwner;
            if (partyOwner?.Clan != null)
            {
                MobileParty.ActualClan = partyOwner.Clan;
                MobileParty.Party.SetCustomOwner(partyOwner);
            }

            if (_name != null)
            {
                MobileParty.Party.SetCustomName(_name);
            }

            if (_homeSettlement != null)
            {
                MobileParty.SetCustomHomeSettlement(_homeSettlement);
            }

            MobileParty.ShouldJoinPlayerBattles = true;
            MobileParty.Party.SetVisualAsDirty();
        }

        public readonly struct LegacyPartyState
        {
            public Hero? PartyOwner { get; }
            public TextObject? Name { get; }
            public Settlement? HomeSettlement { get; }

            public LegacyPartyState(Hero? partyOwner, TextObject? name, Settlement? homeSettlement)
            {
                PartyOwner = partyOwner;
                Name = name;
                HomeSettlement = homeSettlement;
            }
        }
    }
}
