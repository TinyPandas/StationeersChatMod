using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace ChatMod
{
    /// <summary>
    /// Single place for all chat input UI: builds and owns a TMP_InputField so it displays
    /// and behaves correctly. Exposes a minimal API so ChatUiBehaviour only handles focus and submit.
    /// </summary>
    public class ChatInputBar : MonoBehaviour
    {
        private ChatInputField _field;
        private Image _backgroundImage;

        /// <summary>Fired when the user commits (Enter). <paramref name="value"/> may be empty if they pressed Enter with no text.</summary>
        public event Action<string> OnSubmit;

        public bool IsFocused => _field != null && _field.isFocused;

        public string GetText() => _field != null ? _field.text ?? "" : "";

        public void Build(RectTransform barRect)
        {
            var go = new GameObject("ChatInputField");
            go.transform.SetParent(barRect, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(8f, 2f);
            rect.offsetMax = new Vector2(-8f, -2f);

            _backgroundImage = go.AddComponent<Image>();
            _backgroundImage.color = new Color(1f, 1f, 1f, 0.06f);

            _field = go.AddComponent<ChatInputField>();
            _field.targetGraphic = _backgroundImage;
            _field.transition = Selectable.Transition.None;

            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            // Text area (viewport for scrolling/masking)
            var textAreaGo = new GameObject("Text Area");
            textAreaGo.transform.SetParent(go.transform, false);

            var textAreaRect = textAreaGo.AddComponent<RectTransform>();
            textAreaRect.anchorMin = Vector2.zero;
            textAreaRect.anchorMax = Vector2.one;
            textAreaRect.offsetMin = new Vector2(8f, 2f);
            textAreaRect.offsetMax = new Vector2(-8f, -2f);

            textAreaGo.AddComponent<RectMask2D>();

            _field.textViewport = textAreaRect;

            // Main text (what user types)
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textAreaGo.transform, false);

            var textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var tmpText = textGo.AddComponent<TextMeshProUGUI>();
            tmpText.font = font;
            tmpText.fontSize = 18;
            tmpText.color = Color.white;
            tmpText.raycastTarget = false;

            _field.textComponent = tmpText;

            // Placeholder
            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textAreaGo.transform, false);

            var placeholderRect = placeholderGo.AddComponent<RectTransform>();
            placeholderRect.anchorMin = new Vector2(0f, 0f);
            placeholderRect.anchorMax = new Vector2(1f, 1f);
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;

            var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.font = font;
            placeholderText.fontSize = 18;
            placeholderText.color = new Color(1f, 1f, 1f, 0.45f);
            placeholderText.raycastTarget = false;
            placeholderText.text = "Type a message...";

            _field.placeholder = placeholderText;
            _field.contentType = TMP_InputField.ContentType.Standard;
            _field.lineType = TMP_InputField.LineType.SingleLine;
            _field.text = "";

            _field.onSubmit.AddListener(HandleSubmit);
            _field.onEndEdit.AddListener(OnInputEndEdit);
        }

        /// <summary>Wire an authored hierarchy (InputBar already contains <see cref="ChatInputField"/>).</summary>
        public void BindExistingHierarchy()
        {
            if (_field != null)
                return;

            _field = GetComponentInChildren<ChatInputField>(true);
            if (_field == null)
            {
                ChatModLog.Warning("[ChatMod] ChatInputBar: no ChatInputField found under InputBar.");
                return;
            }

            _field.onSubmit.RemoveAllListeners();
            _field.onSubmit.AddListener(HandleSubmit);
            _field.onEndEdit.RemoveListener(OnInputEndEdit);
            _field.onEndEdit.AddListener(OnInputEndEdit);
        }

        /// <summary>Only fired when the user presses Enter, not when they click away (onEndEdit). So we only send on explicit submit.</summary>
        private void HandleSubmit(string value)
        {
            OnSubmit?.Invoke(value ?? "");
        }

        private void OnInputEndEdit(string value)
        {
            if (_field == null || !string.IsNullOrWhiteSpace(value))
                return;
            _field.RefreshPlaceholderWhenEmpty();
        }

        public void RequestFocus()
        {
            if (_field == null) return;
            _field.ActivateInputField();
            _field.Select();
        }

        public void ReleaseFocus()
        {
            if (_field == null) return;
            _field.BlockNextActivation();
            _field.DeactivateInputField();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            _field.RefreshPlaceholderWhenEmpty();
        }

        public void Clear()
        {
            if (_field != null)
                _field.text = "";
        }
    }
}
