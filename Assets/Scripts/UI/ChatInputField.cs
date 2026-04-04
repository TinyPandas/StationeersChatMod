using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChatMod
{
    /// <summary>
    /// Thin <see cref="TMP_InputField"/> subclass so we can refresh the placeholder after blur when TMP/EventSystem
    /// leave it wrong. Background tint and alpha come from the prefab <see cref="UnityEngine.UI.Graphic"/> — we do not
    /// override <see cref="UnityEngine.UI.Selectable.OnSelect"/> / <c>OnDeselect</c> (that was forcing 14% / 6% alpha
    /// on <c>targetGraphic</c> and made the field look like it kept changing transparency).
    /// </summary>
    public class ChatInputField : TMP_InputField
    {
        private bool _blockNextActivation;

        /// <summary>
        /// Suppresses TMP's post-submit re-activation for one cycle.
        /// TMP sets <c>m_ShouldActivateNextUpdate</c> in both <c>OnSubmit</c> and <c>ActivateInputField</c>,
        /// then acts on it in <c>LateUpdate</c> via <c>ActivateInputFieldInternal</c> — bypassing <c>OnSelect</c>.
        /// We intercept <c>OnSubmit</c> to prevent the flag from being set in the first place.
        /// </summary>
        public void BlockNextActivation() => _blockNextActivation = true;

        public override void OnSubmit(BaseEventData eventData)
        {
            if (_blockNextActivation)
            {
                // TMP's OnSubmit only sets m_ShouldActivateNextUpdate when !isFocused,
                // then calls SendOnSubmit again. Since OnUpdateSelected already fired onSubmit,
                // we drop this entirely to prevent the LateUpdate re-activation.
                _blockNextActivation = false;
                return;
            }
            base.OnSubmit(eventData);
        }

        /// <summary>
        /// Call after focus is lost while text is empty when the placeholder stays invisible (TMP / EventSystem quirks).
        /// </summary>
        public void RefreshPlaceholderWhenEmpty()
        {
            if (m_Placeholder == null || !string.IsNullOrWhiteSpace(text))
                return;
            m_Placeholder.enabled = true;
            if (m_Placeholder is TextMeshProUGUI tmp)
            {
                tmp.alpha = tmp.color.a;
                tmp.ForceMeshUpdate(true);
            }
            else
                m_Placeholder.CrossFadeAlpha(1f, 0f, true);
        }
    }
}
