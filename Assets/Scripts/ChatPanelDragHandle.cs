using System;
using UnityEngine;

namespace ChatMod
{
    /// <summary>
    /// Drag bar for a panel: assign <see cref="RootRect"/> to the panel (e.g. ChatRoot) in the prefab. While dragging,
    /// updates <c>RootRect.anchoredPosition</c> in canvas space. Subscribe to <see cref="OnDragEnded"/> for save callbacks.
    /// </summary>
    public class ChatPanelDragHandle : MonoBehaviour
    {
        private RectTransform _dragBarRect;
        public RectTransform _rootRect;
        private bool _dragging;
        private Vector2 _dragOffsetCanvas;
        private bool _initialized;

        /// <summary>
        /// The root panel RectTransform (position and size used for hit-test and anchor reporting).
        /// </summary>
        public RectTransform RootRect { get => _rootRect; set => _rootRect = value; }

        /// <summary>Fired when a drag starts (pointer down on the drag bar).</summary>
        public event Action OnDragStarted;

        /// <summary>Fired when the pointer is released (drag ends).</summary>
        public event Action OnDragEnded;

        private void Awake()
        {
            _dragBarRect = GetComponent<RectTransform>();
            _initialized = _dragBarRect != null;
        }

        private static bool ScreenToCanvasPoint(RectTransform canvasRect, Vector2 screenPoint, out Vector2 canvasPoint)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out canvasPoint);
        }

        private void Update()
        {
            if (!_initialized || _rootRect == null)
                return;

            var canvasRect = _rootRect.parent as RectTransform;
            if (canvasRect == null)
                return;

            Vector2 mouseScreen = Input.mousePosition;

            // Hit test in screen space so it works with any resolution and with an invisible (Color.clear) bar
            if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(_dragBarRect, mouseScreen, null))
            {
                if (ScreenToCanvasPoint(canvasRect, mouseScreen, out Vector2 mouseCanvas))
                {
                    _dragging = true;
                    _dragOffsetCanvas = mouseCanvas - _rootRect.anchoredPosition;
                    OnDragStarted?.Invoke();
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (_dragging)
                    OnDragEnded?.Invoke();
                _dragging = false;
            }

            if (_dragging && ScreenToCanvasPoint(canvasRect, mouseScreen, out Vector2 currentCanvasPos))
            {
                Vector2 pos = currentCanvasPos - _dragOffsetCanvas;
                _rootRect.anchoredPosition = pos;
            }
        }
    }
}
