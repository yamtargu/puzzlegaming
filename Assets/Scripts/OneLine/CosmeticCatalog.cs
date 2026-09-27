using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Everything the shop sells. Items are colors only — they can't touch gameplay.
    /// The first item of each list should be free (price 0); it is the default.
    /// </summary>
    [CreateAssetMenu(fileName = "CosmeticCatalog", menuName = "One Line/Cosmetic Catalog")]
    public class CosmeticCatalog : ScriptableObject
    {
        [Serializable]
        public class Item
        {
            public string id;
            public string displayName;
            public int price;
            [Tooltip("The default look instead of this item's colors: the Starlight visit palette for line skins, " +
                     "the Deep space backdrop (tinted by the tier) for backgrounds.")]
            public bool useTierColors;
            [Tooltip("Can't be bought with coins — it comes with the Celestial Pass.")]
            public bool exclusive;
        }

        [Serializable]
        public class LineSkin : Item
        {
            public Color line = Color.white;
            public Color node = Color.gray;
        }

        [Serializable]
        public class Background : Item
        {
            public Color background = Color.black;
            public Color edge = Color.gray;
        }

        public List<LineSkin> lineSkins = new();
        public List<Background> backgrounds = new();

        public LineSkin EquippedSkin() => Pick(lineSkins, Cosmetics.EquippedSkinId);

        /// <summary>The line skin or background with this id, or null.</summary>
        public Item Find(string id)
        {
            foreach (var item in lineSkins) if (item.id == id) return item;
            foreach (var item in backgrounds) if (item.id == id) return item;
            return null;
        }
        public Background EquippedBackground() => Pick(backgrounds, Cosmetics.EquippedBackgroundId);

        // The equipped item if it exists and is owned; otherwise the default (first) item.
        static T Pick<T>(List<T> items, string id) where T : Item
        {
            foreach (var item in items)
                if (item.id == id && Cosmetics.IsOwned(item)) return item;
            return items.Count > 0 ? items[0] : null;
        }
    }
}
