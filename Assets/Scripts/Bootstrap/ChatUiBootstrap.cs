using System.Collections.Generic;
using System.Linq;
using StationeersMods.Interface;
using UnityEngine;

namespace ChatMod
{
    /// <summary>
    /// Ensures a single <see cref="ChatUiBehaviour"/> exists. Prefabs come only from
    /// <see cref="ContentHandler.prefabs"/> (LaunchPad / mod content).
    /// </summary>
    public static class ChatUiBootstrap
    {
        /// <summary>Must match the prefab asset name in the mod content list (and your .prefab file name).</summary>
        public const string ModContentPrefabName = "ChatUiRoot";

        /// <summary>Must match the non-host row prefab root name in mod content.</summary>
        public const string MessageRowPrefabName = "MessageRow";

        /// <summary>Must match the host row prefab root name in mod content.</summary>
        public const string MessageRowHostPrefabName = "MessageRowHost";

        private static IEnumerable<GameObject>? _registeredPrefabs;
        private static ChatUiBehaviour? _instance;

        public static ChatUiBehaviour? Instance => _instance;

        /// <param name="contentHandler">From <see cref="ModBehaviour.OnLoaded"/>; supplies mod-registered prefabs.</param>
        public static void EnsureExists(ContentHandler? contentHandler = null)
        {
            if (contentHandler?.prefabs != null)
                _registeredPrefabs = contentHandler.prefabs;

            if (_instance != null)
                return;

            _instance = Object.FindObjectOfType<ChatUiBehaviour>();
            if (_instance != null)
                return;

            GameObject? prefab = null;
            if (_registeredPrefabs != null)
                prefab = _registeredPrefabs.FirstOrDefault(p => p != null && p.name == ModContentPrefabName);

            if (prefab == null)
            {
                ChatModLog.Error(
                    $"[ChatMod] No chat UI prefab. Register root named '{ModContentPrefabName}' in mod content (ContentHandler.prefabs).");
                return;
            }

            var root = Object.Instantiate(prefab);
            root.name = prefab.name;
            Object.DontDestroyOnLoad(root);

            _instance = root.GetComponent<ChatUiBehaviour>() ??
                        root.GetComponentInChildren<ChatUiBehaviour>(true);

            if (_instance == null)
            {
                ChatModLog.Error("[ChatMod] Chat UI prefab must include ChatUiBehaviour on the root or a child.");
                Object.Destroy(root);
            }
        }

        public static void OpenFromGameInput()
        {
            EnsureExists();
            _instance?.OpenFromGameInput();
        }

        public static void ToggleChatPanelHotkey()
        {
            EnsureExists();
            _instance?.ToggleChatPanelHotkey();
        }

        /// <summary>Resolves a mod-registered prefab by root GameObject name.</summary>
        public static GameObject? TryGetNamedPrefab(string assetName)
        {
            if (_registeredPrefabs == null)
                return null;
            return _registeredPrefabs.FirstOrDefault(x => x != null && x.name == assetName);
        }
    }
}
