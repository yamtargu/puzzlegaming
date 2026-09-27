<!-- The user's brief for the Wand skins feature, saved so each session can read it instead of re-pasting it.
     Its "Current state" section predates the groundwork commit; the real state is in Docs/WandSkins_Progress.md. -->

Feature: premium line skins ("Wands") bought with coins. These must be SPECTACULAR, not simple color swaps: layered, animated, reactive effects that make players want to buy them. Purely cosmetic: nothing may affect gameplay, input, snapping, timing or level results (CLAUDE.md rule).

## Quality bar
- Every skin must look impressive both in motion AND in a still screenshot (it will be used for store screenshots).
- Premium mobile-game quality: layered depth, smooth motion, rich but readable. The path and stars must always stay clearly visible; effects never hide where the line goes.
- Fits the night-sky / zodiac theme: elegant and magical, not neon or cheap looking.
- Motion design: nothing static. Use easing from Tween.cs, noise-based motion (not pure sine), random phases so things never move in sync.

## Current state (verified in code, don't re-explore this)
- WandTrail.cs (~150 lines): on PathManager.NodeVisited it creates 2 LineRenderers per segment (glow with Art.SoftLineMaterial + core) via PathManager.CreateLine, tweens the tip from the previous star to the new one over style.trailDrawTime, and emits dust from one ParticleSystem (explicit Emit, maxParticles 400, Art.SoftCircle). Segments are Destroy()ed in Clear().
- There is NO finger-following effect today. PathManager.OnDrag(screen) is private; the only drag API is SimulateDrag (for AutoPlayer).
- CosmeticCatalog.cs: Item (id, displayName, price, useTierColors, exclusive) → LineSkin (line, node colors). Asset: Assets/Levels/CosmeticCatalog.asset.
- Skin colors are applied at level load in LevelManager.cs (~line 124). ShopUI.cs shows color swatches (~lines 145, 281-287). Cosmetics.cs: owned/equipped/TryBuy via SaveService.
- Editor/OneLineSetup.cs (~line 169) builds the catalog's skins. New skins MUST be added there too.
- CompletionSequence.cs (~1100 lines, open only what you need) plays the win animation; WinPanel waits for its Finished event. GameFeedback owns sounds/haptics. NodeGlow uses Light2D per node.
- Exclusive items come with the Celestial Pass (PremiumPanel), not coins.

## Effect layers (every skin defines all of these)
1. Core: the main line (shader-driven: gradients, UV scroll, noise, width variation).
2. Halo: soft outer glow, pulsing gently. If the project already uses Bloom, core colors go slightly HDR so they bloom; don't add post-processing without asking.
3. Energy flow: pulses of light that keep travelling along the WHOLE drawn path from the first star to the last, looping (like energy flowing through the constellation). Shader-based, cheap.
4. Finger effect: while dragging, a skin-specific effect at the finger (dust, comet head, sparks, a companion...) whose emission scales with finger speed.
5. Ambient life: placed segments stay alive: twinkles, drifting motes, shimmer; subtle, low particle count.
6. Star reach: when the line reaches a star: a burst + an expanding ring/ripple + the star flashes in skin colors. Short (under 0.6 s), satisfying.
7. Escalation: intensity grows with path progress (visited / total stars): brighter core, faster flow, more particles, richer bursts. The last star gets the biggest burst.
8. Win flourish: when the level is completed, the whole path lights up in the skin's style (e.g. a wave running start → end) BEFORE CompletionSequence takes over. Short (under 1 s), must not delay or break CompletionSequence / WinPanel.
9. Sound: a skin-specific connect sound through GameFeedback, pitch rising step by step with each connected star; the last star gets a resolving chime. Respect AudioService toggles. Light haptic on reach (existing haptics setting).

## Rarity tiers
Common / Rare / Epic / Legendary / Mythic. Each tier uses more layers at higher detail. Frame/label color per tier in the shop; Mythic gets an animated frame.

## Skins
- Default (free, upgraded): current colors and feel, but with all layers at a tasteful level: anti-aliased core, round caps, clean joins at stars, soft halo, gentle energy flow, gold dust at the finger. It's the base the others are measured against.
- Moonlight (Common): silver-blue core with a slow shimmer, pale motes drifting up, soft moon-halo ring on star reach.
- Stardust (Rare): gold-white core with dozens of tiny 4-point glints twinkling along the line (random phases, star-shaped flashes, some bigger "hero" glints); finger leaves falling glitter that sparkles as it falls; reach = glitter fountain.
- Prism (Epic): rainbow line. The core splits into 3 thin R/G/B lines that drift apart slightly while drawing and converge when placed (chromatic effect); hue flows continuously along the whole path; light-refraction flares (small lens-flare streaks) at each star join; rainbow dust keeps its birth hue.
- Comet (Epic): blazing comet head at the finger with a long tapering 2-layer tail (hot white core + blue plasma) that curves with finger motion; icy sparks shed from the tail; placed segments cool down from white to soft blue over ~1 s; reach = ice-crystal flash + ring.
- Element set (Epic each), tied to zodiac elements (Zodiac.cs); show the element's signs under the name in the shop:
  - Fire (Aries/Leo/Sagittarius): flame ribbon with animated noise (flickers, licks upward), embers rising on curl-noise paths, occasional sparks, ash motes fading to grey; reach = fire burst + heat ring.
  - Water (Cancer/Scorpio/Pisces): liquid ribbon with moving caustic highlights and a ripple running along it; droplets with light gravity; reach = splash crown + concentric water rings.
  - Air (Gemini/Libra/Aquarius): two thin ribbons spiraling around each other along the path; wind streaks and swirl curls peel off; reach = small whirlwind spiral.
  - Earth (Taurus/Virgo/Capricorn): after a segment is placed, small crystals grow along it (scale-in with overshoot), faceted with sweeping glints; reach = crystal shards burst and settle.
- Jupiter's Lightning (Legendary): jagged bolt between stars, regenerated every ~60-80 ms with small branching forks; random flicker; now and then a short arc jumps between two connected stars; reach = bright flash + electric ring; the Light2D of the reached star flickers.
- Aurora (Legendary): wide layered curtain (2-3 layers) in green → teal → purple with vertical light rays that shimmer and slowly undulate; soft glowing wisps; energy flow looks like light rippling through the curtain.
- Galaxy (Legendary): the line is a strip cut out of deep space: dark purple-blue with parallax-scrolling tiny stars (2 depths) and nebula colors inside, bright thin edges; tiny stars orbit the finger; reach = mini spiral galaxy that spins and fades.
- Phoenix (Mythic): fire-gold ribbon with feather-shaped particles peeling off; finger has a small flame bird glow; win flourish: a phoenix made of particles/procedural wing shapes rises from the last star, flaps once and dissolves into embers (short, before CompletionSequence).
- Void (Mythic): dark line with a purple-violet accretion shimmer along its edges; particles spiral INTO the line (inverse flow); a lensing ring warps around the finger (fake it with a ring shader, no screen grab unless cheap); reach = implosion then soft outward pulse.
- Pixie Companion (Mythic): a tiny glowing fairy follows the finger with a lagged, bobbing, wobbly flight path (spring motion), fluttering wing glints, leaving a sparkle trail; on star reach it circles the star once; while idle it hovers near the last star; on win it does a happy loop-the-loop.

## Technical rules
- Architecture: one effect component per skin type behind a common interface (e.g. ISkinEffect: OnSegment, OnDrag, OnReach, OnProgress, OnWin, Clear), chosen by the equipped skin. WandTrail becomes the host/router. Keep it clean so adding a skin = one class + catalog data.
- Finger position: add a read-only visual event to PathManager (e.g. `event Action<Vector3> Dragged` with the board-space point, raised from OnDrag and SimulateDrag). No gameplay logic changes.
- Performance (mid-range phones, stable 60 fps): no per-frame allocations; pool LineRenderers and particle systems; cap particles per skin; prefer shader tricks (UV scroll, noise textures generated procedurally at startup, vertex offset) over large particle counts. Add a quality level (High / Low) in the settings asset: Low halves particles and disables the most expensive layers; pick Low automatically on weak devices (e.g. SystemInfo checks).
- Shaders: URP 2D compatible, with a simple fallback (like StarSDF does). Procedural art only: no image files, no packages. Generated textures (noise, gradients) are created once and cached.
- Everything tilts with the board (under BoardPivot, local simulation space).
- Clear(), restart, retry, time-up, leaving to the map and hints must fully reset or hide effects: no leftover particles, sounds or segments. HintSystem and AutoPlayer keep working (AutoPlayer shows finger effects via SimulateDrag).
- CompletionSequence and WinPanel timing must not change except the short win flourish before it; if that needs a hook, add a visual-only one.
- All visual values in ScriptableObjects (per-skin settings blocks referenced from the catalog), no magic numbers.
- SaveService: backward-compatible keys; existing players keep owned/equipped skins; unknown equipped id falls back to Default.

## Prices
Suggest per-tier prices from the economy (time boosts 30 / 40 coins ≈ 3-4 levels; check CoinManager for coins per level). Rough targets: Common ≈ 10 levels, Rare ≈ 25, Epic ≈ 40, Legendary ≈ 70, Mythic ≈ 120 levels of coins. Ask me whether some Mythics should be Celestial Pass exclusives instead. Prices in the catalog asset, not code.

## Shop ("Wands" tab)
- Built in code with UIKit / UIParts like the rest of the UI.
- Each row: live animated mini-preview (a short looping path on a few stars), name, tier frame/label, price or Owned/Equipped.
- "Try" button: opens a larger preview where a demo path draws itself across ~6 stars with ALL layers (finger effect, reach bursts, escalation, win flourish, sound). This is the main selling point; make it look great.
- Not enough coins: clear message. Previews only run while the shop is open.

## Work in phases, ONE phase per session
This feature is split across several sessions to save context. Do only ONE phase per session.
- Phase 1: the effect architecture + all 9 layers on Default, the Dragged event, pooling, quality levels, the shop tab with previews and Try, + Stardust and Prism.
- Phase 2: Moonlight, Comet, the four element skins.
- Phase 3: Jupiter's Lightning, Aurora, Galaxy.
- Phase 4: Phoenix, Void, Pixie Companion.

### Start of every session
1. Read Docs/WandSkins_Progress.md if it exists. It says which phases are done, the architecture decisions, file list, prices and open issues. Trust it; don't re-explore finished work.
2. Run `git log --oneline -10` and `git status` to confirm the last phase was committed and the tree is clean. If not, tell me before doing anything.
3. Pick the first phase not marked done. If I named a phase in my message, do that one instead.

### During the phase
1. First give me a short plan in casual Turkish: files, how each effect is built (shader vs particles), prices (phase 1). Wait for my OK.
2. Code it following CLAUDE.md (read only what you need). Reuse the architecture from earlier phases; don't redesign it.
3. Check compile errors (Coplay check_compile_errors) and Unity logs (get_unity_logs).
4. Play Mode (Coplay play_game): equip each new skin, draw a full level, take screenshots mid-drag, right after a star reach, and during the win flourish. Test restart, hint, time-up, win. Check frame time if possible and report particle counts.
5. Critique your own screenshots: if a skin looks flat or simple, improve it before reporting.
6. Tell me in Turkish what was done, the exact Editor steps (e.g. re-run One Line/Setup Levels + Scene) and what you couldn't test.

### End of every session
1. Create or update Docs/WandSkins_Progress.md (English, short, max ~80 lines):
   - Phases: done / not done, one line each
   - Architecture: interface name, how skins are registered, where settings live
   - Files added/changed, one line each with purpose
   - Prices per tier, catalog ids of all skins
   - Known issues / TODO for the next phase
   - Editor steps the user must run
2. Ask me: "Phase X bitti, commit'leyeyim mi?" Only after my yes, commit the phase with a clear message (e.g. "Wand skins phase 1: effect system, Default, Stardust, Prism") including the progress file. Never push unless I ask.
3. Finish with the exact line I should paste to start the next session, e.g. "Continue the Wand skins feature: do the next phase (see Docs/WandSkins_Progress.md)."
