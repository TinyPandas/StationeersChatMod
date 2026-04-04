using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChatMod
{
    /// <summary>
    /// Wraps ChatInputField and exposes a minimal API for focus and submit.
    /// The field hierarchy is owned by the prefab — use BindExistingHierarchy().
    /// </summary>
    public class ChatInputBar : MonoBehaviour
    {
        private ChatInputField _field;

        /// <summary>Fired when the user commits (Enter).</summary>
        public event Action<string> Submitted;

        public bool IsFocused => _field != null && _field.isFocused;

        public string GetText() => _field != null ? _field.text ?? "" : "";

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
            Submitted?.Invoke(value ?? "");
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
            if (_field == null) return;
            _field.text = "";
            _field.caretPosition = 0;
            _field.stringPosition = 0;
        }
    }
}
