using System;

namespace OneLine
{
    /// <summary>
    /// Which cosmetics the player owns and has equipped (saved by SaveService).
    /// Buying spends coins; equipping only changes which colors the board is drawn with. Exclusive items can't be
    /// bought with coins — they come with the Celestial Pass (Entitlements).
    /// </summary>
    public static class Cosmetics
    {
        /// <summary>Raised after a purchase or an equip change.</summary>
        public static event Action Changed;

        public static string EquippedSkinId => SaveService.EquippedSkin;
        public static string EquippedBackgroundId => SaveService.EquippedBackground;

        public static bool IsOwned(CosmeticCatalog.Item item) =>
            (!item.exclusive && item.price <= 0) || SaveService.IsOwned(item.id);

        public static bool IsEquipped(CosmeticCatalog.Item item) =>
            (item is CosmeticCatalog.LineSkin ? EquippedSkinId : EquippedBackgroundId) == item.id;

        /// <summary>Buys the item if the player can afford it. Returns true if it is owned afterwards.</summary>
        public static bool TryBuy(CosmeticCatalog.Item item)
        {
            if (IsOwned(item)) return true;
            if (item.exclusive || !CoinManager.Spend(item.price)) return false;
            Grant(item);
            return true;
        }

        /// <summary>Gives an item without charging coins (purchases, restores).</summary>
        public static void Grant(CosmeticCatalog.Item item)
        {
            SaveService.SetOwned(item.id, true);
            SaveService.Save();
            Changed?.Invoke();
        }

        /// <summary>Equips an owned item in its slot (line skin or background). Returns false if not owned.</summary>
        public static bool Equip(CosmeticCatalog.Item item)
        {
            if (!IsOwned(item)) return false;
            if (item is CosmeticCatalog.LineSkin) SaveService.EquippedSkin = item.id;
            else SaveService.EquippedBackground = item.id;
            SaveService.Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Forgets every purchase and equip (for testing).</summary>
        public static void ResetAll(CosmeticCatalog catalog)
        {
            foreach (var item in catalog.lineSkins) SaveService.SetOwned(item.id, false);
            foreach (var item in catalog.backgrounds) SaveService.SetOwned(item.id, false);
            SaveService.ClearEquipped();
            SaveService.Save();
            Changed?.Invoke();
        }
    }
}
