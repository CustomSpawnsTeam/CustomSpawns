using HarmonyLib;

namespace CustomSpawns.HarmonyPatches
{
    public interface IPatch
    {
        bool IsApplicable();
        void Apply(Harmony instance);
    }
}