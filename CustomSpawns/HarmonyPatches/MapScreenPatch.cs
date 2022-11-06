using System.Reflection;
using CustomSpawns.Config;
using CustomSpawns.HarmonyPatches.Gameplay;
using TaleWorlds.CampaignSystem;
using HarmonyLib;
using TaleWorlds.InputSystem;
using SandBox.View.Map;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using static HarmonyLib.AccessTools;

namespace CustomSpawns.HarmonyPatches
{
    //The tick seems to get called whenever on the campaign map view, which makes sense. we can have some hotkeys here!
    public class MapScreenPatch : IPatch
    {
        private static readonly MethodInfo? TickMethod = typeof(MapScreen)
            .GetMethod("TaleWorlds.CampaignSystem.GameState.IMapStateHandler.Tick", all);
        private static readonly MethodInfo PostfixMethod = typeof(MapScreenPatch)!
            .GetMethod("Postfix", all)!;

        private static bool _trueSight;
        private static ConfigLoader _configLoader;
        
        public MapScreenPatch(ConfigLoader configLoader)
        {
            _configLoader = configLoader;
        }

        static void Postfix()
        {
                ProcessTrueSightControls();
                ProcessAdditionalPartySpottingRange();
        }

        static void ProcessTrueSightControls()
        {
            var mapInput = MapScreen.Instance.Input;

            if (mapInput == null)
            {
                return;
            }

            if (mapInput.IsKeyReleased(InputKey.T) && mapInput.IsControlDown())
            {
                _trueSight = !_trueSight;
            }

            if (_configLoader.Config.IsDebugMode)
            {
                Campaign.Current.TrueSight = _trueSight;
            }
        }

        static void ProcessAdditionalPartySpottingRange()
        {
            if (!MBGameManager.Current.CheatMode)
            {
                return;
            }

            var mapInput = MapScreen.Instance.Input;

            if (mapInput == null)
            {
                return;
            }

            if (mapInput.IsKeyReleased(InputKey.R) && mapInput.IsControlDown())
            {
                PartySpottingRangePatch.AdditionalSpottingRange -= 2;
                InformationManager.DisplayMessage(
                    new InformationMessage($"Additional Spotting Range is Now: {PartySpottingRangePatch.AdditionalSpottingRange}", Colors.Green));
            }


            if (mapInput.IsKeyReleased(InputKey.Y) && mapInput.IsControlDown())
            {
                PartySpottingRangePatch.AdditionalSpottingRange += 2;
                InformationManager.DisplayMessage(
                    new InformationMessage($"Additional Spotting Range is Now: {PartySpottingRangePatch.AdditionalSpottingRange}", Colors.Green));
            }
        }

        public bool IsApplicable()
        {
            return TickMethod != null;
        }

        public void Apply(Harmony instance)
        {
            instance.Patch(TickMethod,
                postfix: new HarmonyMethod(PostfixMethod));
        }
    }
}

//This is what the TW code looks like 1.5.9

//private void RegisterCheatHotkey(string id, InputKey hotkeyKey, HotKey.Modifiers modifiers, HotKey.Modifiers negativeModifiers = HotKey.Modifiers.None)
//{
//    base.RegisterHotKey(new HotKey(id, "Cheats", hotkeyKey, modifiers, negativeModifiers), true);
//}