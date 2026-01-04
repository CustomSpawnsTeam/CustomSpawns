using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CustomSpawns.Data.Dao;
using CustomSpawns.Utils;
using HarmonyLib;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.Party;
using static HarmonyLib.AccessTools;

namespace CustomSpawns.HarmonyPatches
{
    public class GetUnitValueForFactionPatch: IPatch
    {
        private static SpawnDao _spawnDao; 
        private static readonly MethodInfo? GetUnitValueForFactionMethodInfo = typeof(SafePassageBarterable)
            .GetMethod("GetUnitValueForFaction", all);
        private static readonly MethodInfo PostfixMethodInfo = typeof(GetUnitValueForFactionPatch)!
            .GetMethod("Postfix", all)!;
        public GetUnitValueForFactionPatch(SpawnDao spawnDao)
        {
            _spawnDao = spawnDao;
        }
        
        //TODO make this alterable.
        static void Postfix(ref int __result)
        {
            if (MobileParty.ConversationParty == null || MobileParty.ConversationParty.IsBandit)
            {
                return;
            }
            string isolatedPartyStringId = CampaignUtils.IsolateMobilePartyStringID(MobileParty.ConversationParty);
            ISet<string> partySpawns = _spawnDao.FindAllPartyTemplateId();
            ISet<string> subSpawnParties = _spawnDao.FindAllSubPartyTemplateId();
            if (partySpawns.Any(spawn => spawn.Equals(isolatedPartyStringId))
                || subSpawnParties.Any(spawn => spawn.Equals(isolatedPartyStringId)))
            {
                __result /= 8;
            }
        }

        public bool IsApplicable()
        {
            return GetUnitValueForFactionMethodInfo != null;
        }

        public void Apply(Harmony instance)
        {
            instance.Patch(GetUnitValueForFactionMethodInfo,
                postfix: new HarmonyMethod(PostfixMethodInfo));
        }
    }
}
