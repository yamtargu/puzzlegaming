using System;

namespace OneLine
{
    public enum BoostType { SaturnsGift, LunarStillness }

    /// <summary>Where boosts came from. RewardedAd: RewardedAds. Iap is a hook for later — nothing grants from it yet.</summary>
    public enum BoostSource { Shop, BossReward, RewardedAd, Iap, Debug }

    /// <summary>
    /// Time-boost inventory (saved by SaveService): Saturn's Gift (+seconds) and Lunar Stillness (slower timer).
    /// The only purchases that affect gameplay. Buying spends coins through CoinManager; rewarded ads (RewardedAds) and
    /// any future source (IAP) add through <see cref="Grant"/>. Knows nothing about the timer or the UI.
    /// </summary>
    public static class Boosts
    {
        /// <summary>Raised after a count changes. Arguments: the boost, its new count.</summary>
        public static event Action<BoostType, int> Changed;

        static TimerSettings settings;

        /// <summary>Starting stock and prices come from here (LevelTimer and ShopUI call this on Awake).</summary>
        public static void Configure(TimerSettings s)
        {
            if (s) settings = s;
        }

        public static TimerSettings Settings => settings;

        public static string DisplayName(BoostType type) => type == BoostType.SaturnsGift ? "Saturn's Gift" : "Lunar Stillness";

        public static int Count(BoostType type) =>
            SaveService.GetBoost(type, settings ? settings.StartingCount(type) : 2);

        public static int Price(BoostType type, int amount) =>
            settings ? settings.Price(type, amount) : (type == BoostType.SaturnsGift ? 30 : 40) * amount;

        /// <summary>Adds boosts from any source (shop, BOSS reward, and later a rewarded ad or IAP).</summary>
        public static void Grant(BoostType type, int amount, BoostSource source)
        {
            if (amount <= 0) return;
            Set(type, Count(type) + amount);
        }

        /// <summary>Uses one if there is one. Returns false (and changes nothing) otherwise.</summary>
        public static bool TryConsume(BoostType type)
        {
            int n = Count(type);
            if (n <= 0) return false;
            Set(type, n - 1);
            return true;
        }

        /// <summary>Buys <paramref name="amount"/> for coins. False (nothing spent) if the balance is short.</summary>
        public static bool TryBuy(BoostType type, int amount)
        {
            if (amount <= 0 || !CoinManager.Spend(Price(type, amount))) return false;
            Grant(type, amount, BoostSource.Shop);
            return true;
        }

        /// <summary>Coins still missing to buy <paramref name="amount"/> (0 = affordable).</summary>
        public static int Missing(BoostType type, int amount) =>
            Math.Max(0, Price(type, amount) - CoinManager.GetBalance());

        static void Set(BoostType type, int count)
        {
            SaveService.SetBoost(type, count);
            SaveService.Save();
            Changed?.Invoke(type, count);
        }
    }
}
