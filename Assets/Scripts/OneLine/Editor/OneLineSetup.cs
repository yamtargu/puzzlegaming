using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OneLine.EditorTools
{
    /// <summary>Creates the theme set, the LevelPack and the scene objects. Safe to run again.</summary>
    public static class OneLineSetup
    {
        const string LevelFolder = "Assets/Levels";
        const string PackPath = LevelFolder + "/LevelPack.asset";
        const string ThemePath = LevelFolder + "/ThemeSet.asset";
        const string PlusPath = LevelFolder + "/TestLevel_Plus.asset";

        // One name per tier of 10 levels, calm -> intense. Shown in the HUD.
        static readonly string[] TierNames =
        {
            "Dawn", "Breeze", "Ripple", "Meadow", "Drift", "Bloom", "Tide", "Lantern", "Glimmer", "Current",
            "Spark", "Ember", "Tangle", "Mirage", "Hollow", "Thicket", "Cascade", "Prism", "Undertow", "Labyrinth",
            "Static", "Quicksilver", "Riddle", "Nightfall", "Vortex", "Wildfire", "Eclipse", "Monsoon", "Obsidian", "Tempest",
            "Maelstrom", "Paradox", "Inferno", "Abyss", "Avalanche", "Cyclone", "Phantom", "Enigma", "Chimera", "Leviathan",
            "Nebula", "Pulsar", "Quasar", "Supernova", "Void", "Oblivion", "Infinity", "Zenith", "Apex", "Singularity",
        };

        // Each tier turns the accent hue this far around the color wheel, so neighbouring tiers look
        // clearly different: teal, blue, indigo, violet, magenta, rose, orange, yellow, lime, green, teal...
        const float StartHue = 170f, HueStep = 36f;

        [MenuItem("One Line/Setup Levels + Scene")]
        public static void SetupAll()
        {
            CreateThemeSet();
            CreateCosmeticCatalog();
            CreateDepthSettings();
            CreateStarStyle();
            CreateUIStyle();
            var pack = BuildPackFromGenerated();
            SetupScene(pack);
        }

        const string UIStylePath = "Assets/Settings/UIStyle.asset";
        const string LevelNamesPath = "Assets/Settings/LevelNames.asset";

        /// <summary>UI look + constellation names. Created once; your Inspector edits are kept.</summary>
        public static UIStyle CreateUIStyle()
        {
            var names = AssetDatabase.LoadAssetAtPath<LevelNames>(LevelNamesPath);
            if (!names)
            {
                names = ScriptableObject.CreateInstance<LevelNames>();
                AssetDatabase.CreateAsset(names, LevelNamesPath);
            }
            var style = AssetDatabase.LoadAssetAtPath<UIStyle>(UIStylePath);
            if (!style)
            {
                style = ScriptableObject.CreateInstance<UIStyle>();
                AssetDatabase.CreateAsset(style, UIStylePath);
            }
            if (!style.starStyle) style.starStyle = CreateStarStyle();
            if (!style.levelNames) style.levelNames = names;
            if (!style.zodiac) style.zodiac = CreateZodiacTheme();
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            return style;
        }

        const string ZodiacThemePath = "Assets/Settings/ZodiacTheme.asset";
        const string ZodiacNamesPath = "Assets/Settings/ZodiacNames.asset";
        const string ZodiacFiguresPath = "Assets/Settings/ZodiacConstellations.asset";

        /// <summary>Astrology theme + names + constellation figures. Created once; your Inspector edits are kept.</summary>
        public static ZodiacTheme CreateZodiacTheme()
        {
            T Load<T>(string path) where T : ScriptableObject
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset) return asset;
                if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            var theme = Load<ZodiacTheme>(ZodiacThemePath);
            if (!theme.names) theme.names = Load<ZodiacNames>(ZodiacNamesPath);
            if (!theme.constellations) theme.constellations = Load<ZodiacConstellations>(ZodiacFiguresPath);
            if (!theme.backgroundMaterial)
            {
                const string path = "Assets/Settings/ZodiacBackground.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                theme.backgroundMaterial = mat;
            }
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            return theme;
        }

        const string AppConfigPath = "Assets/Settings/AppConfig.asset";

        /// <summary>Celestial Pass offer + About texts. Created once; your Inspector edits are kept.</summary>
        public static AppConfig CreateAppConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AppConfig>(AppConfigPath);
            if (config) return config;
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            config = ScriptableObject.CreateInstance<AppConfig>();
            AssetDatabase.CreateAsset(config, AppConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        const string CompletionSettingsPath = "Assets/Settings/CompletionSettings.asset";

        /// <summary>Timings and sizes of the level-complete fusion. Created once; Inspector edits are kept.</summary>
        public static CompletionSettings CreateCompletionSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CompletionSettings>(CompletionSettingsPath);
            if (settings) return settings;
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            settings = ScriptableObject.CreateInstance<CompletionSettings>();
            AssetDatabase.CreateAsset(settings, CompletionSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        const string CatalogPath = LevelFolder + "/CosmeticCatalog.asset";
        const string DepthSettingsPath = "Assets/Settings/DepthSettings.asset";
        const string StarStylePath = "Assets/Settings/StarStyle.asset";

        /// <summary>Space/star look (palette + motion). Created once; your Inspector edits are kept.</summary>
        public static StarStyle CreateStarStyle()
        {
            var style = AssetDatabase.LoadAssetAtPath<StarStyle>(StarStylePath);
            if (style) return style;
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            style = ScriptableObject.CreateInstance<StarStyle>();
            AssetDatabase.CreateAsset(style, StarStylePath);
            AssetDatabase.SaveAssets();
            return style;
        }

        /// <summary>2.5D tunables (tilt, shadows, pop, parallax). Created once; your Inspector edits are kept.</summary>
        public static DepthSettings CreateDepthSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DepthSettings>(DepthSettingsPath);
            if (settings) return settings;
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            settings = ScriptableObject.CreateInstance<DepthSettings>();
            AssetDatabase.CreateAsset(settings, DepthSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>Default shop items. Each list starts with a free "tier colors" item (the default).</summary>
        public static CosmeticCatalog CreateCosmeticCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            CosmeticCatalog.LineSkin Skin(string id, string name, int price, Color line, Color node) =>
                new() { id = id, displayName = name, price = price, line = line, node = node };
            CosmeticCatalog.Background Back(string id, string name, int price, Color bg, Color edge) =>
                new() { id = id, displayName = name, price = price, background = bg, edge = edge };

            catalog.lineSkins = new()
            {
                new CosmeticCatalog.LineSkin { id = "skin.tier", displayName = "Starlight", price = 0, useTierColors = true },
                Skin("skin.snow", "Snow", 40, new Color(0.95f, 0.96f, 0.98f), new Color(0.55f, 0.58f, 0.65f)),
                Skin("skin.coral", "Coral", 60, new Color(1f, 0.42f, 0.35f), new Color(0.55f, 0.36f, 0.34f)),
                Skin("skin.lime", "Lime", 80, new Color(0.65f, 1f, 0.30f), new Color(0.40f, 0.50f, 0.30f)),
                Skin("skin.violet", "Violet", 100, new Color(0.72f, 0.50f, 1f), new Color(0.45f, 0.38f, 0.60f)),
                Skin("skin.gold", "Gold", 150, new Color(1f, 0.80f, 0.25f), new Color(0.60f, 0.50f, 0.30f)),
                // Exclusive: comes with the Celestial Pass, can't be bought with coins.
                new CosmeticCatalog.LineSkin
                {
                    id = "skin.celestial", displayName = "Celestial Gold", price = 0, exclusive = true,
                    line = new Color(0.96f, 0.84f, 0.55f), node = new Color(0.62f, 0.53f, 0.36f),
                },
            };
            catalog.backgrounds = new()
            {
                new CosmeticCatalog.Background { id = "bg.tier", displayName = "Deep space", price = 0, useTierColors = true },
                Back("bg.midnight", "Midnight", 50, new Color(0.04f, 0.05f, 0.09f), new Color(0.20f, 0.22f, 0.30f)),
                Back("bg.forest", "Forest", 80, new Color(0.05f, 0.12f, 0.08f), new Color(0.20f, 0.30f, 0.24f)),
                Back("bg.plum", "Plum", 100, new Color(0.13f, 0.06f, 0.14f), new Color(0.32f, 0.22f, 0.34f)),
                Back("bg.ember", "Ember", 120, new Color(0.16f, 0.07f, 0.05f), new Color(0.36f, 0.22f, 0.18f)),
            };
            catalog.wands = BuildWands();
            EditorUtility.SetDirty(catalog);
            AssignBoardShaders();
            AssetDatabase.SaveAssets();
            return catalog;
        }

        const string WandFolder = "Assets/Settings/Wands";

        /// <summary>
        /// The wands. Ids stay stable so owned / equipped wands survive: wand.stardust was the free default (now Classic),
        /// wand.twinkle is now Stardust, wand.moonbeam Moonlight, wand.comet Comet. Ember keeps its old dust until Fire.
        /// </summary>
        static System.Collections.Generic.List<CosmeticCatalog.Wand> BuildWands()
        {
            CosmeticCatalog.Wand W(string id, string name, int price, WandRarity rarity, WandEffect effect, WandLook look) =>
                new() { id = id, displayName = name, price = price, rarity = rarity, effect = effect, look = look };

            return new()
            {
                W("wand.stardust", "Classic", 0, WandRarity.Common, WandEffect.Classic, Look("Classic", l => { })),
                W("wand.moonbeam", "Moonlight", 120, WandRarity.Common, WandEffect.Moonlight, Look("Moonlight", l =>
                {
                    l.pathColorMix = 0.08f;
                    l.lineColor = new Color(0.82f, 0.88f, 1f);
                    l.glowColor = new Color(0.62f, 0.72f, 1f);
                    l.coreWidth = 0.045f;
                    l.glowWidth = 0.2f;
                    l.glowAlpha = 0.22f;
                    l.glowOpacity = 0.3f;
                    l.coreHighlight = 0.3f;
                    l.shimmerAmount = 0.7f;
                    l.shimmerWavelength = 1.8f;
                    l.shimmerSpeed = 0.45f;
                    l.shimmerSharpness = 8f;
                    l.dustColors = new[] { new Color(0.86f, 0.9f, 1f), new Color(0.74f, 0.82f, 1f) };
                    l.dustPerUnit = 4f;
                    l.dustIdleRate = 0.3f;
                    l.dustMaxPerFrame = 3;
                    l.dustMaxParticles = 80;
                    l.dustSize = new Vector2(0.035f, 0.07f);
                    l.dustLifetime = new Vector2(1.4f, 2.2f);
                    l.dustScatter = 0.05f;
                    l.dustDrift = new Vector2(0f, 0.16f);
                    l.burstShape = DustShape.Star;
                    l.burstCount = new Vector2Int(8, 10);
                    l.burstSpeed = 0.6f;
                    l.burstLifetime = 0.45f;
                    l.burstSize = new Vector2(0.04f, 0.08f);
                    l.burstColors = new[] { new Color(0.85f, 0.9f, 1f) };
                    l.burstLineColorMix = 0f;
                })),
                W("wand.twinkle", "Stardust", 300, WandRarity.Rare, WandEffect.Stardust, Look("Stardust", l =>
                {
                    l.pathColorMix = 0.12f;
                    l.lineColor = new Color(1f, 0.94f, 0.78f);
                    l.glowColor = new Color(1f, 0.82f, 0.45f);
                    l.coreWidth = 0.05f;
                    l.glowWidth = 0.34f;
                    l.glowAlpha = 0.32f;
                    l.glowOpacity = 0.35f;
                    l.coreHighlight = 0.55f;
                    l.sparkleSpacing = 0.16f;
                    l.sparkleSize = 0.1f;
                    l.sparkleRate = 0.55f;
                    l.sparkleIntensity = 1.5f;
                    l.dustShape = DustShape.Sparkle;
                    l.dustColors = new[] { new Color(1f, 0.92f, 0.65f), new Color(1f, 1f, 0.92f), new Color(1f, 0.8f, 0.45f) };
                    l.dustPerUnit = 14f;
                    l.dustIdleRate = 0.8f;
                    l.dustMaxPerFrame = 6;
                    l.dustMaxParticles = 200;
                    l.dustSize = new Vector2(0.08f, 0.15f);
                    l.dustLifetime = new Vector2(0.7f, 1.2f);
                    l.dustScatter = 0.15f;
                    l.dustDrift = new Vector2(0f, -0.12f);
                    l.dustGravity = 0.25f;
                    l.dustTwinkle = true;
                    l.burstCount = new Vector2Int(14, 18);
                    l.burstSpeed = 1.3f;
                    l.burstLifetime = 0.45f;
                    l.burstSize = new Vector2(0.08f, 0.15f);
                    l.burstColors = new[] { Color.white, new Color(1f, 0.85f, 0.5f) };
                    l.burstLineColorMix = 0.1f;
                })),
                W("wand.rainbow", "Rainbow", 300, WandRarity.Rare, WandEffect.Rainbow, Look("Rainbow", l =>
                {
                    l.pathColorMix = 0f;
                    l.coreWidth = 0.058f;
                    l.glowWidth = 0.34f;
                    l.glowAlpha = 0.34f;
                    l.glowOpacity = 0.45f;
                    l.coreHighlight = 0.4f;
                    l.hueCyclesPerUnit = 0.12f;
                    l.hueSpeed = 0.06f;
                    l.hueSaturation = 0.42f;
                    l.dustPerUnit = 12f;
                    l.dustSize = new Vector2(0.05f, 0.1f);
                    l.dustLifetime = new Vector2(0.7f, 1.2f);
                    l.dustDrift = new Vector2(0f, 0.06f);
                    l.burstCount = new Vector2Int(12, 16);
                    l.burstSpeed = 1.2f;
                    l.burstLineColorMix = 0f;
                })),
                W("wand.comet", "Comet", 300, WandRarity.Rare, WandEffect.Comet, Look("Comet", l =>
                {
                    l.pathColorMix = 0.1f;
                    l.lineColor = new Color(0.82f, 0.92f, 1f);
                    l.glowColor = new Color(0.55f, 0.75f, 1f);
                    l.coreWidth = 0.05f;
                    l.coreSoftness = 0.85f;
                    l.glowWidth = 0.38f;
                    l.glowAlpha = 0.42f;
                    l.glowOpacity = 0.3f;
                    l.coreHighlight = 0.2f;
                    l.cometHeadColor = new Color(0.92f, 0.97f, 1f);
                    l.cometTailColor = new Color(0.55f, 0.78f, 1f);
                    l.dustColors = new[] { new Color(0.8f, 0.92f, 1f), Color.white };
                    l.dustPerUnit = 6f;
                    l.dustSize = new Vector2(0.03f, 0.06f);
                    l.dustLifetime = new Vector2(0.3f, 0.6f);
                    l.dustScatter = 0.35f;
                    l.dustDrift = Vector2.zero;
                    l.dustTrailBack = 0.8f;
                    l.burstCount = new Vector2Int(8, 12);
                    l.burstSpeed = 1.6f;
                    l.burstLifetime = 0.25f;
                    l.burstSize = new Vector2(0.04f, 0.08f);
                    l.burstColors = new[] { new Color(0.85f, 0.95f, 1f), Color.white };
                    l.burstLineColorMix = 0f;
                    l.burstFlashSize = 0.55f;
                    l.burstFlashColor = new Color(0.8f, 0.92f, 1f);
                    l.burstFlashTime = 0.18f;
                })),
                W("wand.goldink", "Gold Ink", 300, WandRarity.Rare, WandEffect.GoldInk, Look("Gold Ink", l =>
                {
                    l.pathColorMix = 0f;
                    l.lineColor = new Color(1f, 0.78f, 0.36f);
                    l.glowColor = new Color(1f, 0.68f, 0.28f);
                    l.coreWidth = 0.06f;
                    l.coreSoftness = 0.15f;
                    l.glowWidth = 0.26f;
                    l.glowAlpha = 0.18f;
                    l.glowOpacity = 0.4f;
                    l.coreHighlight = 0.15f;
                    l.metallic = 0.85f;
                    l.shimmerAmount = 0.55f;
                    l.shimmerWavelength = 2.2f;
                    l.shimmerSpeed = 0.8f;
                    l.shimmerSharpness = 14f;
                    l.dustShape = DustShape.Flake;
                    l.dustColors = new[] { new Color(1f, 0.82f, 0.4f), new Color(1f, 0.9f, 0.6f), new Color(0.9f, 0.66f, 0.28f) };
                    l.dustPerUnit = 6f;
                    l.dustIdleRate = 0.4f;
                    l.dustMaxPerFrame = 4;
                    l.dustMaxParticles = 120;
                    l.dustSize = new Vector2(0.06f, 0.11f);
                    l.dustLifetime = new Vector2(1.2f, 1.8f);
                    l.dustScatter = 0.12f;
                    l.dustDrift = new Vector2(0f, -0.05f);
                    l.dustGravity = 0.12f;
                    l.dustSpin = new Vector2(60f, 200f);
                    l.dustFlip = 1.6f;
                    l.dustFlutter = 0.25f;
                    l.burstCount = new Vector2Int(10, 14);
                    l.burstSpeed = 1f;
                    l.burstColors = new[] { new Color(1f, 0.85f, 0.45f), new Color(1f, 0.95f, 0.75f) };
                    l.burstLineColorMix = 0f;
                })),
                // Old dust-only wand, until Fire (phase 3) replaces it.
                W("wand.ember", "Ember", 110, WandRarity.Rare, WandEffect.Classic, Look("Ember", l =>
                {
                    l.dustColors = new[] { new Color(1f, 0.55f, 0.2f), new Color(1f, 0.75f, 0.35f) };
                    l.dustLineColorMix = 0.3f;
                    l.dustPerUnit = 9f;
                    l.dustSize = new Vector2(0.02f, 0.05f);
                    l.dustLifetime = new Vector2(0.8f, 1.4f);
                    l.dustScatter = 0.15f;
                    l.dustDrift = new Vector2(0f, 0.35f);
                })),
            };
        }

        // Creates a wand look with these values the first time; later runs keep whatever was tuned in the inspector.
        static WandLook Look(string name, System.Action<WandLook> preset)
        {
            string path = $"{WandFolder}/{name}.asset";
            var look = AssetDatabase.LoadAssetAtPath<WandLook>(path);
            if (look) return look;
            if (!AssetDatabase.IsValidFolder(WandFolder)) AssetDatabase.CreateFolder("Assets/Settings", "Wands");
            look = ScriptableObject.CreateInstance<WandLook>();
            preset(look);
            AssetDatabase.CreateAsset(look, path);
            return look;
        }

        // The star and wand-line shaders, referenced from StarStyle so builds include them.
        static void AssignBoardShaders()
        {
            var stars = AssetDatabase.LoadAssetAtPath<StarStyle>(StarStylePath);
            if (!stars) return;
            if (!stars.starShader) stars.starShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/StarSDF.shader");
            if (!stars.wandShader) stars.wandShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/WandLine.shader");
            EditorUtility.SetDirty(stars);
        }

        [MenuItem("One Line/Play Plus Test Level Only")]
        public static void UsePlusTestLevel()
        {
            var level = CreatePlusLevel();
            var manager = Object.FindAnyObjectByType<LevelManager>();
            if (!manager)
            {
                Debug.LogWarning("No LevelManager in the scene — run 'One Line/Setup Levels + Scene' first.");
                return;
            }
            manager.testLevel = level;
            SaveScene(manager.gameObject);
        }

        public static ThemeSet CreateThemeSet()
        {
            var set = AssetDatabase.LoadAssetAtPath<ThemeSet>(ThemePath);
            if (!set)
            {
                set = ScriptableObject.CreateInstance<ThemeSet>();
                AssetDatabase.CreateAsset(set, ThemePath);
            }
            set.tiers.Clear();
            for (int t = 0; t < TierNames.Length; t++)
            {
                float u = t / (TierNames.Length - 1f); // 0 = calm, 1 = intense: darker, more saturated
                float hue = Mathf.Repeat(StartHue + HueStep * t, 360f) / 360f;
                set.tiers.Add(new ThemeSet.Tier
                {
                    name = TierNames[t],
                    background = Color.HSVToRGB(hue, 0.25f + 0.35f * u, 0.16f - 0.08f * u),
                    edge = Color.HSVToRGB(hue, 0.20f + 0.20f * u, 0.30f - 0.06f * u),
                    node = Color.HSVToRGB(hue, 0.15f + 0.20f * u, 0.50f),
                    path = Color.HSVToRGB(hue, 0.55f + 0.30f * u, 0.95f),
                });
            }
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            return set;
        }

        /// <summary>Fills the LevelPack with Generated/Level_001, 002, ... in order.</summary>
        public static LevelPack BuildPackFromGenerated()
        {
            var pack = AssetDatabase.LoadAssetAtPath<LevelPack>(PackPath);
            if (!pack)
            {
                pack = ScriptableObject.CreateInstance<LevelPack>();
                AssetDatabase.CreateAsset(pack, PackPath);
            }
            pack.levels.Clear();
            for (int number = 1; ; number++)
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(LevelGenerator.AssetPath(number));
                if (!level) break;
                pack.levels.Add(level);
            }
            if (pack.Count == 0)
                Debug.LogWarning("No generated levels found — run 'One Line/Generate 500 Levels'.");
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            return pack;
        }

        public static void SetupScene(LevelPack pack)
        {
            var go = GameObject.Find("OneLineGame");
            if (!go) go = new GameObject("OneLineGame");
            if (!go.TryGetComponent(out GameFeedback feedback)) feedback = go.AddComponent<GameFeedback>();
            if (!go.TryGetComponent(out PathManager pathManager)) pathManager = go.AddComponent<PathManager>();
            if (!go.TryGetComponent(out LevelManager levelManager)) levelManager = go.AddComponent<LevelManager>();
            if (!go.TryGetComponent(out HUD hud)) hud = go.AddComponent<HUD>();
            if (!go.TryGetComponent(out ShopUI shop)) shop = go.AddComponent<ShopUI>();
            if (!go.TryGetComponent(out BoardView boardView)) boardView = go.AddComponent<BoardView>();
            if (!go.TryGetComponent(out DepthFeedback depth)) depth = go.AddComponent<DepthFeedback>();
            if (!go.TryGetComponent(out ParallaxBackground parallax)) parallax = go.AddComponent<ParallaxBackground>();
            if (!go.TryGetComponent(out NodeGlow glow)) glow = go.AddComponent<NodeGlow>();
            if (!go.TryGetComponent(out WandTrail trail)) trail = go.AddComponent<WandTrail>();
            if (!go.TryGetComponent(out ProgressDots dots)) dots = go.AddComponent<ProgressDots>();
            var depthSettings = AssetDatabase.LoadAssetAtPath<DepthSettings>(DepthSettingsPath);
            var starStyle = AssetDatabase.LoadAssetAtPath<StarStyle>(StarStylePath);
            boardView.pathManager = depth.pathManager = parallax.pathManager = glow.pathManager = pathManager;
            trail.pathManager = dots.pathManager = feedback.pathManager = pathManager;
            boardView.settings = depth.settings = parallax.settings = glow.settings = depthSettings;
            pathManager.style = trail.style = feedback.style = parallax.style = starStyle;
            parallax.boardView = boardView;
            var catalog = AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CatalogPath);
            trail.cosmetics = catalog;
            pathManager.feedback = feedback;
            levelManager.pathManager = pathManager;
            levelManager.levelPack = pack;
            levelManager.themeSet = AssetDatabase.LoadAssetAtPath<ThemeSet>(ThemePath);
            levelManager.cosmetics = catalog;
            levelManager.testLevel = null;
            hud.levelManager = levelManager;
            shop.catalog = catalog;

            // Night-sky UI: HUD, shop, settings, win screen, hints — all styled by UIStyle.
            var uiStyle = CreateUIStyle();
            if (!go.TryGetComponent(out SettingsPanel settings)) settings = go.AddComponent<SettingsPanel>();
            if (!go.TryGetComponent(out WinPanel win)) win = go.AddComponent<WinPanel>();
            if (!go.TryGetComponent(out HintSystem hints)) hints = go.AddComponent<HintSystem>();
            hud.style = shop.style = settings.style = win.style = dots.style = uiStyle;
            hud.shop = shop;
            hud.settings = settings;
            hud.hints = hints;
            dots.hud = hud;
            dots.levelManager = win.levelManager = levelManager;
            settings.pathManager = hints.pathManager = pathManager;
            hints.style = starStyle;
            levelManager.autoAdvance = false; // the win screen's Next button advances

            // Astrology theme: zodiac sky behind the board, house transition between signs.
            if (!go.TryGetComponent(out ZodiacBackground zodiacSky)) zodiacSky = go.AddComponent<ZodiacBackground>();
            if (!go.TryGetComponent(out HouseTransition houseTransition)) houseTransition = go.AddComponent<HouseTransition>();
            zodiacSky.theme = uiStyle.zodiac;
            zodiacSky.levelManager = levelManager;
            zodiacSky.boardView = boardView;
            zodiacSky.depth = depthSettings;
            houseTransition.style = uiStyle;
            hud.transition = houseTransition;

            // Screens: Home ⇄ Map ⇄ Game (router), home screen, level map, Celestial Pass, audio channels, board fade.
            var appConfig = CreateAppConfig();
            if (!go.TryGetComponent(out AudioService audio)) audio = go.AddComponent<AudioService>();
            if (!go.TryGetComponent(out BoardFader fader)) fader = go.AddComponent<BoardFader>();
            if (!go.TryGetComponent(out PremiumPanel premium)) premium = go.AddComponent<PremiumPanel>();
            if (!go.TryGetComponent(out HomeScreen home)) home = go.AddComponent<HomeScreen>();
            if (!go.TryGetComponent(out LevelMap map)) map = go.AddComponent<LevelMap>();
            if (!go.TryGetComponent(out ScreenRouter router)) router = go.AddComponent<ScreenRouter>();
            fader.pathManager = pathManager;
            fader.style = uiStyle;
            premium.style = uiStyle;
            premium.config = appConfig;
            premium.catalog = catalog;
            settings.config = appConfig;
            settings.catalog = catalog;
            settings.router = router;
            shop.premium = premium;
            home.style = uiStyle;
            home.levelManager = levelManager;
            home.router = router;
            home.shop = shop;
            home.settings = settings;
            home.premium = premium;
            map.style = uiStyle;
            map.levelManager = levelManager;
            map.router = router;
            map.shop = shop;
            hud.router = router;
            win.router = router;
            router.map = map;
            audio.router = router;

            // Level complete: the constellation fuses into one star (the win panel waits for it).
            if (!go.TryGetComponent(out CompletionSequence completion)) completion = go.AddComponent<CompletionSequence>();
            completion.pathManager = pathManager;
            completion.levelManager = levelManager;
            completion.settings = CreateCompletionSettings();
            completion.style = starStyle;
            completion.theme = uiStyle.zodiac;
            completion.feedback = feedback;
            completion.trail = trail;
            completion.ui = uiStyle;
            feedback.sequence = completion;
            hud.completion = completion;
            win.completion = completion;
            if (!audio.music) audio.music = AssetDatabase.LoadAssetAtPath<AudioClip>(ThemeMusicPath);
            if (!audio.gameMusic) audio.gameMusic = AssetDatabase.LoadAssetAtPath<AudioClip>(GameMusicPath);
            if (!feedback.starCollectClip) feedback.starCollectClip = AssetDatabase.LoadAssetAtPath<AudioClip>(StarCollectPath);
            router.style = uiStyle;
            router.levelManager = levelManager;
            router.home = home;
            router.hud = hud;
            router.board = fader;
            router.sky = zodiacSky;
            router.parallax = parallax;
            router.win = win;
            router.houseTransition = houseTransition;
            router.settings = settings;
            router.shop = shop;
            router.premium = premium;
            levelManager.loadOnStart = false; // the router opens levels (Play)
            SetupTimer(go);

            var cam = Camera.main;
            if (cam)
            {
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = pathManager.background;
            }
            SetupGlowVolume(cam);
            SaveScene(go);
        }

        /// <summary>
        /// Timed levels: LevelTimer (logic), the pause and time-up panels, and their links (HUD builds the hourglass
        /// and the boosts itself). Safe to run again; also callable on its own for an existing scene.
        /// </summary>
        public static void SetupTimer(GameObject go)
        {
            var settings = TimeLimits.LoadOrCreateSettings();
            if (!go.TryGetComponent(out LevelTimer timer)) timer = go.AddComponent<LevelTimer>();
            if (!go.TryGetComponent(out PausePanel pause)) pause = go.AddComponent<PausePanel>();
            if (!go.TryGetComponent(out TimeUpPanel timeUp)) timeUp = go.AddComponent<TimeUpPanel>();
            var levelManager = go.GetComponent<LevelManager>();
            var hud = go.GetComponent<HUD>();
            var router = go.GetComponent<ScreenRouter>();
            var shop = go.GetComponent<ShopUI>();
            timer.settings = settings;
            timer.levelManager = levelManager;
            if (levelManager) levelManager.timer = timer;
            pause.style = timeUp.style = hud ? hud.style : CreateUIStyle();
            pause.timer = timeUp.timer = timer;
            pause.router = router;
            pause.settings = go.GetComponent<SettingsPanel>();
            if (hud)
            {
                hud.timer = timer;
                hud.pause = pause;
            }
            if (router)
            {
                router.timer = timer;
                router.pause = pause;
                router.timeUp = timeUp;
            }
            if (shop) shop.timerSettings = settings;
            EditorUtility.SetDirty(go);
        }

        // Home / map background music (its import settings: streamed, Vorbis).
        const string ThemeMusicPath = "Assets/Audio/OneLineTheme.mp3";
        const string GameMusicPath = "Assets/Audio/InGameMusic.mp3";
        const string StarCollectPath = "Assets/Audio/StarCollect.mp3";

        const string GlowProfilePath = "Assets/Settings/GlowVolumeProfile.asset";

        /// <summary>
        /// Global Bloom so bright things (the drawn path, lit nodes) glow. Turns on post-processing for the camera.
        /// The profile is created once; Inspector edits to it are kept.
        /// </summary>
        public static void SetupGlowVolume(Camera cam)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlowProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, GlowProfilePath);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.85f);
                bloom.intensity.Override(1.1f);
                bloom.scatter.Override(0.6f);
                AssetDatabase.AddObjectToAsset(bloom, profile); // volume components must live inside the profile asset
                AssetDatabase.SaveAssets();
            }

            var go = GameObject.Find("Glow Volume");
            if (!go) go = new GameObject("Glow Volume");
            if (!go.TryGetComponent(out Volume volume)) volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            if (cam) cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        }

        // ---------- helpers ----------

        // Plus shape with the tips joined into a diamond. A bare plus has no solution:
        // each tip is only reachable through the center, which can be passed once.
        static LevelData CreatePlusLevel()
        {
            if (!AssetDatabase.IsValidFolder(LevelFolder)) AssetDatabase.CreateFolder("Assets", "Levels");
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(PlusPath);
            if (!level)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, PlusPath);
            }
            //          1
            //        / | \
            //      4 - 0 - 2
            //        \ | /
            //          3
            level.nodes = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(0, -1), new Vector2(-1, 0) };
            level.edges = new[]
            {
                new LevelData.Edge(0, 1), new LevelData.Edge(0, 2), new LevelData.Edge(0, 3), new LevelData.Edge(0, 4),
                new LevelData.Edge(1, 2), new LevelData.Edge(2, 3), new LevelData.Edge(3, 4), new LevelData.Edge(4, 1),
            };
            level.startNode = -1;
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            return level;
        }

        static void SaveScene(GameObject go)
        {
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorSceneManager.SaveScene(go.scene);
        }
    }
}
