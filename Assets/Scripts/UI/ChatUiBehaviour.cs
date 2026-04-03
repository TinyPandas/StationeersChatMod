using System;
using System.Collections;
using Assets.Scripts.Objects.Entities;
using ChatMod.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChatMod
{
    /// <summary>
    /// Coordinates panel lifecycle, input, drag, scene visibility, and HUD hotkey.
    /// Message rendering is delegated to <see cref="ChatMessageListView"/>.
    /// All references must be assigned on the prefab.
    /// </summary>
    public sealed class ChatUiBehaviour : MonoBehaviour
    {
        public static ChatUiBehaviour? Instance { get; private set; }

        [Header("Prefab references (required)")]
        [SerializeField] private RectTransform _chatRoot = null!;
        [SerializeField] private ScrollRect _scrollRectOnChatRoot = null!;
        [SerializeField] private RectTransform _inputBarRectRef = null!;
        [SerializeField] private ChatPanelDragHandle _dragHandleRef = null!;
        [SerializeField] private ChatMessageListView _messageListView = null!;

        [Header("Launcher (optional)")]
        [SerializeField] private GameObject? _chatLauncher;
        [SerializeField] private string[] _hideChatLauncherInScenes = { "Base" };

        [Header("Vanilla HUD chat row")]
        [SerializeField] private Sprite? _vanillaHotkeyRowIcon;
        [SerializeField] [Range(0.35f, 1f)] private float _vanillaHotkeyIconSlotFill = 0.62f;
        [SerializeField] [Range(0.5f, 1.25f)] private float _vanillaHotkeyKeyHintFontScale = 0.82f;

        private RectTransform _rootRect = null!;
        private ScrollRect _scrollRect = null!;
        private RectTransform _inputBarRect = null!;
        private ChatPanelDragHandle? _dragHandle;
        private ChatInputBar? _inputBar;

        private bool _wasInGameplay;
        private bool _wasInputFocused;
        private int _ignoreOpenFromGameInputFramesRemaining;
        private bool _referencesBound;
        private float _nextVanillaHotkeyTryUnscaled;
        private int _unreadWhileChatClosed;
        private KeyCode? _cachedToggleChatPanelKeyForHud;
        private Coroutine? _focusInputCoroutine;

        // Metrics for change detection — kept here so ChatUiBehaviour controls when to refresh.
        private int _lastRenderedCount = -1;
        private long _lastRenderedLastTicks = -1;
        private long _lastRenderedFirstSeq = -1;

        public bool IsInputActive => _inputBar != null && _inputBar.IsFocused;

        private void Awake()
        {
            Instance = this;

            if (!TryResolveReferences())
            {
                ChatModLog.Error("[ChatUiBehaviour] Failed to bind scene/prefab references.");
                if (_chatRoot != null)
                    _chatRoot.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            ResolveChatLauncherIfUnassigned();
            EnsureCanvasRootScale();
            EnsureLauncherDrawsOnTop();
            ApplySavedWindowPosition();
        }

        /// <summary>Screen-space overlay canvas must not use zero localScale (entire UI becomes invisible).</summary>
        private void EnsureCanvasRootScale()
        {
            var rt = transform as RectTransform;
            if (rt == null)
                return;
            if (rt.localScale.x == 0f || rt.localScale.y == 0f || rt.localScale.z == 0f)
                rt.localScale = Vector3.one;
        }

        private void EnsureLauncherDrawsOnTop()
        {
            if (_chatLauncher == null)
                return;
            _chatLauncher.transform.SetAsLastSibling();
        }

        /// <summary>If <see cref="_chatLauncher"/> was not wired in the prefab, use the direct child named ChatLauncher.</summary>
        private void ResolveChatLauncherIfUnassigned()
        {
            if (_chatLauncher != null)
                return;
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c.name != "ChatLauncher")
                    continue;
                _chatLauncher = c.gameObject;
                return;
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ChatHistoryStore.MessageAdded += OnUnreadChatMessageWhileMaybeClosed;
            ApplyChatLauncherForActiveScene();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ChatHistoryStore.MessageAdded -= OnUnreadChatMessageWhileMaybeClosed;
        }

        private void OnUnreadChatMessageWhileMaybeClosed()
        {
            if (!IsInGameplay() || _chatRoot == null || _chatRoot.gameObject.activeSelf)
                return;

            _unreadWhileChatClosed++;
            ChatHotkeyVanillaClone.SetUnreadBadgeCount(_unreadWhileChatClosed);
        }

        private void ClearUnreadHotkeyBadge()
        {
            _unreadWhileChatClosed = 0;
            ChatHotkeyVanillaClone.SetUnreadBadgeCount(0);
        }

        private void SyncHotkeyHudKeyLabelFromConfig(bool force)
        {
            if (ModConfig.ToggleChatPanelKey == null)
                return;

            KeyCode k = ModConfig.ToggleChatPanelKey.Value;
            if (!force && _cachedToggleChatPanelKeyForHud == k)
                return;

            _cachedToggleChatPanelKeyForHud = k;
            ChatHotkeyVanillaClone.KeyLabel = k == KeyCode.None ? "-" : k.ToString();
            ChatHotkeyVanillaClone.RefreshKeyLabelOnClones();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyChatLauncherForActiveScene();
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ApplyChatLauncherForActiveScene();
        }

        /// <summary>Shows <see cref="_chatLauncher"/> in world / in-session; hides in listed menu scenes when not in gameplay.</summary>
        private void ApplyChatLauncherForActiveScene()
        {
            if (_chatLauncher == null)
                return;
            string active = SceneManager.GetActiveScene().name;
            bool menuSceneHides = ShouldHideChatLauncherInScene(active);
            bool inGameplay = IsInGameplay();
            bool hide = menuSceneHides && !inGameplay;
            bool wantActive = !hide;
            if (_chatLauncher.activeSelf != wantActive)
                _chatLauncher.SetActive(wantActive);
        }

        private bool ShouldHideChatLauncherInScene(string sceneName)
        {
            if (_hideChatLauncherInScenes == null || _hideChatLauncherInScenes.Length == 0)
                return false;
            foreach (string entry in _hideChatLauncherInScenes)
            {
                if (string.IsNullOrEmpty(entry))
                    continue;
                if (string.Equals(sceneName, entry.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private bool TryResolveReferences()
        {
            if (_chatRoot == null || _scrollRectOnChatRoot == null ||
                _inputBarRectRef == null || _dragHandleRef == null || _messageListView == null)
            {
                ChatModLog.Error(
                    "[ChatUiBehaviour] Assign all prefab references: Chat Root, Scroll Rect, Input Bar Rect Ref, Drag Handle Ref, Message List View.");
                return false;
            }

            _rootRect = _chatRoot;
            _scrollRect = _scrollRectOnChatRoot;
            _inputBarRect = _inputBarRectRef;
            _dragHandle = _dragHandleRef;

            if (_dragHandle.RootRect == null)
            {
                ChatModLog.Error(
                    "[ChatUiBehaviour] ChatPanelDragHandle must have Root Rect assigned to ChatRoot in the prefab.");
                return false;
            }

            _dragHandle.OnDragEnded += SaveWindowPosition;

            _inputBar = _inputBarRect.GetComponent<ChatInputBar>() ??
                        _inputBarRect.GetComponentInParent<ChatInputBar>();
            if (_inputBar == null)
            {
                ChatModLog.Error(
                    "[ChatUiBehaviour] Assign Input Bar Rect Ref to the InputBar object (with ChatInputBar), not the ChatInputField child.");
                return false;
            }

            _inputBar.BindExistingHierarchy();
            _inputBar.OnSubmit += OnChatInputSubmit;

            ChatHotkeyVanillaClone.IconSpriteOverride = _vanillaHotkeyRowIcon;
            ChatHotkeyVanillaClone.IconSlotFillFraction = _vanillaHotkeyIconSlotFill;
            ChatHotkeyVanillaClone.KeyHintFontScale = _vanillaHotkeyKeyHintFontScale;
            SyncHotkeyHudKeyLabelFromConfig(force: true);

            _referencesBound = true;
            return true;
        }

        private void ApplySavedWindowPosition()
        {
            if (ModConfig.ChatWindowPositionX == null || ModConfig.ChatWindowPositionY == null)
                return;
            _rootRect.anchoredPosition = new Vector2(
                ModConfig.ChatWindowPositionX.Value,
                ModConfig.ChatWindowPositionY.Value);
            ClampWindowToReferenceResolution();
        }

        private void ClampWindowToReferenceResolution()
        {
            const float refW = 1920f;
            const float refH = 1080f;
            Canvas.ForceUpdateCanvases();
            var r = _rootRect.rect;
            float w = Mathf.Max(1f, r.width);
            float h = Mathf.Max(1f, r.height);
            var pos = _rootRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, 0f, refW - w);
            pos.y = Mathf.Clamp(pos.y, 0f, refH - h);
            _rootRect.anchoredPosition = pos;
        }

        private void OnDestroy()
        {
            SaveWindowPosition();
            if (_dragHandle != null)
                _dragHandle.OnDragEnded -= SaveWindowPosition;

            if (_inputBar != null)
                _inputBar.OnSubmit -= OnChatInputSubmit;

            if (Instance == this)
                Instance = null;
        }

        private static bool IsInGameplay() => Human.LocalHuman != null;

        private void SaveWindowPosition()
        {
            if (ModConfig.ChatWindowPositionX == null || ModConfig.ChatWindowPositionY == null)
                return;
            var pos = _rootRect.anchoredPosition;
            ModConfig.ChatWindowPositionX.Value = pos.x;
            ModConfig.ChatWindowPositionY.Value = pos.y;
            ModConfig.Save();
        }

        private void Update()
        {
            if (!_referencesBound)
                return;

            ApplyChatLauncherForActiveScene();

            bool inGameplay = IsInGameplay();
            if (!inGameplay)
            {
                if (_wasInGameplay)
                {
                    ChatHistoryStore.Clear();
                    PlayerNameColorCache.Clear();
                    ClearUnreadHotkeyBadge();
                    _messageListView.Clear();
                }

                _rootRect.gameObject.SetActive(false);
                _wasInGameplay = false;
                return;
            }

            _wasInGameplay = true;

            SyncHotkeyHudKeyLabelFromConfig(force: false);

            if (Time.unscaledTime >= _nextVanillaHotkeyTryUnscaled)
            {
                _nextVanillaHotkeyTryUnscaled = Time.unscaledTime + 0.5f;
                ChatHotkeyVanillaClone.TryInstall();
                ChatHotkeyVanillaClone.SetUnreadBadgeCount(_unreadWhileChatClosed);
            }

            var toggleKey = ModConfig.ToggleChatPanelKey.Value;
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey) &&
                !(_inputBar != null && _inputBar.IsFocused))
                ToggleChatPanelHotkey();

            HandleAltReleaseFocus();
            SyncKeyManagerTypingState();
            RefreshMessages();

            if (_messageListView.ScrollToBottomPending)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 0f;
                _messageListView.AcknowledgeScrollToBottom();
            }

            if (_ignoreOpenFromGameInputFramesRemaining > 0)
                _ignoreOpenFromGameInputFramesRemaining--;
        }

        private void OnChatInputSubmit(string value)
        {
            if (_inputBar == null)
                return;

            // Opening the panel starts a delayed RequestFocus; cancel it so submit does not end up "keeping" focus.
            CancelPendingInputFocusCoroutine();

            _inputBar.Clear();

            if (string.IsNullOrWhiteSpace(value))
            {
                _ignoreOpenFromGameInputFramesRemaining = 12;
                _inputBar.ReleaseFocus();
                return;
            }

            ChatMessageSender.Send(value);
            _ignoreOpenFromGameInputFramesRemaining = 12;
            _inputBar.ReleaseFocus();
        }

        private void SyncKeyManagerTypingState()
        {
            bool focused = _inputBar != null && _inputBar.IsFocused;
            if (focused && !_wasInputFocused)
                KeyManager.SetInputState("ChatMod_Input", KeyInputState.Typing);
            if (!focused && _wasInputFocused)
                KeyManager.RemoveInputState("ChatMod_Input");
            _wasInputFocused = focused;
        }

        private void HandleAltReleaseFocus()
        {
            if (_inputBar == null || !_inputBar.IsFocused)
                return;
            if (!Input.GetKeyDown(KeyCode.LeftAlt))
                return;
            _inputBar.ReleaseFocus();
        }

        private void RefreshMessages(bool force = false)
        {
            if (!_referencesBound || !_chatRoot.gameObject.activeSelf)
                return;

            ChatHistoryStore.GetTailMetrics(out int count, out long lastTicks, out long firstSeq);

            bool metricsChanged = force ||
                                  _lastRenderedCount != count ||
                                  _lastRenderedLastTicks != lastTicks ||
                                  _lastRenderedFirstSeq != firstSeq;

            if (!metricsChanged)
                return;

            _lastRenderedCount = count;
            _lastRenderedLastTicks = lastTicks;
            _lastRenderedFirstSeq = firstSeq;

            _messageListView.Refresh(ChatHistoryStore.GetSnapshot(), force);
        }

        private void InvalidateMessageListRefresh()
        {
            _lastRenderedCount = -1;
            _lastRenderedLastTicks = -1;
            _lastRenderedFirstSeq = -1;
        }

        /// <summary>Show chat and focus input (vanilla chat intercept, programmatic open).</summary>
        public void OpenFromGameInput()
        {
            if (!_referencesBound)
                return;

            if (_ignoreOpenFromGameInputFramesRemaining > 0)
                return;

            if (!_rootRect.gameObject.activeSelf)
            {
                _rootRect.gameObject.SetActive(true);
                ClearUnreadHotkeyBadge();
                InvalidateMessageListRefresh();
            }

            if (_inputBar != null && _inputBar.IsFocused)
            {
                _inputBar.RequestFocus();
                return;
            }

            StartFocusInputAfterFrames(3);
        }

        /// <summary>Config key / cloned HUD row: toggle <see cref="_chatRoot"/> visibility.</summary>
        public void ToggleChatPanelHotkey()
        {
            if (!_referencesBound)
                return;

            if (_ignoreOpenFromGameInputFramesRemaining > 0)
                return;

            if (_rootRect.gameObject.activeSelf)
            {
                CancelPendingInputFocusCoroutine();
                _inputBar?.ReleaseFocus();
                _rootRect.gameObject.SetActive(false);
                return;
            }

            _rootRect.gameObject.SetActive(true);
            ClearUnreadHotkeyBadge();
            InvalidateMessageListRefresh();
            StartFocusInputAfterFrames(3);
        }

        private void CancelPendingInputFocusCoroutine()
        {
            if (_focusInputCoroutine == null)
                return;
            StopCoroutine(_focusInputCoroutine);
            _focusInputCoroutine = null;
        }

        private void StartFocusInputAfterFrames(int frames)
        {
            CancelPendingInputFocusCoroutine();
            _focusInputCoroutine = StartCoroutine(FocusInputFieldAfterFrames(frames));
        }

        private IEnumerator FocusInputFieldAfterFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;

            _focusInputCoroutine = null;

            if (_inputBar == null || _ignoreOpenFromGameInputFramesRemaining > 0)
                yield break;

            _inputBar.RequestFocus();
        }

    }
}
