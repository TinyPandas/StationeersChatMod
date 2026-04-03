#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChatMod
{
    /// <summary>
    /// Owns all message row rendering: spawning, incremental sync, formatting, and layout.
    /// Attach to the same GameObject as (or a child of) the ScrollRect content area.
    /// </summary>
    public sealed class ChatMessageListView : MonoBehaviour
    {
        [SerializeField] private RectTransform _contentRect = null!;

        private readonly List<GameObject> _rows = new();
        private bool _loggedMissingPrefab;
        private int _lastRenderedCount = -1;
        private long _lastRenderedLastTicks = -1;
        private long _lastRenderedFirstSeq = -1;
        private int _lastCountForScroll = -1;

        public bool ScrollToBottomPending { get; private set; }

        public void AcknowledgeScrollToBottom() => ScrollToBottomPending = false;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Sync rows to <paramref name="snapshot"/>. Pass <paramref name="force"/> to force a full rebuild.</summary>
        public void Refresh(IReadOnlyList<ChatEntry> snapshot, bool force = false)
        {
            bool metricsChanged = force ||
                                  _lastRenderedCount != snapshot.Count ||
                                  _lastRenderedLastTicks != TailTicks(snapshot) ||
                                  _lastRenderedFirstSeq != HeadSeq(snapshot);

            bool rowCountMismatch = _rows.Count != snapshot.Count;

            if (!metricsChanged && !rowCountMismatch)
                return;

            if (metricsChanged || force)
            {
                _lastRenderedCount = snapshot.Count;
                _lastRenderedLastTicks = TailTicks(snapshot);
                _lastRenderedFirstSeq = HeadSeq(snapshot);
            }

            if (snapshot.Count != _lastCountForScroll)
            {
                _lastCountForScroll = snapshot.Count;
                ScrollToBottomPending = true;
            }

            SyncRows(snapshot, force);
        }

        /// <summary>Destroy all rows and reset render state.</summary>
        public void Clear()
        {
            DestroyAllRows();
            _lastRenderedCount = -1;
            _lastRenderedLastTicks = -1;
            _lastRenderedFirstSeq = -1;
            _lastCountForScroll = -1;
            ScrollToBottomPending = false;
        }

        // ── Row sync ──────────────────────────────────────────────────────────

        private void SyncRows(IReadOnlyList<ChatEntry> messages, bool forceFullRebuild)
        {
            if (forceFullRebuild)
            {
                FullRebuild(messages);
                return;
            }

            if (messages.Count == 0)
            {
                DestroyAllRows();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
                return;
            }

            // Trim stale rows from the head (history trimmed from the store).
            const int maxTrims = 4096;
            int trims = 0;
            while (_rows.Count > 0 && trims < maxTrims)
            {
                var go = _rows[0];
                if (go == null) { _rows.RemoveAt(0); continue; }

                var marker = go.GetComponent<RowMarker>();
                if (marker == null) { FullRebuild(messages); return; }
                if (marker.Sequence == messages[0].Sequence) break;
                if (marker.Sequence < messages[0].Sequence)
                {
                    Destroy(go);
                    _rows.RemoveAt(0);
                    trims++;
                    continue;
                }

                FullRebuild(messages);
                return;
            }

            if (trims >= maxTrims) { FullRebuild(messages); return; }

            // Trim excess rows from the tail.
            while (_rows.Count > messages.Count)
            {
                int last = _rows.Count - 1;
                if (_rows[last] != null) Destroy(_rows[last]);
                _rows.RemoveAt(last);
            }

            // Append new rows at the tail.
            while (_rows.Count < messages.Count)
            {
                if (!TryAppendRow(messages[_rows.Count]))
                {
                    FullRebuild(messages);
                    return;
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        }

        private void FullRebuild(IReadOnlyList<ChatEntry> messages)
        {
            DestroyAllRows();
            if (messages == null || messages.Count == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
                return;
            }

            foreach (var entry in messages)
            {
                if (!TryAppendRow(entry)) break;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRect);
        }

        private bool TryAppendRow(ChatEntry entry)
        {
            string prefabName = entry.IsHost
                ? ChatUiBootstrap.MessageRowHostPrefabName
                : ChatUiBootstrap.MessageRowPrefabName;

            var prefab = ChatUiBootstrap.TryGetNamedPrefab(prefabName);
            if (prefab == null)
            {
                if (!_loggedMissingPrefab)
                {
                    ChatModLog.Warning("[ChatMod] Missing MessageRow / MessageRowHost prefab in mod content.");
                    _loggedMissingPrefab = true;
                }
                return false;
            }

            var inst = Instantiate(prefab, _contentRect, false);
            inst.name = prefab.name;

            var marker = inst.AddComponent<RowMarker>();
            marker.Sequence = entry.Sequence;

            var tmp = inst.GetComponent<TextMeshProUGUI>() ??
                      inst.GetComponentInChildren<TextMeshProUGUI>(true);

            ApplyFontSize(tmp);
            ApplyFormatting(tmp, entry);

            _rows.Add(inst);
            return true;
        }

        private void DestroyAllRows()
        {
            foreach (var go in _rows)
                if (go != null) Destroy(go);
            _rows.Clear();
        }

        // ── Formatting (absorbed from ChatMessageFormatting) ──────────────────

        private const string HostSpriteTag = "<sprite name=\"host_icon_3\">";

        private static void ApplyFormatting(TextMeshProUGUI? tmp, ChatEntry entry)
        {
            if (tmp == null) return;
            tmp.text = BuildLine(entry);
        }

        private static string BuildLine(ChatEntry entry)
        {
            string nameHex = entry.NameColorHex ?? ChatEntry.DefaultNameColorHex;
            string name = StripHostSuffix(entry.DisplayName);
            string prefix = entry.IsHost ? HostSpriteTag : "";
            return $"[{entry.Timestamp:HH:mm:ss}] {prefix}<color=#{nameHex}>{name}</color>: {entry.Message}";
        }

        private static string StripHostSuffix(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return displayName;
            var s = displayName.TrimEnd();
            const string token = "(Host)";
            if (s.Length >= token.Length && s.EndsWith(token, StringComparison.OrdinalIgnoreCase))
                return s.Substring(0, s.Length - token.Length).TrimEnd();
            return displayName;
        }

        // ── Layout (absorbed from ChatUiLayout) ───────────────────────────────

        private static void ApplyFontSize(TextMeshProUGUI? tmp)
        {
            if (tmp == null) return;
            float size = ModConfig.MessageFontSize.Value;
            tmp.enableAutoSizing = false;
            tmp.fontSize = size;
            tmp.fontSizeMin = size;
            tmp.fontSizeMax = size;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static long TailTicks(IReadOnlyList<ChatEntry> list) =>
            list.Count > 0 ? list[list.Count - 1].Timestamp.Ticks : -1;

        private static long HeadSeq(IReadOnlyList<ChatEntry> list) =>
            list.Count > 0 ? list[0].Sequence : -1;

        // ── RowMarker (private nested, replaces ChatMessageRowMarker) ─────────

        private sealed class RowMarker : MonoBehaviour
        {
            public long Sequence { get; set; }
        }
    }
}
