using System.Collections.Generic;
using CustomSpawns.Config;
using CustomSpawns.Data.Dao;
using CustomSpawns.HarmonyPatches.PartySizeLimit;
using CustomSpawns.Spawn.PartySize;
using CustomSpawns.Utils;
using HarmonyLib;

namespace CustomSpawns.HarmonyPatches
{
    public class PatchManager
    {
        private readonly SpawnDao _spawnDao;
        private readonly ConfigLoader _configLoader;
        private readonly MessageBoxService _messageBoxService;
        private readonly PartySizeCalculatedSubject _partySizeCalculatedSubject;
        private bool IsApplied { get; set; }

        public PatchManager(
            SpawnDao spawnDao,
            ConfigLoader configLoader,
            MessageBoxService messageBoxService,
            PartySizeCalculatedSubject partySizeCalculatedSubject
        )
        {
            _spawnDao = spawnDao;
            _configLoader = configLoader;
            _messageBoxService = messageBoxService;
            _partySizeCalculatedSubject = partySizeCalculatedSubject;
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
                    new PartySizeModelPatch(_partySizeCalculatedSubject),
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