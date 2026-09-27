using System;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Poetic level titles per zodiac house: real stars of the constellation, its ruling planet, element and myth.
    /// Deterministic: each sign's list is shuffled once with a fixed seed and the sign's levels walk through it,
    /// so a level always has the same name and no name repeats inside a sign. BOSS levels take the sign's
    /// iconic names instead, in order.
    /// </summary>
    [CreateAssetMenu(fileName = "ZodiacNames", menuName = "One Line/Zodiac Names")]
    public class ZodiacNames : ScriptableObject
    {
        [Serializable]
        public class Sign
        {
            public string sign;
            [Tooltip("BOSS levels, in order — the first is the sign's most iconic name.")]
            public string[] bossNames;
            [Tooltip("Every other level. Keep at least as many as the sign has levels (~42) to avoid repeats.")]
            public string[] names;
        }

        public int seed = 1729;

        public Sign[] signs =
        {
            new()
            {
                sign = "Aries",
                bossNames = new[] { "The Great Ram", "Mars Ascendant", "Crown of Fire", "The Fleece Eternal", "Hamal's Throne" },
                names = new[]
                {
                    "The Ram's Horn", "Mars Rising", "First Fire", "Hamal", "Sheratan", "Mesarthim", "Botein",
                    "The Golden Fleece", "Phrixus' Flight", "Helle's Fall", "Colchis Gate", "Spring Equinox", "First Point",
                    "Red Ember", "Kindling Star", "The Iron Crown", "Ares' Spear", "Dawn Charger", "Burning Meadow",
                    "The Herald", "Golden Wool", "Mars' Forge", "Crimson Vane", "Blaze Path", "The Bold Step",
                    "Cardinal Flame", "The Young Sun", "Ram's Leap", "Aries Ember", "Firstlight", "Warrior's Oath",
                    "The Spark Road", "Hearth of Mars", "Chrysomallos", "Jason's Quest", "The Argo Sails", "Vernal Fire",
                    "Torch of Dawn", "The Charge", "Scarlet Horizon", "Brave Heart", "The Open Gate", "Iron and Ash",
                    "Sunrise Horn", "The Forge Light",
                },
            },
            new()
            {
                sign = "Taurus",
                bossNames = new[] { "The Great Bull", "Bull of Heaven", "Heart of the Bull", "The Pleiades", "Aldebaran's Watch" },
                names = new[]
                {
                    "The Bull's Eye", "Pleiades Veil", "Venus Garden", "Aldebaran", "Elnath", "The Hyades", "Seven Sisters",
                    "Europa's Ride", "Ain", "Alcyone", "Maia", "Electra", "Merope", "Taygeta", "Celaeno", "Sterope",
                    "Atlas", "Pleione", "The Crab Nebula", "Fertile Ground", "Emerald Pasture", "Venus Rose",
                    "Horns of Spring", "The Plough", "Earthsong", "Evening Venus", "Taurus Rising", "Rich Soil",
                    "Taurus Gate", "The Patient Star", "Velvet Field", "Copper Horn", "Golden Hoof", "Meadow Star",
                    "The Steady Heart", "Garden of Stars", "Stone and Bloom", "May Blossom", "Sky Ox", "The Rain Stars",
                    "Hathor's Crown", "Zeus Disguised", "The Deep Root", "Quiet Pasture", "Venus Morning",
                },
            },
            new()
            {
                sign = "Gemini",
                bossNames = new[] { "The Heavenly Twins", "Castor and Pollux", "Gemini Ascendant", "The Eternal Pair", "Mercury's Crown" },
                names = new[]
                {
                    "The Twins", "Castor", "Pollux", "Mercury's Path", "Alhena", "Mebsuta", "Tejat", "Propus", "Wasat",
                    "Mekbuda", "Alzirr", "Leda's Sons", "Dioscuri", "Mirror Star", "Twin Flames", "Two Lanterns",
                    "The Messenger", "Winged Heels", "Quicksilver", "Divided Light", "The Echo", "Brother Stars",
                    "Sparta's Guard", "Argo's Rowers", "Castor's Horse", "Pollux's Glove", "Breath of Air", "Paper Wings",
                    "The Letter", "Whispered Word", "Double Dawn", "The Crossroads", "Mercury Wind", "Twin Sails",
                    "Two Shores", "The Bridge", "Helen's Brothers", "Swift Signal", "Starlit Duet", "Two Voices",
                    "Hermes' Staff", "Mutable Sky", "Silver Tongue", "The Mirror Gate", "Open Window",
                },
            },
            new()
            {
                sign = "Cancer",
                bossNames = new[] { "The Great Crab", "Beehive Crown", "Karkinos Unbound", "Queen of Tides", "The Moon's Throne" },
                names = new[]
                {
                    "The Crab's Shell", "Beehive Cluster", "Praesepe", "Acubens", "Altarf", "Asellus", "Tegmine", "Karkinos",
                    "Hera's Servant", "Moon Harbor", "Tidal Home", "Summer Solstice", "The Tropic", "Silver Shore",
                    "Pearl Moon", "Moonlit Cove", "The Shell Gate", "Tide Pool", "Mother Moon", "Hearthwater",
                    "Salt and Silver", "Deep Current", "The Manger", "Lantern Bay", "Quiet Harbor", "Crescent Tide",
                    "Moon's Cradle", "Sea Glass", "The Keepsake", "Driftwood Star", "Pale Lagoon", "The Sideways Step",
                    "Coral Keep", "Night Tide", "Moonwell", "Luna's Garden", "Harbor Lights", "The Shelter",
                    "Midsummer Moon", "Soft Armor", "Wave Hollow", "Home Star", "The Claw", "Moonstone", "Water Mirror",
                },
            },
            new()
            {
                sign = "Leo",
                bossNames = new[] { "The Great Lion", "Regulus Crowned", "The Nemean Lion", "King of Stars", "Heart of the Sun" },
                names = new[]
                {
                    "The Lion's Heart", "Regulus", "Solar Crown", "Denebola", "Algieba", "Zosma", "Chertan", "Adhafera",
                    "Rasalas", "The Sickle", "Nemean Hide", "Heracles' Trial", "Golden Mane", "Sun Throne", "Royal Star",
                    "Midsummer Blaze", "The Pride", "Amber Roar", "King's Road", "Noon Fire", "Sunfire", "The Lion's Tail",
                    "Proud Flame", "Radiant Court", "Lion Gate", "Gilded Paw", "The Sun's Heir", "Heart of Fire",
                    "Crowned Star", "Leonids", "Summer Crown", "Golden Hour", "The Regent", "Blazing Mane", "Lionheart",
                    "Sun Temple", "Ember Throne", "Brave Light", "The Hunt", "Warm Wind", "Sun Banner", "The Roar",
                    "Gold Dust", "Fixed Fire", "Honey Sun",
                },
            },
            new()
            {
                sign = "Virgo",
                bossNames = new[] { "The Star Maiden", "Spica's Crown", "Astraea's Return", "Queen of Harvest", "Demeter's Gift" },
                names = new[]
                {
                    "The Maiden", "Spica", "Porrima", "Vindemiatrix", "Zavijava", "Auva", "Heze", "Zaniah", "Syrma",
                    "Ear of Wheat", "Astraea", "Demeter's Field", "Persephone", "Harvest Moon", "Golden Sheaf", "The Gleaner",
                    "Autumn Bloom", "Grain of Light", "The Vintager", "Quiet Orchard", "Linen Star", "Mercury's Garden",
                    "The Healer", "Clear Spring", "Silver Sickle", "Barley Field", "Pure Thread", "Olive Grove",
                    "The Weaver", "Seed Vault", "Field of Stars", "Hidden Blossom", "Morning Dew", "The Almanac",
                    "Ripe Vine", "Careful Hand", "Herb Garden", "Pale Wheat", "The Lantern Maid", "Loom of Earth",
                    "Virgin Spring", "The Scribe", "Honey Field", "Late Summer", "Stone Garden",
                },
            },
            new()
            {
                sign = "Libra",
                bossNames = new[] { "The Great Scales", "Scales of Astraea", "The Final Balance", "Justice Crowned", "Venus Enthroned" },
                names = new[]
                {
                    "The Scales", "Zubenelgenubi", "Zubeneschamali", "Zubenelhakrabi", "Brachium", "Autumn Equinox",
                    "Balance Point", "Venus' Mirror", "Themis", "Dike's Law", "Even Light", "The Fulcrum", "Silver Beam",
                    "Equal Night", "Twin Pans", "Weighed Stars", "The Accord", "Gentle Wind", "Rose Balance",
                    "Fair Measure", "Harmony", "The Treaty", "Velvet Justice", "Air and Grace", "The Pendulum",
                    "Libra Gate", "Middle Way", "Crystal Scale", "Soft Verdict", "Venus at Dusk", "The Arbiter",
                    "Poised Star", "Feather Weight", "Copper Beam", "Tipping Light", "Calm Horizon", "The Covenant",
                    "Still Air", "Twilight Scale", "Chelae", "Grace Note", "Equal Days", "Silver Thread", "The Promise",
                    "Balanced Sky",
                },
            },
            new()
            {
                sign = "Scorpio",
                bossNames = new[] { "The Great Scorpion", "Antares Awakened", "Pluto's Throne", "The Final Sting", "Heart of Night" },
                names = new[]
                {
                    "Antares", "The Sting", "Pluto's Gate", "Shaula", "Sargas", "Dschubba", "Acrab", "Lesath", "Alniyat",
                    "Larawag", "Scorpion's Heart", "Rival of Mars", "Orion's Bane", "Gaia's Guardian", "Crimson Heart",
                    "Deep Water", "The Underworld", "Hidden Venom", "Night Current", "Obsidian Tide", "Black Pearl",
                    "The Descent", "Phoenix Ash", "Secret Spring", "The Tail Stars", "Ruby Eye", "Dark Harbor",
                    "Veiled Moon", "Silent Pool", "Pluto's Garden", "Hades' Key", "The Burrow", "Midnight Well",
                    "Mars in Water", "Curved Tail", "Serpent Tide", "Shadow Lake", "Deep Crimson", "The Oracle",
                    "Ember in Ice", "The Riddle", "Bitter Rose", "Undertow", "The Vow", "Autumn Night",
                },
            },
            new()
            {
                sign = "Sagittarius",
                bossNames = new[] { "The Great Archer", "Chiron Ascendant", "Jupiter's Crown", "Heart of the Galaxy", "The Final Arrow" },
                names = new[]
                {
                    "The Archer", "Chiron's Bow", "Kaus Australis", "Nunki", "Ascella", "Kaus Media", "Kaus Borealis",
                    "Alnasl", "Rukbat", "Arkab", "Albaldah", "The Teapot", "Galactic Heart", "Milky Way Gate",
                    "Jupiter's Road", "Centaur's Path", "Flying Arrow", "The Far Shore", "Wanderer's Star", "Open Road",
                    "Campfire", "Silver Quiver", "Crotus", "The Horizon", "Wild Hunt", "Bright Target", "Jupiter Rising",
                    "The Pilgrim", "Long Journey", "Fire Arrow", "Sky Galloper", "Lucky Star", "Philosopher's Lamp",
                    "The Quest", "Arrow of Truth", "Distant Fire", "Hoofbeat", "The Lagoon", "Trifid Glow", "Starward",
                    "Boundless", "The Compass", "Great Plains", "Ember Bow", "Winter's Eve",
                },
            },
            new()
            {
                sign = "Capricorn",
                bossNames = new[] { "The Great Sea-Goat", "Saturn Enthroned", "Crown of the Summit", "Amalthea's Horn", "The Eternal Mountain" },
                names = new[]
                {
                    "The Sea-Goat", "Saturn's Ring", "Deneb Algedi", "Dabih", "Algedi", "Nashira", "Alshat", "Pan's Flight",
                    "Amalthea", "Horn of Plenty", "Winter Solstice", "Mountain Peak", "Stone Stair", "The Summit",
                    "Father Time", "Saturn's Clock", "Frost Crown", "The Climb", "Granite Star", "Tropic of Winter",
                    "Iron Patience", "High Pasture", "Cold Harbor", "The Architect", "Ancient Path", "Sea and Stone",
                    "Enki's Waters", "Snow Line", "The Ledge", "Midwinter Light", "Silent Ridge", "Lead and Gold",
                    "The Keystone", "Slate Sky", "Winter Horn", "Longest Night", "Mossy Cliff", "The Ladder",
                    "Timekeeper", "Obsidian Peak", "The Cornerstone", "Frost Fern", "Steady Climb", "The Old Tower",
                    "Glacier Star",
                },
            },
            new()
            {
                sign = "Aquarius",
                bossNames = new[] { "The Water-Bearer", "Ganymede's Cup", "Uranus Awakened", "The Endless Stream", "Saturn's Heir" },
                names = new[]
                {
                    "Sadalsuud", "Sadalmelik", "Skat", "Sadachbia", "Albali", "Ancha", "Ganymede", "Uranus Spark",
                    "The Urn", "Stream of Stars", "Deucalion's Flood", "Helix Eye", "Lightning Air", "Future Star",
                    "The Visionary", "Open Sky", "Crystal Rain", "Electric Blue", "Silver Pitcher", "Cloud Harbor",
                    "Free Wind", "The Inventor", "Star Fountain", "Luck of Lucks", "New Age", "Rainbringer",
                    "Aether Current", "Frost Glass", "The Idealist", "Sky River", "Comet Trail", "Sapphire Wave",
                    "Kindred Stars", "Airborne", "Distant Signal", "The Rebel Star", "Hidden Spring", "Starwater",
                    "Wind Chime", "Blue Current", "Ice Crystal", "Aquarids", "Thunder Veil", "Wide Horizon", "Pouring Light",
                },
            },
            new()
            {
                sign = "Pisces",
                bossNames = new[] { "The Great Fishes", "Neptune Enthroned", "Ribbon of Aphrodite", "The Endless Sea", "Circle Eternal" },
                names = new[]
                {
                    "Two Fishes", "Neptune's Tide", "Alrescha", "Alpherg", "Fumalsamakah", "Torcular", "The Circlet",
                    "The Cord", "Aphrodite's Escape", "Eros' Ribbon", "Typhon's Shadow", "Dream Current", "Ocean Hymn",
                    "Deep Blue", "Mystic Water", "The Knot", "Silver Scale", "Moon Pool", "Last House", "Neptune's Veil",
                    "Coral Dream", "Sea Mist", "Pearl Diver", "Endless Ocean", "The Dreamer", "Jupiter's Sea",
                    "Quiet Depths", "Aurora Water", "Soft Horizon", "Rain Song", "Blue Lotus", "The Mirror Lake",
                    "Tidal Veil", "Starlit Reef", "Van Maanen", "Siren Song", "Twin Currents", "Foam Crown",
                    "Mermaid Light", "The Undersea", "Moonlit Scales", "Vernal Point", "Lost Harbor", "The Return",
                    "Ocean Star",
                },
            },
        };

        int[][] orders;

        /// <summary>Title of a level: an iconic name on BOSS levels, otherwise the next name of its sign.</summary>
        public string Get(int levelNumber)
        {
            int s = Zodiac.SignOf(levelNumber);
            if (signs == null || s >= signs.Length || signs[s] == null) return Zodiac.Names[s];
            var sign = signs[s];
            int first = Zodiac.FirstLevel(s);

            if (Difficulty.IsBoss(levelNumber) && sign.bossNames is { Length: > 0 })
            {
                // Which boss of this sign (0 = first).
                int ordinal = levelNumber / Difficulty.LevelsPerTier - (first - 1) / Difficulty.LevelsPerTier - 1;
                return sign.bossNames[Mathf.Clamp(ordinal, 0, int.MaxValue) % sign.bossNames.Length];
            }
            if (sign.names == null || sign.names.Length == 0) return Zodiac.Names[s];
            if (orders == null || orders.Length != signs.Length) orders = new int[signs.Length][];
            if (orders[s] == null || orders[s].Length != sign.names.Length) orders[s] = Shuffle(sign.names.Length, seed + 7919 * s);
            return sign.names[orders[s][(levelNumber - first) % sign.names.Length]];
        }

        static int[] Shuffle(int count, int seed)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            var rnd = new System.Random(seed);
            for (int i = count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        void OnValidate() => orders = null;
    }
}
