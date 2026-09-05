using System.Collections.Generic;
using CustomSpawns.AI.Behaviours;
using CustomSpawns.AI.Model;
using CustomSpawns.Invasions.Adapters;
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
            AddClassDefinition(typeof(InvasionSaveData), 6);
            AddClassDefinition(typeof(InvasionGroupSaveData), 7);
            AddClassDefinition(typeof(InvasionCaptureSaveData), 8);
            AddClassDefinition(typeof(InvasionCreditSaveData), 9);
            AddClassDefinition(typeof(InvasionCaptureEvidenceSaveData), 10);
        }

        protected override void DefineEnumTypes()
        {
            AddEnumDefinition(typeof(SettlementOffensivePhase), 5);
        }

        protected override void DefineContainerDefinitions()
        {
            ConstructContainerDefinition(typeof(List<InvasionGroupSaveData>));
            ConstructContainerDefinition(typeof(List<InvasionCaptureSaveData>));
            ConstructContainerDefinition(typeof(List<InvasionCreditSaveData>));
            ConstructContainerDefinition(typeof(List<InvasionCaptureEvidenceSaveData>));
            ConstructContainerDefinition(typeof(List<MobileParty>));
            ConstructContainerDefinition(typeof(List<string>));
            ConstructContainerDefinition(typeof(Dictionary<string, string>));
            ConstructContainerDefinition(typeof(Dictionary<
                MobileParty, CsPartyData>));
            ConstructContainerDefinition(typeof(Dictionary<MobileParty,
                PatrolAroundClosestLestInterruptedAndSwitchBehaviour.PatrolState>));
            ConstructContainerDefinition(typeof(Dictionary<MobileParty,
                SettlementOffensiveBehaviourState>));
        }
    }
}
