using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Everything the shop sells. Items are looks only (colors, and the wand's trail particles) — they can't touch gameplay.
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

        public enum WandParticle { Dust, Sparkle, Star }

        /// <summary>The wand: how the dust coming off the tip of the drawn line looks and moves (WandTrail).</summary>
        [Serializable]
        public class Wand : Item
        {
            public WandParticle particle = WandParticle.Dust;
            [Tooltip("Particles per connection, as a multiple of StarStyle.dustPerSegment.")]
            public float amount = 1f;
            [Tooltip("Start size range (world units).")]
            public Vector2 size = new(0.025f, 0.06f);
            [Tooltip("Lifetime range (s).")]
            public Vector2 lifetime = new(0.6f, 1.1f);
            [Tooltip("Random speed off the tip (world units / s).")]
            public float scatter = 0.25f;
            [Tooltip("Steady drift (world units / s): up = rising sparks, down = falling.")]
            public Vector2 drift = new(0f, 0.08f);
            [Tooltip("Speed back along the line, away from the moving tip (a comet's tail).")]
            public float trailBack;
            [Tooltip("Stretches particles along their motion (0 = round motes).")]
            public float stretch;
            [Tooltip("Blend the line's colors toward this color by tintAmount.")]
            public Color tint = Color.white;
            [Range(0f, 1f)] public float tintAmount;
        }

        public List<LineSkin> lineSkins = new();
        public List<Background> backgrounds = new();
        [Tooltip("Trail effects. Defaults fill in for catalogs saved before wands existed.")]
        public List<Wand> wands = DefaultWands();

        /// <summary>The default wand list; the first (Stardust) is the original look and free.</summary>
        public static List<Wand> DefaultWands() => new()
        {
            new Wand { id = "wand.stardust", displayName = "Stardust", price = 0 },
            new Wand
            {
                id = "wand.twinkle", displayName = "Twinkle", price = 60, particle = WandParticle.Sparkle, amount = 1.2f,
                size = new Vector2(0.05f, 0.1f), lifetime = new Vector2(0.5f, 0.9f), scatter = 0.3f, drift = new Vector2(0f, 0.03f),
            },
            new Wand
            {
                id = "wand.comet", displayName = "Comet Tail", price = 90, amount = 2.5f, size = new Vector2(0.03f, 0.07f),
                lifetime = new Vector2(0.4f, 0.7f), scatter = 0.05f, drift = Vector2.zero, trailBack = 0.6f, stretch = 0.35f,
            },
            new Wand
            {
                id = "wand.ember", displayName = "Ember", price = 110, amount = 1.5f, size = new Vector2(0.02f, 0.05f),
                lifetime = new Vector2(0.8f, 1.4f), scatter = 0.15f, drift = new Vector2(0f, 0.35f),
                tint = new Color(1f, 0.55f, 0.2f), tintAmount = 0.7f,
            },
            new Wand
            {
                id = "wand.moonbeam", displayName = "Moonbeam", price = 140, particle = WandParticle.Star, amount = 0.8f,
                size = new Vector2(0.05f, 0.09f), lifetime = new Vector2(0.9f, 1.4f), scatter = 0.12f, drift = new Vector2(0f, -0.05f),
                tint = new Color(0.75f, 0.85f, 1f), tintAmount = 0.6f,
            },
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
