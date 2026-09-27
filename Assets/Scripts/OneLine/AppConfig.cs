using UnityEngine;

namespace OneLine
{
    /// <summary>App-level settings that aren't visual: the Celestial Pass offer and the About section.</summary>
    [CreateAssetMenu(fileName = "AppConfig", menuName = "One Line/App Config")]
    public class AppConfig : ScriptableObject
    {
        [Header("Celestial Pass (no ads)")]
        [Tooltip("Store product id of the one-time, non-consumable purchase.")]
        public string passProductId = "com.oneline.celestialpass";
        public string passTitle = "Celestial Pass";
        public string passSubtitle = "No ads · forever";
        [Tooltip("Shown until a real store provides the localized price.")]
        public string passPriceLabel = "$2.99";
        public int passBonusCoins = 500;
        [Tooltip("Exclusive cosmetic granted with the pass (a line skin marked Exclusive in the catalog).")]
        public string passSkinId = "skin.celestial";
        [Tooltip("{0} = passBonusCoins.")]
        public string[] passPerks =
        {
            "No interstitial ads — forever",
            "Rewarded ads stay optional, for hints and extra time",
            "One-time gift: {0} coins",
            "Exclusive Celestial Gold wand trail",
        };
        [Tooltip("Mock store only: seconds before a purchase or restore answers.")]
        public float mockStoreDelay = 1.2f;

        [Header("About")]
        [Tooltip("Placeholder — leave empty until the policy page exists.")]
        public string privacyPolicyUrl = "";
        public string credits = "Fonts: Cinzel & Quicksand (SIL Open Font License)";
    }
}
