using Assets.Scripts.Inventory;
using HarmonyLib;
using UnityEngine;

namespace ChatMod.Patches
{
    [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetButtonDown))]
    public static class KeyManagerGetButtonDownPatch
    {
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetButtonUp))]
    public static class KeyManagerGetButtonUpPatch
    {
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetButton))]
    public static class KeyManagerGetButtonPatch
    {
        private static bool Prefix(KeyCode key, ref bool __result)
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetRightAxis))]
    public static class KeyManagerGetRightAxisPatch
    {
        private static bool Prefix(ref float __result)
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
            {
                __result = 0f;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetForwardAxis))]
    public static class KeyManagerGetForwardAxisPatch
    {
        private static bool Prefix(ref float __result)
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
            {
                __result = 0f;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetAscend))]
    public static class KeyManagerGetAscendPatch
    {
        private static bool Prefix(ref float __result)
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
            {
                __result = 0f;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(KeyManager), nameof(KeyManager.GetDescend))]
    public static class KeyManagerGetDescendPatch
    {
        private static bool Prefix(ref float __result)
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
            {
                __result = 0f;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(InventoryManager), "CheckDisplaySlotInput")]
    public static class InventoryManagerCheckDisplaySlotInputPatch
    {
        private static bool Prefix()
        {
            if (ChatUiBehaviour.Instance != null && ChatUiBehaviour.Instance.IsInputActive)
                return false;
            return true;
        }
    }
}
