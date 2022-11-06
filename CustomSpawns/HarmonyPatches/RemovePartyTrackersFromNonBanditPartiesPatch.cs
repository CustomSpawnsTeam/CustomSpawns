using System.Linq;
using System.Reflection;
using CustomSpawns.Data;
using CustomSpawns.Data.Dao;
using CustomSpawns.Utils;
using HarmonyLib;
using SandBox.ViewModelCollection.Map;
using TaleWorlds.CampaignSystem.Party;
using static System.Reflection.BindingFlags;

namespace CustomSpawns.HarmonyPatches
{
    public class RemovePartyTrackersFromNonBanditPartiesPatch : IPatch
    {
        private static SpawnDao _spawnDao;
        private static readonly MethodInfo? CanAddPartyMethod = typeof(MapMobilePartyTrackerVM)
            .GetMethod("CanAddParty", NonPublic | Instance | DeclaredOnly);
        private static readonly MethodInfo PostfixMethod = typeof(RemovePartyTrackersFromNonBanditPartiesPatch)!
                .GetMethod("Postfix", NonPublic | Static | DeclaredOnly)!;

    public RemovePartyTrackersFromNonBanditPartiesPatch(SpawnDao spawnDao)
    {
        _spawnDao = spawnDao;
    }

    public bool IsApplicable()
    {
        return CanAddPartyMethod != null;
    }
    
    public void Apply(Harmony instance)
    {
        instance.Patch(CanAddPartyMethod, postfix: new HarmonyMethod(PostfixMethod));
    }

    static void Postfix(MobileParty party, ref bool __result)
    {
        if (__result)
        {
            string isolatedPartyStringId = CampaignUtils.IsolateMobilePartyStringID(party);
            if (_spawnDao.FindAllPartyTemplateId().Any(partyId => isolatedPartyStringId.Equals(partyId)))
                __result = false;
        }
    }
    }
}