# ChatUiBehaviour: code vs prefab (thorough)

`ChatUiBehaviour` lives on **ChatUiRoot**. Below: what runs today, whether it can live on the **prefab**, and what must stay **code**.

---

## 1. Reference resolution (`TryResolveReferences`)

| Current behavior | Prefab / editor | Code |
|------------------|-----------------|------|
| — | **Chat Root** `RectTransform` on `ChatUiBehaviour` | **Required** `[SerializeField]`; **fail** if missing |
| — | **Scroll Rect On Chat Root** = **ScrollRect** on **ChatRoot** | **Required**; **fail** if missing |
| — | **Message Content** `RectTransform` | **Required** |
| — | **Input Bar Rect Ref** | **Required** |
| — | **Drag Handle Ref** = **ChatPanelDragHandle** (on DragBar); **RootRect → ChatRoot** | **Required**; **fail** if `RootRect` null |
| `GetComponent<ChatInputBar>` on InputBar | Component on **InputBar** in prefab | **GetComponent** only (fail if missing) |

**Invariant:** **Zero `Transform.Find`** for chat UI binding; missing references fail fast in `Awake`.

---

## 2. Drag (`ChatPanelDragHandle`)

| Current behavior | Prefab | Code |
|------------------|--------|------|
| ~~`RootRect = _rootRect` in `ChatUiBehaviour`~~ | **RootRect → ChatRoot** in inspector | **Removed** — prefab owns the reference |
| `OnDragPosition` → `ChatUiBehaviour` sets `anchoredPosition` | — | **Moved into `ChatPanelDragHandle`** — it writes `RootRect.anchoredPosition` while dragging |
| `OnDragEnded` → `SaveWindowPosition` | — | **Stays** — config persistence is mod logic |

---

## 3. Window position (config)

| Behavior | Prefab | Code |
|----------|--------|------|
| Default anchors/position | **Prefab** | — |
| Restore X/Y from `ModConfig` on `Awake` | — | **Yes** (`ApplySavedWindowPosition`) |
| Clamp to 1920×1080 ref | Could match Canvas Scaler reference resolution in prefab | **Yes** today (simple guard) |
| Save on drag end / destroy | — | **Yes** |

---

## 4. `MessageContent`

| Behavior | Prefab | Code |
|----------|--------|------|
| Shipped **empty** (no placeholder rows) | **Yes** | **No** `ClearMessageContentPlaceholders` (removed) |

---

## 5. `Update` loop

| Behavior | Prefab | Code |
|----------|--------|------|
| Show/hide **ChatRoot** by `Human.LocalHuman` | — | **Yes** (game state) |
| Clear `ChatHistoryStore` when leaving gameplay | — | **Yes** (policy) |
| `RefreshMessages` / rebuild rows | — | **Yes** |
| `ScrollRect.verticalNormalizedPosition = 0` | — | **Yes** (one line; could move to small helper on prefab later) |
| Alt / KeyManager | — | **Yes** |

---

## 6. Input submit / focus

| Behavior | Prefab | Code |
|----------|--------|------|
| `ChatInputBar` + `ChatInputField` hierarchy | **Prefab** | **Bind** + subscribe `OnSubmit` |
| Clear + send + `ReleaseFocus` | — | **Yes** |
| `OpenFromGameInput` coroutine | — | **Yes** |

---

## 7. `ChatInputField` (subclass)

| Behavior | Prefab | Code |
|----------|--------|------|
| Correct TMP font + placeholder refs | **Prefab** | Prefer |
| Placeholder / background tint overrides | Try remove when stable | **Code** today |

---

## 8. What stays code no matter what

- Harmony patches, `ChatUiBootstrap`, `TryGetNamedPrefab`, `Instantiate` rows, `ChatMessageFormatting.ApplyTo`
- `ChatNotificationSound.Play` (WAV path next to DLL until you move to `Assets`)

---

## Summary

- **Done:** No runtime **`RootRect` assignment**; drag **applies `anchoredPosition` inside `ChatPanelDragHandle`**; `ChatUiBehaviour` only subscribes **`OnDragEnded`** → save config.
- **Done:** **Required `[SerializeField]`** on `ChatUiBehaviour`: **Chat Root**, **Scroll Rect On Chat Root**, **Message Content**, **Input Bar Rect**, **Drag Handle** — **no `Find`** for those nodes.
- **Done:** **Empty `MessageContent`** in prefab; **`ClearMessageContentPlaceholders` removed**.
