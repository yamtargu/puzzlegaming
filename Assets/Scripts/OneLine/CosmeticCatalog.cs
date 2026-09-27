using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Everything the shop sells. Items are looks only (colors, and the wand's line + dust effects) — they can't touch gameplay.
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

        /// <summary>
        /// A wand: how the drawn line looks, the dust that follows the finger and the burst when the line reaches a star
        /// (WandTrail on the board, WandPreview in the shop). Every value lives in its WandLook asset.
        /// </summary>
        [Serializable]
        public class Wand : Item
        {
            public WandEffect effect = WandEffect.Classic;
            public WandRarity rarity = WandRarity.Common;
            [Tooltip("Colors, widths, speeds and particle settings. Empty = the built-in Classic look.")]
            public WandLook look;
        }

        public List<LineSkin> lineSkins = new();
        public List<Background> backgrounds = new();
        [Tooltip("Wands (line + dust effects). The first (Classic) is free and the default. Built by One Line/Setup Levels + Scene.")]
        public List<Wand> wands = DefaultWands();

        /// <summary>Fallback for catalogs saved before wands existed: just the free Classic wand.</summary>
        public static List<Wand> DefaultWands() => new()
        {
            new Wand { id = "wand.stardust", displayName = "Classic", price = 0 },
        };

        public LineSkin EquippedSkin() => Pick(lineSkins, Cosmetics.EquippedSkinId);
        public Wand EquippedWand() => Pick(wands, Cosmetics.EquippedWandId);

        /// <summary>The line skin or background with this id, or null.</summary>
        public Item Find(string id)
        {
            foreach (var item in lineSkins) if (item.id == id) return item;
            foreach (var item in backgrounds) if (item.id == id) return item;
            foreach (var item in wands) if (item.id == id) return item;
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
