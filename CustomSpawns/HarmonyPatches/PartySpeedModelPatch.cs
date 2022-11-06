using System.Reflection;
using CustomSpawns.PartySpeed;
using CustomSpawns.Utils;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using static HarmonyLib.AccessTools;

namespace CustomSpawns.HarmonyPatches
{
    public class PartySpeedModelPatch : IPatch
    {
        private static readonly MethodInfo? CalculateFinalSpeedMethod = typeof(DefaultPartySpeedCalculatingModel)
            .GetMethod("CalculateFinalSpeed", all);
        private static readonly MethodInfo PrefixMethod = typeof(PartySpeedModelPatch)!
            .GetMethod("Prefix", all)!;
        private static readonly MethodInfo PostfixMethod = typeof(PartySpeedModelPatch)!
            .GetMethod("Postfix", all)!;

        private static PartySpeedContext PartySpeedContext;
        
        private static readonly TextObject ExtraSpeedExplanationText = new("Custom Spawns Extra Speed Modification");

        public PartySpeedModelPatch(PartySpeedContext partySpeedContext)
        {
            PartySpeedContext = partySpeedContext;
        }
        
        private static bool Prefix(MobileParty mobileParty, ref ExplainedNumber finalSpeed)
        {
            if (!PartySpeedContext.IsPartySpeedBonusAllowedByUser())
            {
                return true;
            }

            string partyId = CampaignUtils.IsolateMobilePartyStringID(mobileParty); //TODO if this is non-trivial make it more efficient
            if (PartySpeedContext.IsPartyEligibleForExtraSpeed(partyId))
            {
                float extraSpeed = PartySpeedContext.GetSpeedWithExtraBonus(partyId);
                finalSpeed.Add(extraSpeed, ExtraSpeedExplanationText);
            }
            else if (PartySpeedContext.IsBasePartySpeedOverriden(partyId))
            {
                float overridenBaseSpeed = PartySpeedContext.GetBaseSpeed(partyId);
                finalSpeed = new ExplainedNumber(overridenBaseSpeed);
                return true;
            }

            return true;
        }

        private static void Postfix(MobileParty mobileParty, ref ExplainedNumber __result)
        {
            if (!PartySpeedContext.IsPartySpeedBonusAllowedByUser())
            {
                return;
            }

            string partyId = CampaignUtils.IsolateMobilePartyStringID(mobileParty); //TODO if this is non-trivial make it more efficient

            if (PartySpeedContext.IsPartyMinimumSpeedOverriden(partyId)) //minimum adjustment
                __result.LimitMin(PartySpeedContext.GetMinimumSpeed(partyId));

            if (PartySpeedContext.IsPartyMaximumSpeedOverriden(partyId))//maximum adjustment
                __result.LimitMax(PartySpeedContext.GetMaximumSpeed(partyId));
        }

        public bool IsApplicable()
        {
            return CalculateFinalSpeedMethod != null;
        }

        public void Apply(Harmony instance)
        {
            instance.Patch(CalculateFinalSpeedMethod,
                prefix: new HarmonyMethod(PrefixMethod),
                postfix: new HarmonyMethod(PostfixMethod));
        }
    }
}