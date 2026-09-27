# Wand Skins — Progress

Spec: `Docs/WandSkins_Brief.md` (9 effect layers, 5 tiers, one phase per session). This file is the source of truth between sessions; trust it.

## Phases
- Phase 0, groundwork (built under an earlier, simpler brief): DONE, committed ("Wand skins groundwork").
- Phase 1, effect architecture + all 9 layers on Default + quality levels + Try preview + Stardust + Prism: NOT DONE (next).
- Phase 2, Moonlight, Comet, Fire, Water, Air, Earth: NOT DONE (Moonlight and Comet exist in a simpler form).
- Phase 3, Jupiter's Lightning, Aurora, Galaxy: NOT DONE.
- Phase 4, Phoenix, Void, Pixie Companion: NOT DONE.

## What exists (refactor into the new architecture, don't rebuild)
- `PathManager.Dragged(Vector3)`: visual-only, board-local finger point, raised in OnPointerDown/OnDrag (so SimulateDrag/AutoPlayer too).
- `WandTrail` is the host, but effects are inline (no ISkinEffect yet): pooled segment LineRenderers (WandLine shader),
  speed-scaled drag dust, reach burst + optional flash, Comet head/tail (ring buffer), Gold Ink speed→width profile.
  `Clear()` returns everything to the pool; listens to NodeVisited, PathCleared, BoardBuilt, Dragged, Cosmetics.Changed.
- `WandLine.shader` (URP 2D, premultiplied): capsule SDF per segment. `_Seg` = (drawn length, half width, path distance at
  start, full length), `_SegWidth` = core width at 0, 1/3, 2/3, 1. AA core, round ends, halo, core softness. Modes =
  `WandEffect`: 0 Classic, 1 Stardust glints, 2 Rainbow hue, 3 Moonlight shimmer, 4 Comet soft trail, 5 Gold Ink metallic.
  Fallback: two plain LineRenderers when the shader is unsupported.
- `WandLook` (ScriptableObject, `Assets/Settings/Wands/*.asset`): every visual value (line, shimmer, ink, comet, dust,
  burst, flash). Created once by `OneLineSetup.Look()`, never overwritten, so inspector tuning survives; OnValidate retunes live.
- `CosmeticCatalog.Wand`: id, displayName, price, exclusive, `effect` (WandEffect), `rarity` (WandRarity: Common, Rare,
  Legendary), `look`. Equip slot + save key `OneLine.Wand` (Cosmetics / SaveService). Unknown or unowned id → first (Classic).
- `WandPreview` (uGUI MaskableGraphic): shop preview mirroring shader + dust per effect; runs only while visible (cull).
- `ShopUI`: "Wands" section (preview, name, rarity label + frame color, price/Equip); tap a row → big preview overlay. No Try demo.
- `Art`: Mote (dust), Flake (gold leaf). `UIStyle`: rarity colors, preview sizes/timings. `StarStyle.wandShader` holds the shader.

## Decisions (user)
- Build on the groundwork; Phase 1 introduces the ISkinEffect interface and moves the inline Comet/Ink logic into effect classes.
- Rainbow becomes Prism (Epic) and keeps id `wand.rainbow` (the user owns it).
- Gold Ink stays as an extra Rare (not in the brief); port it to the new architecture.
- All Mythics are sold for coins (no Celestial Pass exclusives for now).
- Tiers: extend WandRarity to Common, Rare, Epic, Legendary, Mythic (Legendary is unused so far, so reordering is safe).

## Catalog ids (keep stable)
| id | now | brief |
|---|---|---|
| wand.stardust | Classic, free | Default |
| wand.moonbeam | Moonlight, Common 120 | Moonlight |
| wand.twinkle | Stardust, Rare 300 | Stardust |
| wand.rainbow | Rainbow, Rare 300 | Prism (Epic) |
| wand.comet | Comet, Rare 300 | Comet (Epic) |
| wand.goldink | Gold Ink, Rare 300 | extra Rare |
| wand.ember | old dust-only wand | Fire (Epic) |

New ids later: wand.water, wand.air, wand.earth, wand.lightning, wand.aurora, wand.galaxy, wand.phoenix, wand.void, wand.pixie.

## Prices (coins ≈ 10–12 per level, boosts 30/40)
Common 120 · Rare 300 · Epic 480 · Legendary 840 · Mythic 1440. Owners keep items when prices change. Prices live in the catalog (set by OneLineSetup.BuildWands).

## Files
- `Assets/Scripts/OneLine/WandTrail.cs`: board host (segments, dust, burst, comet, ink)
- `Assets/Scripts/OneLine/WandLook.cs`: settings ScriptableObject, enums, shared curves
- `Assets/Scripts/OneLine/WandPreview.cs`: shop preview graphic
- `Assets/Shaders/WandLine.shader`: segment shader
- `Assets/Settings/Wands/*.asset`: looks (Classic, Moonlight, Stardust, Rainbow, Comet, Gold Ink, Ember)
- `CosmeticCatalog.cs`: Wand item; `ShopUI.cs`: Wands section + big preview
- `PathManager.cs`: Dragged; `Art.cs`: Mote/Flake; `UIStyle.cs`: shop values; `StarStyle.cs`: wandShader
- `Editor/OneLineSetup.cs`: BuildWands(), Look(), AssignBoardShaders()

## Known issues / TODO
- Missing from the brief: ISkinEffect, energy flow, ambient life, escalation, star flash + ring, win flourish (needs a
  visual-only hook before CompletionSequence), per-skin sounds via GameFeedback, High/Low quality + auto-detect, 5 tiers +
  animated Mythic frame, Try demo (~6 stars, all layers, sound), element signs under element skins.
- Editing scripts or creating assets while the Editor is in Play mode triggers a reload. ZodiacBackground then spams
  NullReferenceException (a separate fix task was started). Stop Play mode before editing.
- Untested so far: real touch input, the purchase flow, device performance (tests used SimulateDown/SimulateDrag).

## Editor steps
- None right now. Re-running One Line/Setup Levels + Scene is safe (rebuilds the wand list, keeps tuned WandLook assets).

## Testing tips
- Play-mode driver: execute_script starting a coroutine on PathManager (SimulateDown/SimulateDrag along level.solution),
  ScreenCapture to a scratch folder; restore SaveService (owned, equipped, coins) at the end.
