# ChatMod UI flow (as implemented today)

This document maps **runtime behavior** and highlights where **C# overrides or fights** authored prefab / TMP defaults. Mermaid renders in GitHub, many IDEs, and some Markdown previewers.

---

## 1. Startup (mod load)

```mermaid
flowchart TD
  A[Game loads mod: ChatMod.OnLoaded] --> B[ModConfig.Bind]
  B --> C[Harmony.PatchAll]
  C --> D[ChatUiBootstrap.EnsureExists contentHandler]
  D --> E{ChatUiBehaviour already in scene?}
  E -->|yes| F[Use existing instance]
  E -->|no| G[Load ChatUiRoot from ContentHandler.prefabs by name]
  G --> H[Instantiate + DontDestroyOnLoad]
  H --> I[GetComponent ChatUiBehaviour]
  I --> J[Awake runs on instance]
```



**On `ChatUiBehaviour.Awake` (first frame of instance):**

```mermaid
flowchart TD
  A[Awake] --> B[TryResolveReferences]
  B --> C[Assign from required SerializeFields only — no Find]
  C --> D{Refs + DragHandle.RootRect + ChatInputBar OK?}
  D -->|no| E[Log error; disable behaviour]
  D -->|yes| F[Wire OnDragEnded; BindExistingHierarchy + OnSubmit]
  F --> G[ApplySavedWindowPosition if config has X/Y]
  G --> H[ClampWindowToReferenceResolution when position applied]
```



**Takeaway:** At startup, code **requires** all **`ChatUiBehaviour` serialized references** (no `Transform.Find`), plus **`ChatPanelDragHandle` / `ChatInputBar`** on the prefab (no runtime `AddComponent`); **`MessageContent` is empty in the shipped prefab** (no runtime clear). **Config** can **overwrite** `ChatRoot` position and **clamp**. Notification sound: `ChatNotificationSound.Play()` beside DLL.

---

## 2. Every frame while in gameplay (`Update`)

```mermaid
flowchart TD
  U[Update] --> G{Human.LocalHuman?}
  G -->|no| H[Clear ChatHistoryStore if was in game]
  H --> I[Set ChatRoot inactive]
  G -->|yes| J[Ensure ChatRoot active]
  J --> K[HandleAltReleaseFocus: LeftAlt -> ReleaseFocus]
  J --> L[SyncKeyManagerTypingState: focus -> KeyManager Typing]
  J --> M[RefreshMessages if history changed]
  M --> N{Rebuild?}
  N -->|yes| O[Destroy all row instances; Instantiate MessageRow/MessageRowHost; Apply MessageFontSize to TMP; ApplyTo text]
  N -->|no| P[Skip]
  J --> Q{scrollToBottomNextFrame?}
  Q -->|yes| R[ForceUpdateCanvases; verticalNormalizedPosition = 0]
```



**Takeaway:** Code drives **visibility** of the whole panel (in-game vs menu), **scroll position** when history changes, **keyboard/game input routing** (KeyManager), and **full rebuild** of message rows (destroy + instantiate). It does **not** resize ChatRoot or viewport each frame anymore (that was removed in favor of trusting the prefab).

---

## 3. Opening chat from the game (vanilla chat prompt)

```mermaid
flowchart TD
  V[Player opens vanilla chat: InputWindow.GetText chat title] --> P[InputWindowGetTextPatch Prefix]
  P --> Q[ChatUiBootstrap.EnsureExists]
  Q --> R[OpenFromGameInput]
  R --> S{ignore frames > 0?}
  S -->|yes| T[Skip focus]
  S -->|no| U{Already focused?}
  U -->|yes| V2[RequestFocus]
  U -->|no| W[Coroutine: wait 3 frames -> RequestFocus]
```



---

## 4. Submitting from the mod input field (`Enter`)

```mermaid
flowchart TD
  S[User presses Enter on TMP_InputField] --> T[ChatInputBar.HandleSubmit -> OnSubmit event]
  T --> U[ChatUiBehaviour.OnChatInputSubmit]
  U --> V[Always Clear input first]
  V --> W{Whitespace only?}
  W -->|yes| X[ReleaseFocus + ignore frames counter]
  W -->|no| Y[ChatMessageSender.Send]
  Y --> Z[ReleaseFocus]
```



**Takeaway:** **Clear runs before** branching. So the field is always emptied on submit attempt (including empty Enter). That is **by design** in code, not the prefab.

---

## 5. New chat lines appearing (history)

```mermaid
flowchart TD
  C[ChatMessage.PrintToConsole patch] --> H[ChatHistoryStore.Add]
  H --> E[ChatUiBootstrap.EnsureExists]
  C --> S[Optional ChatNotificationSound.Play]
  U[ConsoleWindow patch etc.] --> H
  H --> R[Next Update: RefreshMessages sees new snapshot]
  R --> B[Sync message rows (incremental trim/append)]
```



---

## 6. Input field: TMP vs `ChatInputField` (parallel to prefab)

```mermaid
flowchart TD
  TMP[TMP_InputField base] --> A[UpdateLabel sets placeholder when font non-null etc.]
  CF[ChatInputField subclass] --> B[OnSelect/OnDeselect: recolor targetGraphic Image]
  CF --> C[LateUpdate after base: SyncPlaceholderToText]
  C --> D[placeholder.enabled = IsNullOrEmpty text]
  C --> E[TextMeshProUGUI.alpha = 0 or color.a]
```



**Takeaway:** This **overrides** default placeholder visibility every **LateUpdate** and tweaks **graphic colors** on select/deselect. If something feels “prefab should control it but doesn’t,” this layer is the first suspect—along with **TMP’s own** `DeactivateInputField` placeholder logic.

---

## Code vs prefab: who owns what?


| Area                                                 | Mostly prefab                 | Code still intervenes                                                       |
| ---------------------------------------------------- | ----------------------------- | --------------------------------------------------------------------------- |
| ChatRoot layout, colors, sizes                       | Yes                           | Position from config + clamp; drag moves `anchoredPosition`                 |
| Message row visuals (font, sprite layout per prefab) | Yes                           | **Replaces** entire `text` string; strips `(Host)` in formatting            |
| MessageContent at runtime                            | Empty in prefab                 | Rows **spawned** from **MessageRow** / **MessageRowHost**; **MessageFontSize** applied to each row TMP at spawn       |
| Scroll view                                          | Yes                           | Sets `verticalNormalizedPosition` to bottom on history change               |
| Input field layout                                   | Yes                           | May **add** `ChatInputBar`; **Clear** + **ReleaseFocus** on submit          |
| Placeholder / input chrome                           | Intended prefab + TMP         | **ChatInputField** forces placeholder enable/alpha; **Image** tint on focus |
| Panel visible in menu                                | —                             | **Hidden** when not `Human.LocalHuman`                                      |


---

## Glossary: “blur”

**Blur** here means the **input field loses focus** (same idea as “blur” in HTML): the caret leaves the field—typically because the user **clicks elsewhere**, **tabs away**, **Escape**, `**DeactivateInputField`**, or your code calls `**ReleaseFocus()**` (which clears EventSystem selection). It is **not** a graphics post-processing effect.

**Field “blurring” issue (recap):** If **typed text disappears** when you only click away, that is **not** explained by `OnChatInputSubmit` (that path clears only on **Enter/submit**). Then look for **prefab UnityEvents** (`onEndEdit`, etc.), **wrong TMP references**, or **game/mod** side effects. If the **placeholder** does not return when the field is **empty**, suspect **invisible characters** in `text`, **placeholder alpha/color**, or the **ChatInputField** sync fighting TMP.

---

## Font size + host sprite (design note)

**MessageFontSize** (config, integer **12–26**, default **18**) is applied to each spawned row **TMP** at runtime (auto-sizing off). If a **sprite** in the host row ever looks off-center at extreme sizes, tune the **sprite asset** or prefab rather than adding per-size prefabs—this mod assumes one **MessageRow** / **MessageRowHost** pair scales acceptably.

---

*Generated for the UnityChatMod project; update this file when behavior changes.*