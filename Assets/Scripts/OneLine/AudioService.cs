using UnityEngine;
using UnityEngine.Audio;

namespace OneLine
{
    /// <summary>
    /// Music and sound-effect channels, each switched on/off in Settings (SaveService) and applied immediately.
    /// With an AudioMixer assigned (exposed parameters for the two groups' volume) it drives the mixer; without one,
    /// sound effects check <see cref="SfxOn"/> before playing and the music sources are muted directly.
    /// Two looping tracks, one audible at a time: the menu music (Home and Map) and the game music (while a level is
    /// played). Switching screens fades the outgoing track out quickly (then pauses it) while the incoming one starts
    /// at once, so the switch has no gap and barely any overlap. Each track resumes from where it paused; restarting
    /// a level, the next level or the pause panel keep the game screen, so the game music just keeps playing.
    /// </summary>
    public class AudioService : MonoBehaviour
    {
        [Header("Optional mixer")]
        public AudioMixer mixer;
        public AudioMixerGroup musicGroup;
        public AudioMixerGroup sfxGroup;
        [Tooltip("Exposed mixer parameters (volume in dB).")]
        public string musicParameter = "MusicVolume";
        public string sfxParameter = "SfxVolume";
        public float mutedDecibels = -80f;

        [Header("Music")]
        [Tooltip("Home screen and map.")]
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Tooltip("Loops while a level is played. Empty = silence in game.")]
        public AudioClip gameMusic;
        [Range(0f, 1f)] public float gameMusicVolume = 0.5f;
        [Tooltip("Switches the music between menu and game (without one, the menu music plays everywhere).")]
        public ScreenRouter router;
        [Tooltip("Menu ⇄ game: the outgoing track fades out over this (s), then pauses.")]
        public float switchFadeOut = 0.18f;
        [Tooltip("The incoming track starts at once and fades in over this (s).")]
        public float switchFadeIn = 0.12f;

        sealed class Track
        {
            public AudioSource source;
            public float volume, fade; // fade: 0..1
            public bool want, started;
            public Tween tween;
        }

        static AudioService instance;
        Track menuTrack, gameTrack;

        public static bool SfxOn => SaveService.Sfx;
        public static bool HapticsOn => SaveService.Haptics;

        /// <summary>Puts a sound-effect source on the mixer's SFX group, if there is one.</summary>
        public static void RouteSfx(AudioSource source)
        {
            if (instance && instance.sfxGroup && source) source.outputAudioMixerGroup = instance.sfxGroup;
        }

        void Awake()
        {
            instance = this;
            AudioListener.volume = 1f; // older builds muted the whole listener for "sound off"
            menuTrack = NewTrack(music, musicVolume);
            gameTrack = NewTrack(gameMusic, gameMusicVolume);
            Apply();
            if (!router) SetTrack(menuTrack, true, 0f); // no screens: music plays everywhere
        }

        Track NewTrack(AudioClip clip, float volume)
        {
            if (!clip) return null;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData(); // ready before the first switch
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            source.outputAudioMixerGroup = musicGroup;
            return new Track { source = source, volume = volume };
        }

        void OnEnable()
        {
            SaveService.Changed += OnSettingChanged;
            if (router) router.ScreenChanged += OnScreenChanged;
        }

        void OnDisable()
        {
            SaveService.Changed -= OnSettingChanged;
            if (router) router.ScreenChanged -= OnScreenChanged;
        }

        void OnScreenChanged(AppScreen screen) => SetGameMusic(screen == AppScreen.Game);

        /// <summary>Switches to the game music (or back to the menu music). Repeated calls change nothing.</summary>
        public void SetGameMusic(bool game)
        {
            // Fade out first, so the outgoing track is already dipping when the incoming one starts.
            SetTrack(game ? menuTrack : gameTrack, false, switchFadeOut);
            SetTrack(game ? gameTrack : menuTrack, true, switchFadeIn);
        }

        // Fades a track in (starting it, or resuming where it paused) or out (then pauses it).
        void SetTrack(Track track, bool play, float time)
        {
            if (track == null) return;
            bool fading = track.tween != null && track.tween.Alive;
            if (play == track.want && !fading && (track.started || !play)) return; // already there
            track.want = play;
            track.tween?.Kill();
            if (play)
            {
                if (!track.started) { track.source.Play(); track.started = true; }
                else track.source.UnPause();
            }
            float from = track.fade, to = play ? 1f : 0f;
            if (time <= 0f)
            {
                SetFade(track, to);
                if (!play) track.source.Pause();
                return;
            }
            track.tween = Tween.Run(this, time, Ease.Linear, k => SetFade(track, Mathf.Lerp(from, to, k)),
                () => { if (!track.want) track.source.Pause(); });
        }

        static void SetFade(Track track, float fade)
        {
            track.fade = fade;
            track.source.volume = track.volume * fade;
        }

        void OnSettingChanged(string key)
        {
            if (key == SaveService.MusicKey || key == SaveService.SfxKey) Apply();
        }

        void Apply()
        {
            if (mixer)
            {
                mixer.SetFloat(musicParameter, SaveService.Music ? 0f : mutedDecibels);
                mixer.SetFloat(sfxParameter, SaveService.Sfx ? 0f : mutedDecibels);
            }
            // Muted, not paused: the tracks keep their place and switch as usual.
            bool mute = !mixer && !SaveService.Music;
            if (menuTrack != null) menuTrack.source.mute = mute;
            if (gameTrack != null) gameTrack.source.mute = mute;
        }
    }
}
