using Assets.Scripts.Objects.Entities;
using HarmonyLib;

namespace ChatMod
{
    /// <summary>Refresh name-color cache when suit changes.</summary>
    [HarmonyPatch(typeof(Human), "OnSuitOccupantChanged")]
    public static class HumanSuitColorCachePatch
    {
        private static void Postfix(Human __instance)
        {
            if (__instance == null)
                return;

            var hex = PlayerNameColorCache.SuitColorToHex(__instance);
            PlayerNameColorCache.Set(__instance.ReferenceId, hex);
        }
    }
}
