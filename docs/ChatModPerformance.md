# ChatMod — performance notes (send / receive lag)

Investigation summary: reported hitches when sending or receiving chat often align with work done **on the main thread** in the mod’s own paths.

## 1. Notification sound (high impact when enabled)

`ChatNotificationSound.Play()` → `WavLoader.LoadFromFile()` **every** play:

- Reads the full WAV from disk (`File.ReadAllBytes`).
- Parses WAV and allocates sample buffers.
- Creates a **new** `AudioClip` each time.
- Then `AudioSource.PlayClipAtPoint`.

All of this runs synchronously when the chat patch runs (same frame as `PrintToConsole` / history update).

**Mitigation idea:** Load once, cache `AudioClip` (and resolved path), reuse for playback.

## 2. Message list UI sync (incremental; full rebuild only as fallback)

**`ChatHistoryStore`** assigns each entry a monotonic **`Sequence`** and trims from the head when over `MaxMessages`.

**`ChatUiBehaviour`** keeps spawned rows tagged with **`ChatMessageRowMarker.Sequence`**. On each refresh (when the panel is open and history changed, or row count ≠ store count):

- **Trim head:** destroy leading rows until the first row’s sequence matches `messages[0]` (covers store head trim).
- **Trim tail:** if too many rows (rare), destroy from the end.
- **Append:** instantiate **only** new tail messages (typically **one** per send/receive).

**`FullRebuildMessageRows`** runs only on `RefreshMessages(force: true)`, missing markers, sequence mismatch (stale UI), or safety caps.

Each update still does **`LayoutRebuilder.ForceRebuildLayoutImmediate`** on `MessageContent` when anything structural changed — cheaper than recreating every row, but not free.

## 3. Per-frame change detection (no list alloc when idle)

`Update` → `RefreshMessages` → **`ChatHistoryStore.GetTailMetrics`** (count, last timestamp ticks, first sequence) under lock **without** copying the message list.

A **full `GetSnapshot()`** runs only when the panel is open and a UI sync is actually needed.

**Remaining cost when idle:** one cheap metrics read per gameplay frame (no allocations).

## 4. Scroll-to-bottom pass

After new messages, `Canvas.ForceUpdateCanvases()` forces a broad canvas/layout update — can still hitch on top of content layout.

## Quick checks

- Toggle **`PlaySoundOnMessage`** off: if lag drops sharply, prioritize sound caching.
- Compare behavior with **few vs many** stored messages (chat open): if lag scales with count, prioritize incremental UI.

---

*Last updated: performance investigation (chat send/receive lag).*