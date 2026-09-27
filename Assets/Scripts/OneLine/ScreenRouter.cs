using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OneLine
{
    public enum AppScreen { Home, Map, Game }

    /// <summary>
    /// The screens of the single scene — Home ⇄ Map ⇄ Game — plus the overlay panels (Settings, Shop, Premium) on top.
    /// Screens crossfade with a slight scale while the sky stays put (it only drifts and zooms), so it feels like one
    /// world. The app starts on Home; Play opens the first unfinished level. Back (Android back button, or a swipe in
    /// from the left edge) closes the top panel (or the map's level card), else goes Game → Map → Home.
    /// It is also the LevelTimer's pause source: the timer stops while the game screen isn't showing, any panel is
    /// open or a house transition plays.
    /// </summary>
    public class ScreenRouter : MonoBehaviour
    {
        public UIStyle style;
        public LevelManager levelManager;
        public HomeScreen home;
        public LevelMap map;
        public HUD hud;
        public BoardFader board;
        public ZodiacBackground sky;
        public ParallaxBackground parallax;
        public WinPanel win;
        public HouseTransition houseTransition;
        public SettingsPanel settings;
        public ShopUI shop;
        public PremiumPanel premium;
        [Header("Timed levels")]
        public LevelTimer timer;
        public PausePanel pause;
        public TimeUpPanel timeUp;

        public AppScreen Current { get; private set; } = AppScreen.Home;
        public event Action<AppScreen> ScreenChanged;

        Tween homeFade, mapFade, hudFade, zoomTween;
        bool swiping;
        Vector2 swipeStart;

        PathManager Board => levelManager ? levelManager.pathManager : null;

        void Awake()
        {
            // The router decides when a level loads (Play), not LevelManager.Start.
            if (levelManager) levelManager.loadOnStart = false;
            if (timer) timer.AddPauseSource(PausesTimer);
        }

        /// <summary>Something covers the level: another screen, a panel, the boost offer, a house transition.</summary>
        public bool PausesTimer() =>
            Current != AppScreen.Game || AnyPanelOpen || (houseTransition && houseTransition.Playing);

        public bool AnyPanelOpen =>
            (premium && premium.IsOpen) || (shop && shop.IsOpen) || (settings && settings.IsOpen) || (win && win.IsOpen) ||
            (pause && pause.IsOpen) || (timeUp && timeUp.IsOpen) || (hud && hud.BoostButtons && hud.BoostButtons.OfferOpen);

        void OnEnable() { if (map) map.SignInView += OnMapSign; }
        void OnDisable() { if (map) map.SignInView -= OnMapSign; }

        // Scrolling the map turns the sky's wheel to the house in view.
        void OnMapSign(int sign)
        {
            if (Current == AppScreen.Map && sky) sky.ShowSign(sign);
        }

        void Start()
        {
            ShowHome(true);
            if (home) home.PlayIntro();
        }

        /// <summary>Opens the first unfinished level.</summary>
        public void Play()
        {
            if (!levelManager) return;
            PlayLevel(levelManager.NextUnfinishedIndex);
        }

        public void PlayLevel(int index)
        {
            if (!levelManager) return;
            SetScreen(AppScreen.Game, false);
            levelManager.LoadLevel(index);
            if (board) board.Show(true, false);
        }

        public void ShowHome(bool instant = false) => SetScreen(AppScreen.Home, instant);

        /// <summary>The level map (without one, Home). Leaving a level this way pauses it.</summary>
        public void ShowMap(bool instant = false)
        {
            if (!map || !map.enabled) { ShowHome(instant); return; }
            SetScreen(AppScreen.Map, instant);
            map.Show(instant);
        }

        /// <summary>Closes the top panel; otherwise steps back one screen: Game → Map → Home.</summary>
        public void Back()
        {
            if (CloseTopPanel()) return;
            if (Current == AppScreen.Game) ShowMap();
            else if (Current == AppScreen.Map) ShowHome();
        }

        bool CloseTopPanel()
        {
            if (premium && premium.IsOpen) { premium.Close(); return true; }
            if (shop && shop.IsOpen) { shop.Close(); return true; }
            if (settings && settings.IsOpen) { settings.Close(); return true; }
            if (hud && hud.BoostButtons && hud.BoostButtons.OfferOpen) { hud.BoostButtons.CloseOffer(); return true; }
            if (pause && pause.IsOpen) { pause.Close(); return true; }
            if (map && Current == AppScreen.Map && map.CloseTop()) return true;
            return false;
        }

        void SetScreen(AppScreen target, bool instant)
        {
            bool game = target == AppScreen.Game;
            Current = target;
            if (Board) Board.suspended = !game;

            if (!game)
            {
                if (win) win.Hide();
                if (pause) pause.Hide();
                if (timeUp) timeUp.Hide(true);
                if (houseTransition) houseTransition.Stop();
                if (board) board.Show(false, instant);
                if (home) home.Refresh();
                if (sky && levelManager) sky.ShowSign(Zodiac.SignOf(levelManager.NextUnfinishedIndex + 1));
            }
            if (home) homeFade = Fade(home.Group, home.Content, target == AppScreen.Home, instant, homeFade);
            if (map)
            {
                if (target != AppScreen.Map) map.CloseTop();
                mapFade = Fade(map.Group, map.Content, target == AppScreen.Map, instant, mapFade);
            }
            if (hud) hudFade = Fade(hud.Group, hud.Content, game, instant, hudFade);

            float blend = instant ? 0f : style.skyBlendTime;
            if (sky) sky.SetHome(target == AppScreen.Home, blend);
            if (parallax)
            {
                zoomTween?.Kill();
                float from = parallax.zoom, to = target == AppScreen.Home ? style.Zodiac.homeZoom : 1f;
                if (instant) parallax.zoom = to;
                else zoomTween = Tween.Run(parallax, blend, Ease.InOutSine, k => parallax.zoom = Mathf.Lerp(from, to, k));
            }
            ScreenChanged?.Invoke(target);
        }

        // Crossfade with a slight scale: the incoming screen grows into place, the outgoing one grows past it.
        Tween Fade(CanvasGroup group, RectTransform content, bool show, bool instant, Tween running)
        {
            running?.Kill();
            if (!group) return null;
            group.blocksRaycasts = show;
            group.interactable = show;
            float from = group.alpha, to = show ? 1f : 0f;
            float scaleFrom = show ? style.screenScaleFrom : 1f, scaleTo = show ? 1f : 2f - style.screenScaleFrom;
            if (instant)
            {
                group.alpha = to;
                if (content) content.localScale = Vector3.one;
                return null;
            }
            return Tween.Run(group, style.screenFadeTime, Ease.OutCubic, k =>
            {
                group.alpha = Mathf.Lerp(from, to, k);
                if (content) content.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, k);
            }, () => { if (content) content.localScale = Vector3.one; });
        }

        // ---------- back button / swipe (no allocations) ----------

        void Update()
        {
            var keyboard = Keyboard.current; // Android's back button arrives as Escape
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Back();

            var pointer = Pointer.current;
            if (pointer == null) return;
            float scale = UIKit.ReferenceScale;
            if (pointer.press.wasPressedThisFrame)
            {
                swipeStart = pointer.position.ReadValue();
                swiping = Current != AppScreen.Home && swipeStart.x - Screen.safeArea.xMin <= style.backSwipeEdge * scale;
            }
            else if (swiping && pointer.press.wasReleasedThisFrame)
            {
                swiping = false;
                var d = pointer.position.ReadValue() - swipeStart;
                if (d.x >= style.backSwipeDistance * scale && Mathf.Abs(d.y) < d.x * 0.6f) Back();
            }
        }
    }
}
