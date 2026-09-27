using System;
using UnityEngine;

namespace OneLine
{
    public enum PurchaseResult { Success, Cancelled, Failed }

    /// <summary>
    /// The store, behind an interface: MockPurchaseService stands in until Unity IAP is added, and a real
    /// implementation only has to be assigned to <see cref="PurchaseService.Current"/>.
    /// </summary>
    public interface IPurchaseService
    {
        bool IsBusy { get; }
        void Purchase(string productId, Action<PurchaseResult> done);
        /// <summary>Restores non-consumables bought earlier. done(true) if something was restored.</summary>
        void Restore(Action<bool> done);
    }

    public static class PurchaseService
    {
        static IPurchaseService current;
        public static IPurchaseService Current
        {
            get => current ??= new MockPurchaseService();
            set => current = value;
        }
    }

    /// <summary>
    /// Development stand-in for the store: every purchase succeeds after a short delay (no real payment).
    /// Restore answers from the save, as a real store would from the account.
    /// </summary>
    public sealed class MockPurchaseService : IPurchaseService
    {
        public float delay = 1.2f;
        GameObject host;

        public bool IsBusy { get; private set; }

        public void Purchase(string productId, Action<PurchaseResult> done)
        {
            if (IsBusy) { done?.Invoke(PurchaseResult.Failed); return; }
            IsBusy = true;
            After(delay, () =>
            {
                IsBusy = false;
                Debug.Log($"MockPurchaseService: '{productId}' purchased (mock store, no payment).");
                done?.Invoke(PurchaseResult.Success);
            });
        }

        public void Restore(Action<bool> done) => After(delay * 0.5f, () => done?.Invoke(SaveService.RemoveAds));

        void After(float seconds, Action action)
        {
            if (!host)
            {
                host = new GameObject("[Mock Store]") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(host);
            }
            Tween.Run(host, seconds, Ease.Linear, null, action);
        }
    }

    /// <summary>What purchases unlock. Called after a successful purchase or restore.</summary>
    public static class Entitlements
    {
        /// <summary>Raised when an entitlement is granted.</summary>
        public static event Action Changed;

        public static bool HasPass => SaveService.RemoveAds;

        /// <summary>
        /// The Celestial Pass: no interstitial ads, plus a one-time gift of coins and an exclusive cosmetic
        /// (cosmetic only — nothing that changes gameplay).
        /// </summary>
        public static void GrantPass(AppConfig config, CosmeticCatalog catalog)
        {
            SaveService.RemoveAds = true;
            if (config && !SaveService.PassBonusGiven)
            {
                SaveService.PassBonusGiven = true;
                CoinManager.Add(config.passBonusCoins);
            }
            var skin = config && catalog ? catalog.Find(config.passSkinId) : null;
            if (skin != null) Cosmetics.Grant(skin);
            Changed?.Invoke();
        }
    }
}
