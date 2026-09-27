using System;
using UnityEngine;

namespace OneLine
{
    public enum AdResult { Rewarded, Skipped, Failed }

    /// <summary>
    /// The ad network, behind an interface: MockAdService stands in until Unity LevelPlay is added, and a real
    /// implementation only has to be assigned to <see cref="AdService.Current"/>. Callbacks must arrive on the main thread.
    /// </summary>
    public interface IAdService
    {
        bool IsBusy { get; }
        bool IsRewardedReady { get; }
        /// <summary>Shows a rewarded ad; done(Rewarded) only if it was watched to the end.</summary>
        void ShowRewarded(string placement, Action<AdResult> done);
    }

    public static class AdService
    {
        static IAdService current;
        public static IAdService Current
        {
            get => current ??= new MockAdService();
            set => current = value;
        }
    }

    /// <summary>Development stand-in: every rewarded ad "plays" for a moment and rewards (no real ad).</summary>
    public sealed class MockAdService : IAdService
    {
        GameObject host;

        public bool IsBusy { get; private set; }
        public bool IsRewardedReady => !IsBusy;

        public void ShowRewarded(string placement, Action<AdResult> done)
        {
            if (IsBusy) { done?.Invoke(AdResult.Failed); return; }
            IsBusy = true;
            if (!host)
            {
                host = new GameObject("[Mock Ads]") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(host);
            }
            float seconds = Boosts.Settings ? Boosts.Settings.mockAdSeconds : 1.5f;
            Tween.Run(host, seconds, Ease.Linear, null, () =>
            {
                IsBusy = false;
                Debug.Log($"MockAdService: rewarded ad '{placement}' watched (mock, no real ad).");
                done?.Invoke(AdResult.Rewarded);
            });
        }
    }

    /// <summary>
    /// Rewarded ads for the time boosts: a watched ad grants TimerSettings.rewardedBoostAmount through
    /// <see cref="Boosts.Grant"/> (BoostSource.RewardedAd), at most rewardedAdsPerDay a day. Optional for the player —
    /// the Celestial Pass removes interstitials only, never these.
    /// </summary>
    public static class RewardedAds
    {
        static TimerSettings S => Boosts.Settings;

        public static bool Enabled => S && S.rewardedAdsEnabled && S.rewardedBoostAmount > 0;
        public static int Amount => S ? S.rewardedBoostAmount : 0;
        public static int LeftToday => S ? Mathf.Max(0, S.rewardedAdsPerDay - SaveService.RewardedAdsToday) : 0;
        public static bool IsBusy => AdService.Current.IsBusy;
        /// <summary>An ad can be offered right now (enabled, under today's cap, the network has one ready).</summary>
        public static bool CanWatch => Enabled && LeftToday > 0 && !IsBusy && AdService.Current.IsRewardedReady;

        /// <summary>Shows an ad; if watched to the end, grants the boost. done(true) when granted.</summary>
        public static void WatchForBoost(BoostType type, Action<bool> done)
        {
            if (!CanWatch) { done?.Invoke(false); return; }
            AdService.Current.ShowRewarded(type == BoostType.SaturnsGift ? "boost_saturn" : "boost_lunar", result =>
            {
                bool rewarded = result == AdResult.Rewarded;
                if (rewarded)
                {
                    SaveService.RecordRewardedAd();
                    Boosts.Grant(type, Amount, BoostSource.RewardedAd);
                }
                done?.Invoke(rewarded);
            });
        }
    }
}
