using System.Collections.Generic;
using CustomSpawns.AI;
using CustomSpawns.AI.Model;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.SaveSystem;

namespace CustomSpawns.Spawn.PartyComponents
{
    /**
     * This class defines the CustomSpawnsPartyComponent class as a saveable type.
     * This class does not need to be instantiated as Bannerlord will detect it automatically via reflection.
     */
    public class CustomSpawnsTypeDefiner : SaveableTypeDefiner
    {
        // This is a random number that is used to identify the type definition.
        // It must be unique from all other mods.
        public CustomSpawnsTypeDefiner() : base(7_955_635) { }

        protected override void DefineClassTypes()
        {
            AddClassDefinition(typeof(CustomSpawnsPartyComponent), 1);
            AddClassDefinition(typeof(CsPartyData), 2);
            AddClassDefinition(typeof(PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState), 3);
            AddClassDefinition(typeof(SettlementOffensiveBehaviourState), 4);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(SettlementOffensivePhase), 5);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(Dictionary<
                MobileParty, CsPartyData>));
            ConstructContainerDefinition(typeof(Dictionary<MobileParty,
                PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState>));
            ConstructContainerDefinition(typeof(Dictionary<MobileParty,
                SettlementOffensiveBehaviourState>));
        }
    }
}
