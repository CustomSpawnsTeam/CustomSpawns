using System.Collections.Generic;
using CustomSpawns.Config;
using CustomSpawns.Data.Dao;
using CustomSpawns.PartySpeed;
using CustomSpawns.Utils;
using HarmonyLib;

namespace CustomSpawns.HarmonyPatches
{
    public class PatchManager
    {
        private readonly SpawnDao _spawnDao;
        private readonly PartySpeedContext _partySpeedContext;
        private readonly ConfigLoader _configLoader;
        private readonly MessageBoxService _messageBoxService;
        private bool IsApplied { get; set; }

        public PatchManager(SpawnDao spawnDao, PartySpeedContext partySpeedContext, ConfigLoader configLoader, MessageBoxService messageBoxService)
        {
            _spawnDao = spawnDao;
            _partySpeedContext = partySpeedContext;
            _configLoader = configLoader;
            _messageBoxService = messageBoxService;
        }

        public void ApplyPatches()
        {
            if (IsApplied)
            {
                return;
            }

            try
            {
                Harmony harmony = new Harmony("com.Questry.CustomSpawns");
                // TODO Should create a generic way to implement all patches instead of relying on automatic instantiation
                List<IPatch> patches = new()
                {
                    new RemovePartyTrackersFromNonBanditPartiesPatch(_spawnDao),
                    new PartySpeedModelPatch(_partySpeedContext),
                    new MapScreenPatch(_configLoader),
                    new GetUnitValueForFactionPatch(_spawnDao)
                };

                int patched = 0;
                foreach (IPatch patch in patches)
                {
                    if (patch.IsApplicable())
                    {
                        patch.Apply(harmony);
                        patched++;
                    }
                }

                if (patched != patches.Count)
                {
                    _messageBoxService.ShowMessage("CustomSpawns: Could not apply all harmony patches");   
                }

                harmony.PatchAll();
                IsApplied = true;
            }
            catch (System.Exception e)
            {
                _messageBoxService.ShowCustomSpawnsErrorMessage(e, "HARMONY PATCHES");
            }
        }
    }
}