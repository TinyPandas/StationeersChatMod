using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChatMod.UI
{
    /// <summary>
    /// Clones the game's helmet/light hotkey row next to the stock HUD and wires it to mod chat.
    /// Driven from <see cref="ChatUiBehaviour"/> while in gameplay.
    /// </summary>
    public sealed class ChatHotkeyCloneMarker : MonoBehaviour
    {
        internal GameObject? BadgeRoot;
        internal TextMeshProUGUI? BadgeCountText;

        public void SetUnreadCount(int count)
        {
            count = Mathf.Max(0, count);
            if (BadgeRoot == null)
                return;

            BadgeRoot.SetActive(count > 0);
            if (BadgeCountText == null)
                return;

            BadgeCountText.text = count > 9 ? "9+" : count.ToString();
        }
    }

    public static class ChatHotkeyVanillaClone
    {
        private const string VanillaHelmetRowName = "HelmetHotkey";
        private const string VanillaLightRowName = "LightHotkey";
        private const string CloneRootName = "ChatHotkey";

        /// <summary>Assigned from <see cref="ChatUiBehaviour"/> (e.g. mod chat icon).</summary>
        public static Sprite? IconSpriteOverride;

        /// <summary>Shown on the cloned row key hint (e.g. F7).</summary>
        public static string KeyLabel = "F7";

        /// <summary>Icon rect uses this fraction of the smaller <c>IconBG</c> side (large sprites like message.png).</summary>
        public static float IconSlotFillFraction = 0.62f;

        /// <summary>Multiplier for <c>HotkeyHint</c> key TMP size (template font is often a bit large for the row).</summary>
        public static float KeyHintFontScale = 0.82f;

        public static void TryInstall()
        {
            var template = FindHotkeyRowTemplate();
            if (template == null)
                return;

            var scene = template.scene;
            if (HasMarkerInScene(scene))
                return;

            var parent = template.transform.parent;
            var clone = Object.Instantiate(template, parent, false);
            clone.name = CloneRootName;
            clone.SetActive(true);
            clone.transform.SetAsLastSibling();

            StripGameMonoBehaviours(clone);
            RenameIconAndApplySprite(clone.transform);
            ApplyKeyLabel(clone.transform, KeyLabel);
            RewireButton(clone.transform);

            var marker = clone.AddComponent<ChatHotkeyCloneMarker>();
            AttachUnreadBadge(clone.transform, marker);
        }

        /// <summary>Updates unread count on all cloned HUD rows in loaded scenes.</summary>
        public static void SetUnreadBadgeCount(int count)
        {
            foreach (var m in Object.FindObjectsOfType<ChatHotkeyCloneMarker>(true))
            {
                if (m != null)
                    m.SetUnreadCount(count);
            }
        }

        /// <summary>Updates key hint text on existing clones (does not re-apply font scale — use after <see cref="KeyLabel"/> changes).</summary>
        public static void RefreshKeyLabelOnClones()
        {
            foreach (var m in Object.FindObjectsOfType<ChatHotkeyCloneMarker>(true))
            {
                if (m != null)
                    SetKeyLabelTextOnly(m.transform, KeyLabel);
            }
        }

        private static void AttachUnreadBadge(Transform cloneRoot, ChatHotkeyCloneMarker marker)
        {
            var iconBg = cloneRoot.Find("IconBG") as RectTransform;
            if (iconBg == null)
                return;

            var badgeGo = new GameObject("UnreadBadge", typeof(RectTransform));
            var brt = badgeGo.GetComponent<RectTransform>();
            brt.SetParent(iconBg, false);
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(-8f, -8f);
            brt.sizeDelta = new Vector2(16f, 16f);

            var img = badgeGo.AddComponent<Image>();
            var tex = Texture2D.whiteTexture;
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            img.color = new Color(0.86f, 0.18f, 0.18f, 1f);
            img.raycastTarget = false;

            var textGo = new GameObject("Count", typeof(RectTransform));
            var trt = textGo.GetComponent<RectTransform>();
            trt.SetParent(brt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(-1f, -1f);
            trt.offsetMax = new Vector2(1f, 1f);

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 6f;
            tmp.fontSizeMax = 11f;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.text = "";

            var hint = cloneRoot.Find("HotkeyHint");
            TMP_Text? fontSrc = null;
            if (hint != null)
            {
                var key = hint.Find("Key");
                if (key != null)
                    fontSrc = key.GetComponent<TMP_Text>();
                fontSrc ??= hint.GetComponentInChildren<TMP_Text>(true);
            }

            if (fontSrc != null)
            {
                tmp.font = fontSrc.font;
                tmp.fontSharedMaterial = fontSrc.fontSharedMaterial;
            }

            marker.BadgeRoot = badgeGo;
            marker.BadgeCountText = tmp;
            badgeGo.SetActive(false);
        }

        private static GameObject? FindHotkeyRowTemplate()
        {
            return FindByExactName(VanillaHelmetRowName) ?? FindByExactName(VanillaLightRowName);
        }

        private static GameObject? FindByExactName(string objectName)
        {
            var found = GameObject.Find(objectName);
            if (found != null)
                return found;

            for (var si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var t = FindChildByNameRecursive(root.transform, objectName);
                    if (t != null)
                        return t.gameObject;
                }
            }

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name != objectName)
                    continue;
                var go = t.gameObject;
                if (!go.scene.IsValid() || !go.scene.isLoaded)
                    continue;
                return go;
            }

            return null;
        }

        private static Transform? FindChildByNameRecursive(Transform parent, string objectName)
        {
            if (parent.name == objectName)
                return parent;
            for (var i = 0; i < parent.childCount; i++)
            {
                var deep = FindChildByNameRecursive(parent.GetChild(i), objectName);
                if (deep != null)
                    return deep;
            }

            return null;
        }

        private static bool HasMarkerInScene(Scene scene)
        {
            var markers = Object.FindObjectsOfType<ChatHotkeyCloneMarker>(true);
            return markers.Any(m => m != null && m.gameObject.scene == scene);
        }

        private static void StripGameMonoBehaviours(GameObject root)
        {
            var list = new List<MonoBehaviour>(root.GetComponentsInChildren<MonoBehaviour>(true));
            foreach (var mb in list)
            {
                if (mb == null)
                    continue;
                var an = mb.GetType().Assembly.GetName().Name ?? string.Empty;
                if (an is "Assembly-CSharp" or "Assembly-CSharp-firstpass")
                    Object.Destroy(mb);
            }
        }

        private static void RenameIconAndApplySprite(Transform root)
        {
            var iconBg = root.Find("IconBG");
            if (iconBg == null)
                return;
            var helm = iconBg.Find("HelmetImage");
            if (helm == null)
            {
                foreach (Transform c in iconBg)
                {
                    if (c.GetComponent<Image>() != null)
                    {
                        helm = c;
                        break;
                    }
                }
            }

            if (helm == null)
                return;

            helm.name = "ChatIcon";

            var img = helm.GetComponent<Image>();
            if (img == null)
                return;

            if (IconSpriteOverride != null)
            {
                img.sprite = IconSpriteOverride;
                img.preserveAspect = true;
            }

            var bgRt = iconBg as RectTransform;
            var iconRt = helm as RectTransform;
            if (bgRt != null && iconRt != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(bgRt);
                float side = Mathf.Min(bgRt.rect.width, bgRt.rect.height) * IconSlotFillFraction;
                if (side > 1f)
                {
                    iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRt.pivot = new Vector2(0.5f, 0.5f);
                    iconRt.anchoredPosition = Vector2.zero;
                    iconRt.sizeDelta = new Vector2(side, side);
                }
            }
        }

        private static void ApplyKeyLabel(Transform root, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;

            var hint = root.Find("HotkeyHint");
            if (hint == null)
                return;

            var key = hint.Find("Key");
            var tmp = key != null
                ? key.GetComponent<TMP_Text>()
                : hint.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null)
                return;

            tmp.text = label;

            float s = KeyHintFontScale;
            if (s > 0f && Mathf.Abs(s - 1f) > 0.001f)
            {
                if (tmp.enableAutoSizing)
                {
                    tmp.fontSizeMin *= s;
                    tmp.fontSizeMax *= s;
                }
                else
                    tmp.fontSize *= s;
            }
        }

        private static void SetKeyLabelTextOnly(Transform root, string label)
        {
            if (string.IsNullOrEmpty(label))
                return;

            var hint = root.Find("HotkeyHint");
            if (hint == null)
                return;

            var key = hint.Find("Key");
            var tmp = key != null
                ? key.GetComponent<TMP_Text>()
                : hint.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null)
                return;

            tmp.text = label;
        }

        private static void RewireButton(Transform root)
        {
            var hint = root.Find("HotkeyHint");
            var btn = hint != null
                ? hint.GetComponent<Button>()
                : root.GetComponentInChildren<Button>(true);
            if (btn == null)
                return;

            var click = (UnityEventBase)btn.onClick;
            for (var i = click.GetPersistentEventCount() - 1; i >= 0; i--)
                click.SetPersistentListenerState(i, UnityEventCallState.Off);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnChatHotkeyClicked);
        }

        private static void OnChatHotkeyClicked()
        {
            ChatUiBootstrap.ToggleChatPanelHotkey();
        }
    }
}
