using System;
using Assets.Scripts.Localization2;
using Assets.Scripts.UI;
using HarmonyLib;
using TMPro;
using UnityEngine.Events;
using GameString = Assets.Scripts.Localization2.GameString;

namespace ChatMod.Patches
{
    /// <summary>Intercepts vanilla chat input; opens mod UI instead.</summary>
    [HarmonyPatch(typeof(InputWindow), nameof(InputWindow.GetText))]
    public static class InputWindowGetTextPatch
    {
        private static bool Prefix(
            GameString title,
            Action<string, string> onSubmit,
            UnityAction<string> onValueChanged,
            string defaultText,
            int characterLimit,
            TMP_InputField.ContentType contentType,
            int width)
        {
            if (title != GameStrings.InputChatMessage)
                return true;

            ChatUiBootstrap.EnsureExists();
            ChatUiBootstrap.Instance?.OpenFromGameInput();
            return false;
        }
    }
}
