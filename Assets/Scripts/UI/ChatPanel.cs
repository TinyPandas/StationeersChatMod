using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Objects.Entities;
using StationeersMods.Interface;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ChatMod
{
    /// <summary>
    /// Coordinates panel lifecycle, input, drag, scene visibility, and HUD hotkey.
    /// Also owns prefab registration (previously ChatUiBootstrap) and row prefab resolution.
    /// Message rendering is delegated to <see cref="ChatMessageListView"/>.
    /// All references must be assigned on the prefab.
    /// </summary>
    public sealed class ChatPanel : MonoBehaviour
    {
        // ── Prefab constants ──────────────────────────────────────────────────
        public const string ModContentPrefabName = "ChatUiRoot";
        public const string MessageRowPrefabName = "MessageRow";
        public const string MessageRowHostPrefabName = "MessageRowHost";

        // ── Static state (previously ChatUiBootstrap) ─────────────────────────
        public static ChatPanel? Instance { get; private set; }
        private static IEnumerable<GameObject>? _registeredPrefabs;

        public static void Initialize(ContentHandler contentHandler)
        {
            if (contentHandler.prefabs != null)
                _registeredPrefabs = contentHandler.prefabs;

            if (Instance != null)
                return;

            Instance = Object.FindObjectOfType<ChatPanel>();
            if (Instance != null)
                return;

            var prefab = _registeredPrefabs?.FirstOrDefault(p => p != null && p.name == ModContentPrefabName);
            if (prefab == null)
            {
                ChatModLog.Error($"[ChatMod] No chat UI prefab. Register root named '{ModContentPrefabName}' in mod content.");
                return;
            }

            var root = Object.Instantiate(prefab);
            root.name = prefab.name;
            Object.DontDestroyOnLoad(root);

            Instance = root.GetComponent<ChatPanel>() ?? root.GetComponentInChildren<ChatPanel>(true);
            if (Instance == null)
            {
                ChatModLog.Error("[ChatMod] Chat UI prefab must include ChatPanel on the root or a child.");
                Object.Destroy(root);
            }
        }

        public static GameObject? TryGetNamedPrefab(string assetName)
        {
            if (_registeredPrefabs == null) return null;
            return _registeredPrefabs.FirstOrDefault(x => x != null && x.name == assetName);
        }

        // ── Serialized references ─────────────────────────────────────────────
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

        [Header("Audio")]
        [SerializeField] private AudioClip? _notificationClip;

        // ── Runtime state ─────────────────────────────────────────────────────
        private RectTransform _rootRect = null!;
        private ScrollRect _scrollRect = null!;
        private RectTransform _inputBarRect = null!;
        private ChatPanelDragHandle? _dragHandle;
        private ChatInputBar? _inputBar;

        private bool _wasInGameplay;
        private bool _wasInputFocused;
        private int _ignoreOpenFramesRemaining;
        private bool _referencesBound;
        private float _nextHotkeyTryUnscaled;
        private int _unreadWhileClosed;
        private KeyCode? _cachedToggleKey;
        private Coroutine? _focusCoroutine;

        // ── Auto-fade state ──────────────────────────────────────────────────
        private CanvasGroup _canvasGroup = null!;

        private enum FadeState { Idle, Counting, Fading }
        private FadeState _fadeState = FadeState.Idle;
        private bool _closedByFade;

        private float _inactivityTimer;
        private const float FadeDuration = 0.5f;
        private bool _wasFocusedForFade;

        private int _lastRenderedCount = -1;
        private long _lastRenderedLastTicks = -1;
        private long _lastRenderedFirstSeq = -1;

        public bool IsInputActive => _inputBar != null && _inputBar.IsFocused;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;

            if (!TryResolveReferences())
            {
                ChatModLog.Error("[ChatPanel] Failed to bind scene/prefab references.");
                if (_chatRoot != null) _chatRoot.gameObject.SetActive(false);
                enabled = false;
                return;
            }

            ResolveChatLauncherIfUnassigned();
            EnsureCanvasRootScale();
            EnsureLauncherDrawsOnTop();
            ApplySavedWindowPosition();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            ChatHistoryStore.MessageAdded += OnMessageAddedWhileClosed;
            ApplyChatLauncherForActiveScene();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ChatHistoryStore.MessageAdded -= OnMessageAddedWhileClosed;
        }

        private void OnDestroy()
        {
            SaveWindowPosition();
            if (_dragHandle != null) _dragHandle.OnDragEnded -= SaveWindowPosition;
            if (_inputBar != null) _inputBar.Submitted -= OnChatInputSubmit;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!_referencesBound) return;

            ApplyChatLauncherForActiveScene();

            bool inGameplay = IsInGameplay();
            if (!inGameplay)
            {
                if (_wasInGameplay)
                {
                    ChatHistoryStore.Clear();
                    PlayerNameColorCache.Clear();
                    ClearUnreadBadge();
                    _messageListView.Clear();
                }

                CloseWithoutFade();
                _wasInGameplay = false;
                return;
            }

            _wasInGameplay = true;

            SyncHotkeyKeyLabel(force: false);

            if (Time.unscaledTime >= _nextHotkeyTryUnscaled)
            {
                _nextHotkeyTryUnscaled = Time.unscaledTime + 0.5f;
                HotkeyHudClone.TryInstall();
                HotkeyHudClone.SetUnreadBadgeCount(_unreadWhileClosed);
            }

            var toggleKey = ModConfig.ToggleChatPanelKey.Value;
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey) &&
                !(_inputBar != null && _inputBar.IsFocused))
                Toggle();

            HandleAltReleaseFocus();
            SyncKeyManagerTypingState();

            RefreshMessages();

            if (_messageListView.ScrollToBottomPending)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 0f;
                _messageListView.AcknowledgeScrollToBottom();
            }

            if (_ignoreOpenFramesRemaining > 0)
                _ignoreOpenFramesRemaining--;

            UpdateAutoFade();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Show chat and focus input (vanilla chat intercept, programmatic open).</summary>
        public void Open()
        {
            if (!_referencesBound || _ignoreOpenFramesRemaining > 0) return;

            bool wasAlreadyActive = _rootRect.gameObject.activeSelf;

            if (!wasAlreadyActive)
            {
                _rootRect.gameObject.SetActive(true);
                ClearUnreadBadge();
                InvalidateMessageListRefresh();
            }

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            ResetInactivityTimer();

            if (_inputBar != null && _inputBar.IsFocused)
            {
                _inputBar.RequestFocus();
                return;
            }

            // If panel was already visible, focus immediately — no layout settle needed.
            // Only delay when we just activated the GameObject.
            if (wasAlreadyActive)
                _inputBar?.RequestFocus();
            else
                StartFocusAfterFrames(1);
        }

        /// <summary>Toggle panel visibility.</summary>
        public void Toggle()
        {
            if (!_referencesBound || _ignoreOpenFramesRemaining > 0) return;

            if (_rootRect.gameObject.activeSelf)
            {
                CancelFocusCoroutine();
                _inputBar?.ReleaseFocus();
                CloseWithoutFade();
                return;
            }

            _rootRect.gameObject.SetActive(true);
            ClearUnreadBadge();
            InvalidateMessageListRefresh();

            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            ResetInactivityTimer();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void OnChatInputSubmit(string value)
        {
            if (_inputBar == null) return;

            CancelFocusCoroutine();
            _inputBar.Clear();

            if (string.IsNullOrWhiteSpace(value))
            {
                _ignoreOpenFramesRemaining = 12;
                _inputBar.ReleaseFocus();
                return;
            }

            ChatMessageSender.Send(value);
            _ignoreOpenFramesRemaining = 12;
            _inputBar.ReleaseFocus();
        }

        private void OnMessageAddedWhileClosed()
        {
            if (!IsInGameplay() || _chatRoot == null) return;

            // Panel is open — reset timer or cancel fade to keep it visible
            if (_chatRoot.gameObject.activeSelf)
            {
                if (_fadeState == FadeState.Fading)
                    CancelFadeAndReset();
                else if (_fadeState == FadeState.Counting)
                    ResetInactivityTimer();
                return;
            }

            // Panel was auto-closed by fade — reopen it
            if (_closedByFade)
            {
                _closedByFade = false;
                _rootRect.gameObject.SetActive(true);
                ClearUnreadBadge();
                InvalidateMessageListRefresh();
                ResetInactivityTimer();
                return;
            }

            // Panel was manually closed — just increment badge
            _unreadWhileClosed++;
            HotkeyHudClone.SetUnreadBadgeCount(_unreadWhileClosed);
        }

        private void ClearUnreadBadge()
        {
            _unreadWhileClosed = 0;
            HotkeyHudClone.SetUnreadBadgeCount(0);
        }

        private void RefreshMessages(bool force = false)
        {
            if (!_referencesBound || !_chatRoot.gameObject.activeSelf) return;

            ChatHistoryStore.GetTailMetrics(out int count, out long lastTicks, out long firstSeq);

            bool changed = force ||
                           _lastRenderedCount != count ||
                           _lastRenderedLastTicks != lastTicks ||
                           _lastRenderedFirstSeq != firstSeq;

            if (!changed) return;

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
            if (_inputBar == null || !_inputBar.IsFocused) return;
            if (!Input.GetKeyDown(KeyCode.LeftAlt)) return;
            _inputBar.ReleaseFocus();
        }

        private void SyncHotkeyKeyLabel(bool force)
        {
            if (ModConfig.ToggleChatPanelKey == null) return;
            KeyCode k = ModConfig.ToggleChatPanelKey.Value;
            if (!force && _cachedToggleKey == k) return;
            _cachedToggleKey = k;
            HotkeyHudClone.KeyLabel = k == KeyCode.None ? "-" : k.ToString();
            HotkeyHudClone.RefreshKeyLabelOnClones();
        }

        private void CancelFocusCoroutine()
        {
            if (_focusCoroutine == null) return;
            StopCoroutine(_focusCoroutine);
            _focusCoroutine = null;
        }

        private void StartFocusAfterFrames(int frames)
        {
            CancelFocusCoroutine();
            _focusCoroutine = StartCoroutine(FocusAfterFrames(frames));
        }

        private IEnumerator FocusAfterFrames(int frames)
        {
            for (int i = 0; i < frames; i++) yield return null;
            _focusCoroutine = null;
            if (_inputBar == null || _ignoreOpenFramesRemaining > 0) yield break;
            _inputBar.RequestFocus();
        }

        private void ResetInactivityTimer()
        {
            _inactivityTimer = ModConfig.AutoFadeTimeout.Value;
            _fadeState = FadeState.Counting;
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        private void CancelFadeAndReset()
        {
            if (_fadeState == FadeState.Fading)
            {
                _canvasGroup.alpha = 1f;
                ResetInactivityTimer();
            }
        }

        private void CloseWithoutFade()
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _rootRect.gameObject.SetActive(false);
            _fadeState = FadeState.Idle;
            _closedByFade = false;
        }

        private void UpdateAutoFade()
        {
            bool enabled = ModConfig.EnableAutoFade?.Value ?? true;

            if (!enabled)
            {
                // Transitioning from enabled to disabled while active: cancel and restore
                if (_fadeState == FadeState.Counting || _fadeState == FadeState.Fading)
                {
                    _canvasGroup.alpha = 1f;
                    _canvasGroup.blocksRaycasts = true;
                    _canvasGroup.interactable = true;
                    _fadeState = FadeState.Idle;
                }
                return;
            }

            bool isFocused = _inputBar != null && _inputBar.IsFocused;

            switch (_fadeState)
            {
                case FadeState.Counting:
                    if (isFocused)
                    {
                        // Pause timer while input is focused
                        _wasFocusedForFade = true;
                        return;
                    }

                    if (_wasFocusedForFade)
                    {
                        // Focus just lost — reset timer to current timeout
                        _wasFocusedForFade = false;
                        _inactivityTimer = ModConfig.AutoFadeTimeout.Value;
                        return;
                    }

                    _inactivityTimer -= Time.unscaledDeltaTime;
                    if (_inactivityTimer <= 0f)
                    {
                        _fadeState = FadeState.Fading;
                    }
                    break;

                case FadeState.Fading:
                    float newAlpha = _canvasGroup.alpha - Time.unscaledDeltaTime / FadeDuration;
                    newAlpha = Mathf.Clamp01(newAlpha);
                    _canvasGroup.alpha = newAlpha;
                    _canvasGroup.blocksRaycasts = true;
                    _canvasGroup.interactable = true;

                    if (newAlpha <= 0f)
                    {
                        _canvasGroup.alpha = 1f;
                        _rootRect.gameObject.SetActive(false);
                        _fadeState = FadeState.Idle;
                        _closedByFade = true;
                    }
                    break;
            }
        }

        private bool TryResolveReferences()
        {
            if (_chatRoot == null || _scrollRectOnChatRoot == null ||
                _inputBarRectRef == null || _dragHandleRef == null || _messageListView == null)
            {
                ChatModLog.Error("[ChatPanel] Assign all prefab references: Chat Root, Scroll Rect, Input Bar Rect Ref, Drag Handle Ref, Message List View.");
                return false;
            }

            _rootRect = _chatRoot;
            _scrollRect = _scrollRectOnChatRoot;
            _inputBarRect = _inputBarRectRef;
            _dragHandle = _dragHandleRef;

            if (_dragHandle.RootRect == null)
            {
                ChatModLog.Error("[ChatPanel] ChatPanelDragHandle must have Root Rect assigned to ChatRoot in the prefab.");
                return false;
            }

            _dragHandle.OnDragEnded += SaveWindowPosition;

            _inputBar = _inputBarRect.GetComponent<ChatInputBar>() ??
                        _inputBarRect.GetComponentInParent<ChatInputBar>();
            if (_inputBar == null)
            {
                ChatModLog.Error("[ChatPanel] Assign Input Bar Rect Ref to the InputBar object (with ChatInputBar), not the ChatInputField child.");
                return false;
            }

            _inputBar.BindExistingHierarchy();
            _inputBar.Submitted += OnChatInputSubmit;

            _canvasGroup = _chatRoot.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                ChatModLog.Error("[ChatPanel] ChatRoot must have a CanvasGroup component for auto-fade support.");
                return false;
            }

            HotkeyHudClone.IconSpriteOverride = _vanillaHotkeyRowIcon;
            HotkeyHudClone.IconSlotFillFraction = _vanillaHotkeyIconSlotFill;
            HotkeyHudClone.KeyHintFontScale = _vanillaHotkeyKeyHintFontScale;
            ChatNotificationSound.Clip = _notificationClip;
            SyncHotkeyKeyLabel(force: true);

            _referencesBound = true;
            return true;
        }

        private void ApplySavedWindowPosition()
        {
            if (ModConfig.ChatWindowPositionX == null || ModConfig.ChatWindowPositionY == null) return;
            _rootRect.anchoredPosition = new Vector2(ModConfig.ChatWindowPositionX.Value, ModConfig.ChatWindowPositionY.Value);
            ClampWindowToScreen();
        }

        private void ClampWindowToScreen()
        {
            const float refW = 1920f, refH = 1080f;
            Canvas.ForceUpdateCanvases();
            var r = _rootRect.rect;
            var pos = _rootRect.anchoredPosition;
            pos.x = Mathf.Clamp(pos.x, 0f, refW - Mathf.Max(1f, r.width));
            pos.y = Mathf.Clamp(pos.y, 0f, refH - Mathf.Max(1f, r.height));
            _rootRect.anchoredPosition = pos;
        }

        private void SaveWindowPosition()
        {
            if (ModConfig.ChatWindowPositionX == null || ModConfig.ChatWindowPositionY == null) return;
            var pos = _rootRect.anchoredPosition;
            ModConfig.ChatWindowPositionX.Value = pos.x;
            ModConfig.ChatWindowPositionY.Value = pos.y;
            ModConfig.Save();
        }

        private void EnsureCanvasRootScale()
        {
            var rt = transform as RectTransform;
            if (rt == null) return;
            if (rt.localScale.x == 0f || rt.localScale.y == 0f || rt.localScale.z == 0f)
                rt.localScale = Vector3.one;
        }

        private void EnsureLauncherDrawsOnTop()
        {
            _chatLauncher?.transform.SetAsLastSibling();
        }

        private void ResolveChatLauncherIfUnassigned()
        {
            if (_chatLauncher != null) return;
            for (int i = 0; i < transform.childCount; i++)
            {
                var c = transform.GetChild(i);
                if (c.name != "ChatLauncher") continue;
                _chatLauncher = c.gameObject;
                return;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyChatLauncherForActiveScene();
        private void OnActiveSceneChanged(Scene previous, Scene next) => ApplyChatLauncherForActiveScene();

        private void ApplyChatLauncherForActiveScene()
        {
            if (_chatLauncher == null) return;
            string active = SceneManager.GetActiveScene().name;
            bool hide = ShouldHideLauncherInScene(active) && !IsInGameplay();
            bool wantActive = !hide;
            if (_chatLauncher.activeSelf != wantActive)
                _chatLauncher.SetActive(wantActive);
        }

        private bool ShouldHideLauncherInScene(string sceneName)
        {
            if (_hideChatLauncherInScenes == null) return false;
            foreach (string entry in _hideChatLauncherInScenes)
            {
                if (!string.IsNullOrEmpty(entry) &&
                    string.Equals(sceneName, entry.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsInGameplay() => Human.LocalHuman != null;
    }
}
