using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Objects.Entities;
using ChatMod.UI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChatMod
{
    /// <summary>
    /// Binds to the authored chat hierarchy and drives data only (messages, input, drag save). All references must be assigned on the prefab — no runtime <c>Transform.Find</c>.
    /// </summary>
    public sealed class ChatUiBehaviour : MonoBehaviour
    {
        public static ChatUiBehaviour? Instance { get; private set; }

        [Header("Prefab references (required)")]
        [Tooltip("RectTransform of ChatRoot (the draggable panel root).")]
        [SerializeField] private RectTransform _chatRoot = null!;
        [Tooltip("ScrollRect on ChatRoot — scrolls MessageViewport / MessageContent.")]
        [SerializeField] private ScrollRect _scrollRectOnChatRoot = null!;
        [Tooltip("RectTransform of MessageContent (child of MessageViewport).")]
        [SerializeField] private RectTransform _messageContent = null!;
        [Tooltip("RectTransform of the bar that owns chat input — the GameObject with ChatInputBar (usually named InputBar), not the ChatInputField child.")]
        [SerializeField] private RectTransform _inputBarRectRef = null!;
        [Tooltip("ChatPanelDragHandle on DragBar (Root Rect → ChatRoot in prefab).")]
        [SerializeField] private ChatPanelDragHandle _dragHandleRef = null!;
        [Header("Launcher (optional)")]
        [Tooltip("Lower-corner chat button / badge root. Hidden when the active scene name matches an entry below (e.g. Stationeers Base menu).")]
        [SerializeField] private GameObject? _chatLauncher;
        [Tooltip("Active scene names (no .unity suffix) where the launcher is hidden in the menu — case-insensitive. While Human.LocalHuman is set (in-session), the launcher stays visible even if the active scene name still matches (additive loading).")]
        [SerializeField] private string[] _hideChatLauncherInScenes = { "Base" };

        [Header("Vanilla HUD chat row")]
        [Tooltip("Icon on the cloned row (use the Sprite from Assets/Texture2D/message.png; message.asset is TMP-only).")]
        [SerializeField] private Sprite? _vanillaHotkeyRowIcon;
        [Tooltip("Cloned HUD row: icon size as a fraction of the smaller IconBG side (large sprites).")]
        [SerializeField] [Range(0.35f, 1f)] private float _vanillaHotkeyIconSlotFill = 0.62f;
        [Tooltip("Cloned HUD row: key label (e.g. F7) TMP size multiplier vs. vanilla template.")]
        [SerializeField] [Range(0.5f, 1.25f)] private float _vanillaHotkeyKeyHintFontScale = 0.82f;

        private RectTransform _rootRect = null!;
        private ScrollRect _scrollRect = null!;
        private RectTransform _contentRect = null!;
        private RectTransform _inputBarRect = null!;
        private ChatPanelDragHandle? _dragHandle;
        private ChatInputBar? _inputBar;

        private readonly List<GameObject> _spawnedMessageRows = new();
        private bool _loggedMissingMessageRowPrefab;
        private int _lastRenderedCount = -1;
        private long _lastRenderedLastTicks = -1;
        private long _lastRenderedFirstSeq = -1;
        private int _lastMessageCountForScroll = -1;
        private bool _scrollToBottomNextFrame;
        private bool _wasInGameplay;
        private bool _wasInputFocused;
        private int _ignoreOpenFromGameInputFramesRemaining;
        private bool _referencesBound;
        private float _nextVanillaHotkeyTryUnscaled;
        private int _unreadWhileChatClosed;
        private KeyCode? _cachedToggleChatPanelKeyForHud;
        private Coroutine? _focusInputCoroutine;

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
            ChatHistoryStore.UnreadCandidateMessageAdded += OnUnreadChatMessageWhileMaybeClosed;
            ApplyChatLauncherForActiveScene();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            ChatHistoryStore.UnreadCandidateMessageAdded -= OnUnreadChatMessageWhileMaybeClosed;
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

        /// <summary>Forces a full message list rebuild the next time the panel is visible (history may have changed while closed).</summary>
        private void InvalidateMessageListRefresh()
        {
            _lastRenderedCount = -1;
            _lastRenderedLastTicks = -1;
            _lastRenderedFirstSeq = -1;
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
            if (_chatRoot == null || _scrollRectOnChatRoot == null || _messageContent == null ||
                _inputBarRectRef == null || _dragHandleRef == null)
            {
                ChatModLog.Error(
                    "[ChatUiBehaviour] Assign all prefab references: Chat Root, Scroll Rect On Chat Root, Message Content, Input Bar Rect Ref, Drag Handle Ref.");
                return false;
            }

            _rootRect = _chatRoot;
            _scrollRect = _scrollRectOnChatRoot;
            _contentRect = _messageContent;
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
                    ClearUnreadHotkeyBadge();
                    DestroyMessageRowsAndResetMessageUiState();
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

            if (_scrollToBottomNextFrame)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 0f;
                _scrollToBottomNextFrame = false;
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
            if (!_referencesBound)
                return;

            bool panelOpen = _chatRoot.gameObject.activeSelf;
            ChatHistoryStore.GetTailMetrics(out int count, out long lastTicks, out long firstSeq);

            bool metricsChanged = force ||
                                  _lastRenderedCount != count ||
                                  _lastRenderedLastTicks != lastTicks ||
                                  _lastRenderedFirstSeq != firstSeq;

            bool rowCountMismatch = panelOpen && _spawnedMessageRows.Count != count;

            if (!metricsChanged && !rowCountMismatch)
                return;

            if (metricsChanged || force)
            {
                _lastRenderedCount = count;
                _lastRenderedLastTicks = lastTicks;
                _lastRenderedFirstSeq = firstSeq;
            }

            if (!panelOpen)
                return;

            if (count != _lastMessageCountForScroll)
            {
                _lastMessageCountForScroll = count;
                _scrollToBottomNextFrame = true;
            }

            IReadOnlyList<ChatEntry> snapshot = ChatHistoryStore.GetSnapshot();
            SyncMessageRows(snapshot, forceFullRebuild: force);
        }

        private void DestroyMessageRowsAndResetMessageUiState()
        {
            for (int i = 0; i < _spawnedMessageRows.Count; i++)
            {
                if (_spawnedMessageRows[i] != null)
                    Destroy(_spawnedMessageRows[i]);
            }

            _spawnedMessageRows.Clear();
            _lastMessageCountForScroll = -1;
            _lastRenderedCount = -1;
            _lastRenderedLastTicks = -1;
            _lastRenderedFirstSeq = -1;
        }

        private void DestroyAllSpawnedMessageRows()
        {
            for (int i = 0; i < _spawnedMessageRows.Count; i++)
            {
                if (_spawnedMessageRows[i] != null)
                    Destroy(_spawnedMessageRows[i]);
            }

            _spawnedMessageRows.Clear();
        }

        private void SyncMessageRows(IReadOnlyList<ChatEntry> messages, bool forceFullRebuild)
        {
            if (forceFullRebuild)
            {
                FullRebuildMessageRows(messages);
                return;
            }

            if (messages.Count == 0)
            {
                DestroyAllSpawnedMessageRows();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
                return;
            }

            const int maxTrims = 4096;
            int trims = 0;

            while (_spawnedMessageRows.Count > 0 && trims < maxTrims)
            {
                var go = _spawnedMessageRows[0];
                if (go == null)
                {
                    _spawnedMessageRows.RemoveAt(0);
                    continue;
                }

                var marker = go.GetComponent<ChatMessageRowMarker>();
                if (marker == null)
                {
                    FullRebuildMessageRows(messages);
                    return;
                }

                if (marker.Sequence == messages[0].Sequence)
                    break;

                if (marker.Sequence < messages[0].Sequence)
                {
                    Destroy(go);
                    _spawnedMessageRows.RemoveAt(0);
                    trims++;
                    continue;
                }

                FullRebuildMessageRows(messages);
                return;
            }

            if (trims >= maxTrims)
            {
                FullRebuildMessageRows(messages);
                return;
            }

            while (_spawnedMessageRows.Count > messages.Count)
            {
                int last = _spawnedMessageRows.Count - 1;
                var go = _spawnedMessageRows[last];
                if (go != null)
                    Destroy(go);
                _spawnedMessageRows.RemoveAt(last);
            }

            while (_spawnedMessageRows.Count < messages.Count)
            {
                int idx = _spawnedMessageRows.Count;
                if (!TryAppendMessageRow(messages[idx]))
                {
                    FullRebuildMessageRows(messages);
                    return;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        }

        private void FullRebuildMessageRows(IReadOnlyList<ChatEntry> messages)
        {
            DestroyAllSpawnedMessageRows();
            if (messages == null || messages.Count == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
                return;
            }

            for (int i = 0; i < messages.Count; i++)
            {
                if (!TryAppendMessageRow(messages[i]))
                    break;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        }

        private bool TryAppendMessageRow(ChatEntry entry)
        {
            string prefabName = entry.IsHost
                ? ChatUiBootstrap.MessageRowHostPrefabName
                : ChatUiBootstrap.MessageRowPrefabName;
            var prefab = ChatUiBootstrap.TryGetNamedPrefab(prefabName);
            if (prefab == null)
            {
                if (!_loggedMissingMessageRowPrefab)
                {
                    ChatModLog.Warning(
                        "[ChatMod] Missing MessageRow / MessageRowHost in mod content (root names must match).");
                    _loggedMissingMessageRowPrefab = true;
                }

                return false;
            }

            var inst = Instantiate(prefab, _contentRect, false);
            inst.name = prefab.name;
            var marker = inst.AddComponent<ChatMessageRowMarker>();
            marker.Sequence = entry.Sequence;

            var tmp = inst.GetComponent<TextMeshProUGUI>() ??
                      inst.GetComponentInChildren<TextMeshProUGUI>(true);
            ChatUiLayout.ApplyConfiguredMessageFontSize(tmp);
            ChatMessageFormatting.ApplyTo(tmp, entry, entry.IsHost);
            _spawnedMessageRows.Add(inst);
            return true;
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
