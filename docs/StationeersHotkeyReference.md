# Matching Stationeers Helmet / Light hotkeys (exported `Base` scene)

You cannot ship edits to the game’s `Base` scene. Practical options:

1. **Replica on your mod canvas (recommended)** — Build a **simplified** `ChatHotkey` row on `ChatUiRoot` using the **same sprites and layout pattern** as `HelmetHotkey` / `LightHotkey`. Use Unity’s **Button** + **TMP** for the key label; skip game-only scripts (`HotkeyController`, color-blind helpers, etc.).
2. **Runtime reparent (advanced, fragile)** — After load, `GameObject.Find("HotKeyPanel")` (or search `Resources`/scene roots) and `transform.SetParent(hotKeyPanel, false)` so your control sits next to the stock buttons. Breaks if Rocketwerkz renames objects; order may need `SetAsLastSibling()`.
3. **Harmony / prefix patches** — Inject UI where the game builds `HotKeyPanel`. Highest maintenance; only if you must be *inside* their hierarchy.

Below is what the **exported** project shows for structure and assets.

---

## Hierarchy (from `Assets/Scenes/Base.unity`)

Path you gave (names only):

`GameCanvas` → `PanelStatusInfo` → `PanelVerticalGroup` → `Internals` → `HotKeyPanel` → …

**`HotKeyPanel`** (`GameObject` id `1606` in export) is a horizontal strip; it lists several hotkey rows as children (including helmet/light).

### `HelmetHotkey` (`GameObject` id `2999`)

| Object        | Role |
|---------------|------|
| **HelmetHotkey** (root) | **Image** (panel background) + **HorizontalLayoutGroup** + `CanvasRenderer`. Two layout children. |
| **HotkeyHint** (`1191`) | Game-specific hotkey UI: layout, optional overlay **Image**, scripts wiring **Button**, **TMP** key text, etc. Not portable as-is. |
| **IconBG** (`3912`) | **Image** (inner frame) + child **HelmetImage** (`3869`) with the helmet **Image** (simple sprite). |

**`LightHotkey` (`3036`)** uses the **same pattern**: root **Image** + **HorizontalLayoutGroup** + **HotkeyHint** + **IconBG** + icon child (light bulb in game).

### Root row visuals (both Helmet and Light)

- **Root `Image`**: **Sliced** (`m_Type: 1`), color white **~75% alpha** (`a ≈ 0.749`).
- **HorizontalLayoutGroup** on root: `ChildAlignment: Middle Center (5)`, control child width/height.

---

## Sprite assets to copy into your mod project

All paths are under the **exported** project:

`D:\StationeersExport\ExportedProject\Assets\Sprite\`

Copy each **`.asset`** and its **`.meta`** (Unity needs GUIDs to stay consistent if you want predictable references). If AssetRipper also exported **textures** referenced by those sprites, copy those too (often under `Assets/Texture2D` or similar in the same export).

| GUID | File (in export) | Used on |
|------|------------------|---------|
| `0ac3d717ad39d5c40be870d5f0217ece` | `lefthotkey-bg.asset` | **HelmetHotkey / LightHotkey root** background (sliced panel). |
| `07b655530c8bf654396e589709e7bc8e` | `lefthotkey-top.asset` | **IconBG** frame behind the helmet/light icon (sliced). |
| `d3617d4c0797ce04fbea37836bb944ec` | `icon-helmetclosed.asset` | **HelmetImage** (simple icon). |
| `faeb43d38352ee9478f50b35bdc0e0f6` | `window-bg.asset` | Optional overlay on **HotkeyHint** (disabled/hidden layer in scene — reference only). |

**Related hotkey kit (for closer HotkeyHint mimic):**

- `righthotkey-bg.asset`
- `righthotkey-top.asset`
- `righthotkey-pressed.asset`
- `hotkeydivider.asset`

Pick a **chat** icon: there is no dedicated “chat” icon in the small search we did; options are use a generic **UI icon** from the same `Sprite` folder, or supply your own PNG + Sprite in your mod.

**TMP font** (if you match key labels): `HelmetHotkey` scene references a font asset `af5cbf2af16d75d40af13e461fe48c73` — locate the corresponding `.asset` under the export (search by GUID in `.meta` files) and copy if you want identical typography; otherwise use your existing LiberationSans / project default.

---

## Simplified `ChatHotkey` prefab recipe (your mod)

1. **Root** `ChatHotkey` — `RectTransform` + **HorizontalLayoutGroup** (same alignment idea as vanilla) + **Image** → sprite `lefthotkey-bg`, **Sliced**, alpha ~0.75.
2. **Child** `HotkeyHint` — **LayoutElement** (min width ~196, min height ~86 if you match export `LayoutElement` on HotkeyHint) + **Button** (target graphic = child Image) + **TMP** for `"T"` or your binding text. Optional: secondary **Image** using `righthotkey-*` sprites for vanilla-style layers.
3. **Child** `IconBG` — **Image** `lefthotkey-top`, sliced + child **Image** for your chat icon (custom or stolen-from-export icon asset).

Wire **Button.onClick** to your existing `ChatUiBootstrap.OpenFromGameInput()` (or toggle chat visibility).

**Sorting:** Parent under your mod canvas and use **`SetAsLastSibling()`** if anything draws on top of the hotkey bar area.

---

## Legal / distribution note

Sprites and fonts are **game assets**. For **personal modding** you often copy from your own game files; for **public releases** check Stationeers / Rocketwerkz modding or EULA terms and prefer **original art** if required.

---

## Export sanity check

If your mod’s **root canvas `RectTransform` localScale is `(0,0,0)`**, nothing will render. Use **`(1,1,1)`** on the canvas root. `ChatUiBehaviour` can enforce this at runtime (see `EnsureCanvasRootScale()`).
