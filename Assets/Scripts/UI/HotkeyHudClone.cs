using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChatMod
{
    /// <summary>
    /// Clones the game's helmet/light hotkey row next to the stock HUD and wires it to the mod chat panel.
    /// Driven from <see cref="ChatPanel"/> while in gameplay.
    /// </summary>
    public static class HotkeyHudClone
    {
        private const string VanillaHelmetRowName = "HelmetHotkey";
        private const string VanillaLightRowName = "LightHotkey";
        private const string CloneRootName = "ChatHotkey";

        public static Sprite? IconSpriteOverride;
        public static string KeyLabel = "F7";
        public static float IconSlotFillFraction = 0.62f;
        public static float KeyHintFontScale = 0.82f;

        private static CloneMarker? _installedMarker;

        public static void TryInstall()
        {
            // Fast path: already installed and still alive
            if (_installedMarker != null)
            {
                ApplySpriteToMarker(_installedMarker);
                return;
            }

            var template = FindHotkeyRowTemplate();
            if (template == null)
                return;

            var scene = template.scene;
            if (HasMarkerInScene(scene))
                return;

            var clone = Object.Instantiate(template, template.transform.parent, false);
            clone.name = CloneRootName;
            clone.SetActive(true);
            clone.transform.SetAsLastSibling();

            StripGameMonoBehaviours(clone);
            RenameIconAndApplySprite(clone.transform);
            ApplyKeyLabel(clone.transform, KeyLabel);
            RewireButton(clone.transform);

            _installedMarker = clone.AddComponent<CloneMarker>();
            AttachUnreadBadge(clone.transform, _installedMarker);
        }

        private static void ApplySpriteToMarker(CloneMarker marker)
        {
            if (IconSpriteOverride == null || marker == null) return;
            var icon = marker.transform.Find("IconBG/ChatIcon");
            if (icon == null) return;
            var img = icon.GetComponent<Image>();
            if (img != null && img.sprite != IconSpriteOverride)
            {
                img.sprite = IconSpriteOverride;
                img.preserveAspect = true;
            }
        }

        private static void ApplySpriteToExistingClones()
        {
            if (IconSpriteOverride == null) return;
            foreach (var m in Object.FindObjectsOfType<CloneMarker>(true))
            {
                if (m == null) continue;
                var icon = m.transform.Find("IconBG/ChatIcon");
                if (icon == null) continue;
                var img = icon.GetComponent<Image>();
                if (img != null && img.sprite != IconSpriteOverride)
                {
                    img.sprite = IconSpriteOverride;
                    img.preserveAspect = true;
                }
            }
        }

        public static void SetUnreadBadgeCount(int count)
        {
            if (_installedMarker != null)
                _installedMarker.SetUnreadCount(count);
        }

        public static void RefreshKeyLabelOnClones()
        {
            if (_installedMarker != null)
                SetKeyLabelTextOnly(_installedMarker.transform, KeyLabel);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void AttachUnreadBadge(Transform cloneRoot, CloneMarker marker)
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
                if (key != null) fontSrc = key.GetComponent<TMP_Text>();
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

        private static GameObject? FindHotkeyRowTemplate() =>
            FindByExactName(VanillaHelmetRowName) ?? FindByExactName(VanillaLightRowName);

        private static GameObject? FindByExactName(string objectName)
        {
            var found = GameObject.Find(objectName);
            if (found != null) return found;

            for (var si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var t = FindChildByName(root.transform, objectName);
                    if (t != null) return t.gameObject;
                }
            }

            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t.name != objectName) continue;
                var go = t.gameObject;
                if (go.scene.IsValid() && go.scene.isLoaded) return go;
            }

            return null;
        }

        private static Transform? FindChildByName(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (var i = 0; i < parent.childCount; i++)
            {
                var result = FindChildByName(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        private static bool HasMarkerInScene(Scene scene)
        {
            var markers = Object.FindObjectsOfType<CloneMarker>(true);
            return markers.Any(m => m != null && m.gameObject.scene == scene);
        }

        private static void StripGameMonoBehaviours(GameObject root)
        {
            var list = new List<MonoBehaviour>(root.GetComponentsInChildren<MonoBehaviour>(true));
            foreach (var mb in list)
            {
                if (mb == null) continue;
                var an = mb.GetType().Assembly.GetName().Name ?? string.Empty;
                if (an is "Assembly-CSharp" or "Assembly-CSharp-firstpass")
                    Object.Destroy(mb);
            }
        }

        private static void RenameIconAndApplySprite(Transform root)
        {
            var iconBg = root.Find("IconBG");
            if (iconBg == null) return;

            Transform? helm = iconBg.Find("HelmetImage");
            if (helm == null)
            {
                foreach (Transform c in iconBg)
                {
                    if (c.GetComponent<Image>() != null) { helm = c; break; }
                }
            }

            if (helm == null) return;
            helm.name = "ChatIcon";

            var img = helm.GetComponent<Image>();
            if (img == null) return;

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
            if (string.IsNullOrEmpty(label)) return;
            var hint = root.Find("HotkeyHint");
            if (hint == null) return;
            var key = hint.Find("Key");
            var tmp = key != null ? key.GetComponent<TMP_Text>() : hint.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null) return;
            tmp.text = label;
            float s = KeyHintFontScale;
            if (s > 0f && Mathf.Abs(s - 1f) > 0.001f)
            {
                if (tmp.enableAutoSizing) { tmp.fontSizeMin *= s; tmp.fontSizeMax *= s; }
                else tmp.fontSize *= s;
            }
        }

        private static void SetKeyLabelTextOnly(Transform root, string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            var hint = root.Find("HotkeyHint");
            if (hint == null) return;
            var key = hint.Find("Key");
            var tmp = key != null ? key.GetComponent<TMP_Text>() : hint.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = label;
        }

        private static void RewireButton(Transform root)
        {
            // The vanilla row may not use a Unity Button — it uses game scripts for click handling.
            // Those get stripped, so we add our own Button to the clone root.
            var btn = root.GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                var click = (UnityEventBase)btn.onClick;
                for (var i = click.GetPersistentEventCount() - 1; i >= 0; i--)
                    click.SetPersistentListenerState(i, UnityEventCallState.Off);
                btn.onClick.RemoveAllListeners();
            }
            else
            {
                // No existing Button — add one to the root so the entire row is clickable.
                btn = root.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.AddListener(OnCloneClicked);
        }

        private static void OnCloneClicked()
        {
            ChatPanel.Instance?.Toggle();
        }

        // ── CloneMarker ───────────────────────────────────────────────────────

        internal sealed class CloneMarker : MonoBehaviour
        {
            internal GameObject? BadgeRoot;
            internal TextMeshProUGUI? BadgeCountText;

            public void SetUnreadCount(int count)
            {
                count = Mathf.Max(0, count);
                if (BadgeRoot == null) return;
                BadgeRoot.SetActive(count > 0);
                if (BadgeCountText != null)
                    BadgeCountText.text = count > 9 ? "9+" : count.ToString();
            }
        }
    }
}
