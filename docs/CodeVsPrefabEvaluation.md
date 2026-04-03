# Code vs prefab evaluation (ChatMod UI)

Goal: prefer **authoring in the prefab** where Unity/TMP already solve the problem; keep **code** only for what the prefab cannot do (game API, Harmony, data, spawning dynamic rows).

Legend: **Move to prefab** · **Simplify code** · **Keep code** (no good prefab-only substitute) · **Remove** (dead or redundant)

---

## `ChatUiBehaviour`

| What the code does today | Prefab / editor alternative | Recommendation |
|--------------------------|----------------------------|----------------|
| ~~`AddComponent<AudioSource>()`~~ | — | **Removed.** `ChatNotificationSound.Play()` uses `PlayClipAtPoint` and loads `chat_message.wav` from the plugin folder. |
| ~~`AddComponent<ChatPanelDragHandle>`~~ | **ChatPanelDragHandle** on **DragBar**; **RootRect → ChatRoot** only in prefab; drag applies position inside handle | **Done:** required **SerializeField** for handle; **fail** if `RootRect` null. |
| ~~`AddComponent<ChatInputBar>`~~ | **ChatInputBar** on **InputBar** | **Done:** `GetComponent` only; **fail** if missing. |
| `TryResolveReferences` reads **required** `[SerializeField]` refs only | Wire **ChatUiRoot** in inspector | **Done:** **no `Transform.Find`**; **fail** if any reference missing. |
| `ApplySavedWindowPosition` + `ClampWindowToReferenceResolution` | Default position/anchors entirely in prefab | **Split**: **Prefab** = default layout. **Code** = optional persistence (read/write `ModConfig`). If you drop persistence, delete position code and rely on prefab only. |
| ~~`ClearMessageContentPlaceholders`~~ | **MessageContent** empty in shipped prefab | **Removed** from code. |
| `Update`: hide/show `ChatRoot` when `Human.LocalHuman` absent/present | Cannot know game state in prefab | **Keep code** (or move to a single `ModVisibilityController` component—still code, but could live on prefab as a small script). |
| `Update`: `ChatHistoryStore.Clear` when leaving gameplay | Game/mod policy | **Keep code** unless you change product behavior (keep history in menu). |
| `RefreshMessages` / destroy + instantiate row prefabs | Row prefabs are already in project | **Keep code** for **spawn + set text**; prefab cannot replace dynamic list binding. Optional: **object pool** later—still code. |
| `ChatMessageFormatting.ApplyTo` → `tmp.text = …` | Could use **TextMeshPro** + binding assets (overkill) | **Keep code** for string formatting; prefab supplies **layout/font/sprite** on the row TMP. |
| `ScrollRect.verticalNormalizedPosition = 0` after rebuild | **ScrollRect** + content size; “scroll to bottom” is one line | **Keep thin code** or attach a **ScrollToBottomOnRectTransformChange** helper on the prefab that listens to `MessageContent` layout changes—still a script, but not in `ChatUiBehaviour`. |
| `HandleAltReleaseFocus` / `SyncKeyManagerTypingState` | Needs `KeyManager` + input | **Keep code** (game integration). |
| `OnChatInputSubmit`: `Clear`, `ReleaseFocus`, `ChatMessageSender.Send` | Submit behavior is mod policy | **Keep code**; prefab cannot send network chat. |
| `OpenFromGameInput` + coroutine focus | Focus is API | **Keep code** (small). |
| ~~`LoadNotificationSoundCoroutine` / `SetCachedClip`~~ | — | **Removed** (redundant with `Play()` loading the WAV). |

---

## `ChatInputField` (subclass of `TMP_InputField`)

| What the code does | Prefab alternative | Recommendation |
|--------------------|-------------------|----------------|
| Tint `targetGraphic` on select/deselect | Use **TMP / Selectable** color blocks or duplicate Image styling in prefab | **Try prefab first**: correct **font** on Text + Placeholder fixes most TMP bugs. If placeholder still breaks, keep minimal override or use **UnityEngine.UI.InputField**-style events in prefab only (harder with TMP). |
| `LateUpdate` → `SyncPlaceholderToText` (enable + alpha) | Fix **Font Asset** on **Text Component**; correct **Placeholder** assignment | **Remove code** if prefab + font are correct; **keep** only if you still see TMP skipping `UpdateLabel` when `font == null`. |

---

## `ChatInputBar`

| What the code does | Prefab alternative | Recommendation |
|--------------------|-------------------|----------------|
| `Build()` constructs full hierarchy at runtime | You already use **authored** `ChatInputField` under `InputBar` | **Remove or gate** `Build()` if never called—reduces two code paths. |
| `BindExistingHierarchy` | N/A if `ChatInputBar` is on prefab: use **`Reset()` or `Awake`** on `ChatInputBar` to self-wire | **Simplify**: `ChatInputBar.Awake` finds `ChatInputField` and subscribes to `onSubmit`—no call from `ChatUiBehaviour`. |

---

## `ChatPanelDragHandle`

| What the code does | Prefab alternative | Recommendation |
|--------------------|-------------------|----------------|
| Exists as behaviour | Add on **DragBar**; set **RootRect** in inspector | **Prefab**; code only wires events if you keep `ChatUiBehaviour` as listener, or move save/load position onto a small component on **ChatRoot**. |

---

## `ChatUiBootstrap`

| What the code does | Prefab alternative | Recommendation |
|--------------------|-------------------|----------------|
| `Instantiate` + `DontDestroyOnLoad` | None—runtime entry | **Keep code**. |
| `TryGetNamedPrefab` | Register assets in mod exporter | **Keep code**; prefab list is data. |

---

## Patches / non-UI

Harmony patches (`ChatMessage`, `InputWindow`, `KeyManager`, etc.) have **no prefab alternative**—they stay in code.

---

## Suggested phased cleanup (low risk → higher impact)

1. ~~**Delete** unused `AudioSource`~~ **Done** (also removed redundant `LoadNotificationSoundCoroutine` / `SetCachedClip`).
2. ~~**Prefab** + no `AddComponent` for drag/input~~ **Done**; ~~optional SerializeField refs~~ **Done** — **required** refs, **no `Find`**.
3. ~~**Empty `MessageContent`** / remove clear~~ **Done.**
4. **Retire `ChatInputField` overrides** after verifying font + placeholder in prefab; if stable, revert to **`TMP_InputField`** in prefab.
5. **Retire `ChatInputBar.Build`** if unused.
6. **Position**: either keep config persistence (code) or delete and use prefab anchors only.

---

## Summary

- **Done:** **Required serialized field references** (no `Find`); default layout when not using saved position. (`MessageContent` empty; no runtime clear.)
- **Must stay in code (or a tiny dedicated component):** gameplay visibility, history clear policy, row **instantiate + text**, scroll-to-bottom trigger, submit/send/focus integration, patches, prefab loading.
- **Biggest win for “less buggy”:** one **authored** path (prefab + SerializeField refs), **no runtime `AddComponent` for things that can be on the prefab**, and **removing** duplicate `Build()` vs `Bind` paths.

*When you implement changes, update `ChatModUiFlow.md` to match.*
