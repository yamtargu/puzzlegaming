using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Every saved value of the game in one place (PlayerPrefs underneath): progress, star ratings, coins, settings,
    /// purchases, cosmetics, time boosts, the daily rewarded-ad count and local solve times. Keys stay compatible with earlier saves.
    /// </summary>
    public static class SaveService
    {
        // ---------- keys ----------
        public const string LastPlayedKey = "OneLine.Level";      // index of the level opened last
        public const string UnlockedKey = "OneLine.Unlocked";     // index of the first level not finished yet
        public const string StarsKey = "OneLine.Stars";           // one digit (0-3) per level index
        public const string CoinsKey = "OneLine.Coins";
        public const string SfxKey = "OneLine.Sound";             // the old single sound toggle, now sound effects
        public const string MusicKey = "OneLine.Music";
        public const string HapticsKey = "OneLine.Haptics";
        public const string ReduceMotionKey = "OneLine.ReduceMotion";
        public const string RemoveAdsKey = "OneLine.RemoveAds";
        public const string PassBonusKey = "OneLine.PassBonus";   // the Celestial Pass gift was given
        public const string OwnedPrefix = "OneLine.Owned.";
        public const string SkinKey = "OneLine.Skin";
        public const string BackgroundKey = "OneLine.Background";
        public const string RevealedHouseKey = "OneLine.MapHouse"; // last zodiac house the map has revealed
        public const string SaturnKey = "OneLine.Boost.Saturn";    // Saturn's Gift count
        public const string LunarKey = "OneLine.Boost.Lunar";      // Lunar Stillness count
        public const string TimerIntroKey = "OneLine.TimerIntro";  // the first timed level's hint was shown
        public const string SolveSumPrefix = "OneLine.SolveSum.";  // + level index: summed solve seconds
        public const string SolveCountPrefix = "OneLine.SolveN.";  // + level index: wins timed
        public const string WandKey = "OneLine.Wand";              // equipped wand (trail effect)
        public const string AdDayKey = "OneLine.Ads.Day";          // local date (yyyymmdd) of the rewarded-ad count
        public const string AdCountKey = "OneLine.Ads.Count";      // rewarded ads watched on that day

        /// <summary>Raised after a setting or an entitlement changes. Argument: the key that changed.</summary>
        public static event Action<string> Changed;

        // ---------- progress ----------

        public static int LastPlayed
        {
            get => GetInt(LastPlayedKey, 0);
            set => SetInt(LastPlayedKey, Mathf.Max(0, value));
        }

        /// <summary>Index of the first unfinished level — levels unlock in order, so also the number finished.</summary>
        public static int HighestUnlocked
        {
            // Saves from before this key existed: every level before the last one played counts as finished.
            get => HasKey(UnlockedKey) ? GetInt(UnlockedKey, 0) : LastPlayed;
            set => SetInt(UnlockedKey, Mathf.Max(0, value));
        }

        /// <summary>False on a first launch (nothing played yet).</summary>
        public static bool HasProgress => HasKey(LastPlayedKey) || HasKey(UnlockedKey);

        public static bool IsCompleted(int index) => index < HighestUnlocked || GetStars(index) > 0;

        /// <summary>Best star rating (0-3) of a level.</summary>
        public static int GetStars(int index)
        {
            var s = Stars;
            return index >= 0 && index < s.Length ? Mathf.Clamp(s[index] - '0', 0, 3) : 0;
        }

        /// <summary>Saves a finished level: keeps the best rating and unlocks the next level.</summary>
        public static void RecordWin(int index, int stars)
        {
            if (index < 0) return;
            stars = Mathf.Clamp(stars, 0, 3);
            if (stars > GetStars(index))
            {
                var chars = Stars.PadRight(index + 1, '0').ToCharArray();
                chars[index] = (char)('0' + stars);
                starsCache = new string(chars);
                SetString(StarsKey, starsCache);
            }
            if (index + 1 > HighestUnlocked) HighestUnlocked = index + 1;
            Save();
        }

        /// <summary>Last zodiac house whose reveal the map has played; -1 before the map was first opened.</summary>
        public static int RevealedHouse
        {
            get => GetInt(RevealedHouseKey, -1);
            set => SetInt(RevealedHouseKey, value);
        }

        static string starsCache;
        static string Stars => starsCache ??= GetString(StarsKey, "");

        // ---------- coins ----------

        /// <summary>Raw balance. Use CoinManager to change it (it raises the balance event).</summary>
        public static int Coins
        {
            get => GetInt(CoinsKey, 0);
            set => SetInt(CoinsKey, Mathf.Max(0, value));
        }

        // ---------- time boosts & timed levels ----------

        /// <summary>Boost count; <paramref name="fallback"/> (the starting stock) until the first change.</summary>
        public static int GetBoost(BoostType type, int fallback) => GetInt(BoostKey(type), fallback);
        public static void SetBoost(BoostType type, int count) => SetInt(BoostKey(type), Mathf.Max(0, count));
        static string BoostKey(BoostType type) => type == BoostType.SaturnsGift ? SaturnKey : LunarKey;

        public static bool TimerIntroSeen { get => GetInt(TimerIntroKey, 0) == 1; set => SetInt(TimerIntroKey, value ? 1 : 0); }

        /// <summary>Rewarded ads watched today (local calendar day); resets on a new day.</summary>
        public static int RewardedAdsToday => GetInt(AdDayKey, 0) == Today ? GetInt(AdCountKey, 0) : 0;

        public static void RecordRewardedAd()
        {
            int count = RewardedAdsToday + 1;
            SetInt(AdDayKey, Today);
            SetInt(AdCountKey, count);
            Save();
        }

        static int Today
        {
            get
            {
                var d = DateTime.Now;
                return d.Year * 10000 + d.Month * 100 + d.Day;
            }
        }

        /// <summary>Adds one win's solve time (first touch → win, paused time excluded) to the level's local average.</summary>
        public static void RecordSolveTime(int index, float seconds)
        {
            if (index < 0 || seconds <= 0f) return;
            SetFloat(SolveSumPrefix + index, GetFloat(SolveSumPrefix + index, 0f) + seconds);
            SetInt(SolveCountPrefix + index, GetInt(SolveCountPrefix + index, 0) + 1);
        }

        /// <summary>Average local solve time in seconds; 0 if never recorded. For tuning the time limits.</summary>
        public static float AverageSolveTime(int index, out int wins)
        {
            wins = GetInt(SolveCountPrefix + index, 0);
            return wins > 0 ? GetFloat(SolveSumPrefix + index, 0f) / wins : 0f;
        }

        // ---------- settings ----------

        public static bool Music { get => GetInt(MusicKey, 1) == 1; set => SetFlag(MusicKey, value); }
        public static bool Sfx { get => GetInt(SfxKey, 1) == 1; set => SetFlag(SfxKey, value); }
        public static bool Haptics { get => GetInt(HapticsKey, 1) == 1; set => SetFlag(HapticsKey, value); }
        /// <summary>Shorter, calmer animations (the level-complete flare without the converge and flight).</summary>
        public static bool ReduceMotion { get => GetInt(ReduceMotionKey, 0) == 1; set => SetFlag(ReduceMotionKey, value); }

        // ---------- purchases ----------

        public static bool RemoveAds { get => GetInt(RemoveAdsKey, 0) == 1; set => SetFlag(RemoveAdsKey, value); }
        public static bool PassBonusGiven { get => GetInt(PassBonusKey, 0) == 1; set => SetFlag(PassBonusKey, value); }

        // ---------- cosmetics ----------

        public static bool IsOwned(string id) => GetInt(OwnedPrefix + id, 0) == 1;
        public static void SetOwned(string id, bool owned)
        {
            if (owned) SetInt(OwnedPrefix + id, 1);
            else DeleteKey(OwnedPrefix + id);
        }

        public static string EquippedSkin { get => GetString(SkinKey, ""); set => SetString(SkinKey, value); }
        public static string EquippedBackground { get => GetString(BackgroundKey, ""); set => SetString(BackgroundKey, value); }
        public static string EquippedWand { get => GetString(WandKey, ""); set => SetString(WandKey, value); }

        public static void ClearEquipped()
        {
            DeleteKey(SkinKey);
            DeleteKey(BackgroundKey);
            DeleteKey(WandKey);
        }

        // ---------- storage ----------

        public static void Save()
        {
            if (sandbox == null) PlayerPrefs.Save();
        }

        static void SetFlag(string key, bool on)
        {
            SetInt(key, on ? 1 : 0);
            Save();
            Changed?.Invoke(key);
        }

        // Sandbox (editor tests and screenshots): writes stay in memory, the real save is never touched.
        static Dictionary<string, object> sandbox;
        static bool sandboxFresh;

        /// <summary>
        /// Test mode: from now on writes go to memory only. With <paramref name="fresh"/>, reads also ignore the real
        /// save (a first-launch player).
        /// </summary>
        public static void BeginSandbox(bool fresh = false)
        {
            sandbox = new Dictionary<string, object>();
            sandboxFresh = fresh;
            starsCache = null;
        }

        public static void EndSandbox()
        {
            sandbox = null;
            starsCache = null;
        }

        public static bool InSandbox => sandbox != null;

        // In the sandbox a deleted key is kept as null, so it doesn't fall back to the real save.
        static bool HasKey(string key)
        {
            if (sandbox == null) return PlayerPrefs.HasKey(key);
            if (sandbox.TryGetValue(key, out var v)) return v != null;
            return !sandboxFresh && PlayerPrefs.HasKey(key);
        }

        static int GetInt(string key, int fallback)
        {
            if (sandbox != null)
            {
                if (sandbox.TryGetValue(key, out var v)) return v is int i ? i : fallback;
                if (sandboxFresh) return fallback;
            }
            return PlayerPrefs.GetInt(key, fallback);
        }

        static float GetFloat(string key, float fallback)
        {
            if (sandbox != null)
            {
                if (sandbox.TryGetValue(key, out var v)) return v is float f ? f : fallback;
                if (sandboxFresh) return fallback;
            }
            return PlayerPrefs.GetFloat(key, fallback);
        }

        static void SetFloat(string key, float value)
        {
            if (sandbox != null) sandbox[key] = value;
            else PlayerPrefs.SetFloat(key, value);
        }

        static string GetString(string key, string fallback)
        {
            if (sandbox != null)
            {
                if (sandbox.TryGetValue(key, out var v)) return v as string ?? fallback;
                if (sandboxFresh) return fallback;
            }
            return PlayerPrefs.GetString(key, fallback);
        }

        static void SetInt(string key, int value)
        {
            if (sandbox != null) sandbox[key] = value;
            else PlayerPrefs.SetInt(key, value);
        }

        static void SetString(string key, string value)
        {
            if (sandbox != null) sandbox[key] = value;
            else PlayerPrefs.SetString(key, value);
        }

        static void DeleteKey(string key)
        {
            if (sandbox != null) sandbox[key] = null;
            else PlayerPrefs.DeleteKey(key);
        }
    }
}
