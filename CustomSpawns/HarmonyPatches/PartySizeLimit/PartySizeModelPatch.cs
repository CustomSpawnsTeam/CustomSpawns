using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CustomSpawns.Spawn.PartySize;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using static HarmonyLib.AccessTools;

namespace CustomSpawns.HarmonyPatches.PartySizeLimit
{
    public class PartySizeModelPatch : IPatch
    {
        private static readonly MethodInfo? GetPartyMemberSizeLimitMethod = Campaign.Current?.Models?.PartySizeLimitModel.GetType()
            .GetMethod("GetPartyMemberSizeLimit", all);
        private static readonly MethodInfo PrefixMethod = typeof(PartySizeModelPatch)
            .GetMethod("Prefix", all)!;
        private static readonly MethodInfo PostfixMethod = typeof(PartySizeModelPatch)
            .GetMethod("Postfix", all)!;

        private static PartySizeCalculatedSubject? _partySizeCalculatedSubject;

        public PartySizeModelPatch(PartySizeCalculatedSubject partySizeCalculatedSubject)
        {
            _partySizeCalculatedSubject = partySizeCalculatedSubject;
        }

        [SuppressMessage("ReSharper", "All")]
        private static bool Prefix(ref bool includeDescriptions)
        {
            includeDescriptions = true;
            return true;
        }
        
        [SuppressMessage("ReSharper", "All")]
        private static void Postfix(PartyBase party, ref ExplainedNumber __result)
        {
            __result = _partySizeCalculatedSubject!.NotifyObservers(party.MobileParty, __result);
        }

        public bool IsApplicable()
        {
            return GetPartyMemberSizeLimitMethod != null;
        }

        public void Apply(Harmony instance)
        {
            instance.Patch(GetPartyMemberSizeLimitMethod,
                prefix: new HarmonyMethod(PrefixMethod),
                postfix: new HarmonyMethod(PostfixMethod));
        }
    }
}