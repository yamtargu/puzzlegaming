# One-Line Path — Claude notes

Mobile 2D puzzle (portrait) with an astrology / zodiac night-sky theme. The player draws a **constellation**: one stroke through the graph that visits **every star exactly once** (Hamiltonian path, not Eulerian). Unity **6000.6.3f1**, URP 2D, uGUI + TextMeshPro, new Input System. Namespace `OneLine`. Single scene: `Assets/Scenes/SampleScene.unity`.

Reply to the user in casual Turkish. Prompts/code comments in English.

## Read less, not more
- This file is the map. **Don't read whole folders.** Open only the scripts the task touches; the list below says which one owns what.
- Every script starts with a `<summary>` doc comment. To orient, read just that (`head -30`), not the whole file.
- Big files, open only the part you need: `CompletionSequence.cs` (~1100 lines), `LevelMap.cs` (~800), `UIKit.cs` (~640), `VectorGraphics.cs` (~560), `ZodiacBackground.cs` (~480), `OneLineSetup.cs` (~460).
- Don't open `.asset` / `.unity` YAML unless a value there is the actual question. Never read `Library/`, `Temp/`, `Logs/` (except a quick `grep` in `Logs/Editor.log` for errors).
- Coplay MCP (`mcp__coplay-mcp__*`) can check compile errors, read Unity logs, play the game and capture screenshots. Prefer `check_compile_errors` / `get_unity_logs` over reading log files.

## Conventions
- UI is **built in code**, not in prefabs. Every visual value lives in a ScriptableObject, so no magic numbers: `UIStyle` (UI), `StarStyle` (board look/motion), `ZodiacTheme` (astrology look), `CompletionSettings` (win animation), `DepthSettings` (2.5D), `TimerSettings` (timed levels, boosts, prices, hourglass look). Assets are in `Assets/Settings/`.
- Art is procedural: `Art.cs` (sprites), `VectorGraphics.cs` (anti-aliased uGUI lines/discs), `UIKit.cs` (panels/buttons/text), `UIParts.cs` (the gold celestial frame + medallion). Reuse these, don't add image files or packages.
- Easing is in `Tween.cs`. Systems talk through C# events, e.g. gameplay raises them and visuals subscribe. Visual scripts never change gameplay state.
- All persistence goes through `SaveService` (PlayerPrefs wrapper; keep keys backward-compatible).
- Coins buy cosmetics and the two time boosts (Saturn's Gift, Lunar Stillness). The time boosts are the ONLY gameplay-affecting purchases; nothing else purchasable may change gameplay. Rewarded ads grant boosts through `Boosts.Grant(type, amount, BoostSource.RewardedAd)` (see `Ads`); IAP would use the same hook.
- No per-frame allocations. Pool particles/sprites. Respect `Screen.safeArea`. Reference resolution 1080×1920.

## Where things live (`Assets/Scripts/OneLine/`)
**Gameplay core**
- `PathManager` — builds the board from `LevelData`, pointer input → path, validation, win/fail. Owns `BoardPivot`. Events: `Completed`, `Failed`, `BoardBuilt`, `NodeVisited(Node,int,Color)`, `PathCleared`.
- `Node` — a star: neighbors, visited state, idle/visit motion.
- `LevelManager` — plays `LevelPack` in order, saves progress, advances. Event: `LevelLoaded(int)`.
- `LevelData` (nodes, edges, startNode, stored `solution`, `difficulty` in bits), `LevelPack` (ordered list).
- `LevelSolver` (solvability / solution count), `Difficulty` (curve: 10 levels per tier, every 5th HARD, every 10th BOSS; `WarmUpLevels`).
- `LevelData.timeLimit` (0 = untimed) + `manualTimeLimit` (hand-tuned, never overwritten).

**Timed levels** (HARD/BOSS levels from 15 on; 5 and 10 are untimed onboarding)
- `LevelTimer` — pure logic, no UI: per-LEVEL timer (retries don't refill; leaving to the map and back resumes it), starts on the first board touch (`PathManager.BoardTouched`), unscaled time, pauses via `AddPauseSource` (ScreenRouter registers screens/panels/house transition) and app background, time up → `PathManager.SetLocked`. Stars by time left (3 ≥50%, 2 ≥20%, else 1) → `LevelManager.LastStars` → WinPanel + SaveService. Also records local solve times (`SaveService.AverageSolveTime`). Events: `Started`, `Tick(remaining01)`, `TimeUp`, `Extended(s)`, `SlowStarted/Ended`, `Configured(timed)`, `PausedChanged`, `Won(stars)`.
- `TimerSettings` — formula (base + perNode·nodes + perBit·bits, ×1.2 HARD / ×1.4 BOSS, ceil 5 s, clamp 20–150), stars, boost rules/prices, all hourglass/boost/panel visuals.
- `Boosts` — static inventory (SaveService, start 2 each, +1 Saturn's Gift for a timed BOSS's first 3-star clear), `TryBuy`, `Grant`. Prices ≈ 3 / 4 levels of coins (30 / 40, packs of 5: 125 / 170).
- Views (subscribe only): `HourglassView` (+ `HourglassSand` mesh), bottom-left above the Restart button (safe-area bottom-left anchor + pivot; `HUD.HourglassLeft/Bottom` keep the flip on screen and clear of Restart, `BottomBlock` reserves the column from the board): waits with the sand below until the first touch (`LevelTimer.Started`), then flips (InOutBack, scale punch, gold burst); loops pause with the timer, `BoostBar` (two boost buttons under the coins, offer card with coin packs + a rewarded-ad button, Saturn spiral, Lunar moonlight overlay + sky `timeScale`), `TimeUpPanel` (coin/ad Saturn's Gift, Try again), `PausePanel` (the HUD's astrolabe menu button replaces Settings + Map on timed levels). HUD hides the board while a started timer is paused or time is up.
- `HintSystem` — ghost line showing the next solution steps. `AutoPlayer` — demo bot that solves levels.

**Board visuals**
- `WandTrail` (drawn line + dust; the equipped wand from `CosmeticCatalog.wands` sets the dust look/motion), `NodeGlow` (Light2D per node), `DepthFeedback` + `BoardView` (2.5D shadows/tilt), `BoardFader`, `ParallaxBackground`.
- `GameFeedback` — sounds, haptics, small shakes.
- `CompletionSequence` + `CompletionSettings`: level-complete "constellation fusion" (merge along the path, flare, sign rises with the level name). Events: `Arrived`, `Finished`. `WinPanel` waits for `Finished`.

**Theme / naming**
- `Zodiac` (12 houses, levels split evenly across them), `ZodiacTheme`, `ZodiacBackground` (wheel, constellation, planets, nebula), `ZodiacConstellations` (real star layouts), `ZodiacNames` (level titles per sign; BOSS gets iconic names), `LevelNames`, `ThemeSet` (per-tier palette), `HouseTransition` (entering a new sign).

**Screens & UI**
- `ScreenRouter`: Home ⇄ Map ⇄ Game + overlay panels. Event: `ScreenChanged`.
- `HomeScreen`, `LevelMap` ("Zodiac Path", virtualized), `HUD` (title cartouche, buttons, top/bottom reserve → PathManager), `ProgressDots`, `WinPanel`, `SettingsPanel`, `ShopUI`, `PremiumPanel`.

**Economy / services**
- `CoinManager` (balance, `BalanceChanged`), `CosmeticCatalog` + `Cosmetics` (line skins, backgrounds, wands = trail particle effects; looks only), `Purchases` (`PurchaseService.Current`, currently a mock store; real Unity IAP later), `Ads` (`IAdService` / `AdService.Current`, currently a mock; `RewardedAds`: free boost per ad, daily cap in TimerSettings), `AudioService` (music/SFX toggles), `AppConfig`.

**Editor** (`Editor/`, menu **One Line/…**)
- `Setup Levels + Scene` (OneLineSetup, safe to re-run; `SetupTimer` wires the timer parts), `Generate 500 Levels` (LevelGenerator, deterministic; recalculates time limits after), `Validate All Levels` (LevelValidation, build fails on unsolvable levels), `Recalculate Time Limits` (TimeLimits: fills `LevelData.timeLimit`, skips manual ones, prints the table with local avg solve times + the design's expected values), `Build UI Fonts` (FontSetup: Cinzel = titles, Quicksand = body, with Turkish glyphs), `Play Plus Test Level Only`.

## Content & assets
- Levels: `Assets/Levels/Generated/` (currently 150 `Level_NNN.asset`), `Assets/Levels/Handmade/` (warm-up levels), `LevelPack.asset`, `CosmeticCatalog.asset`, `ThemeSet.asset`.
- Fonts: `Assets/Fonts/` (Cinzel, Quicksand, OFL). Music: `Assets/Audio/OneLineTheme.mp3`.

## Not built yet (planned)
- Real ad network: a Unity LevelPlay `IAdService` assigned to `AdService.Current` (rewarded ads already work against the mock). Interstitials aren't built (the Celestial Pass would turn them off).
- Real IAP (boost packs could grant via `BoostSource.Iap`).
- Time-limit tuning from real data: `Recalculate Time Limits` prints local average solve times; no remote analytics yet.
- Par-time coin bonus (`CoinManager.ParTimeBonus`) is still unused — callers pass `beatParTime: false`.

## After changing code
1. Check compile errors (Coplay `check_compile_errors`).
2. If levels or the generator changed, run **One Line/Validate All Levels**.
3. Tell the user the exact Editor steps, if any, then verify in Play Mode (Coplay `play_game` + screenshot) when it's visual.
