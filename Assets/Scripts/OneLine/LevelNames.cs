using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// Constellation-like names used as level titles. Deterministic: the list is shuffled once with a fixed seed
    /// and levels walk through it, so a level always has the same name and names don't repeat within a cycle.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelNames", menuName = "One Line/Level Names")]
    public class LevelNames : ScriptableObject
    {
        public int seed = 1729;
        public string[] names =
        {
            "Lyra", "Vega's Crown", "The Lantern", "Cassia", "Orion's Belt", "Altair", "Deneb", "The Swan",
            "Polaris", "Aurora's Veil", "Sirius", "The Shepherd", "Andromeda", "The Harp", "Capella", "Arcturus",
            "The Little Bear", "Pleiades", "The Seven Sisters", "Rigel", "Betelgeuse", "The Hunter", "Cygnus",
            "Draco's Coil", "Aquila", "Spica", "The Maiden", "Auriga", "Carina", "The Keel", "Corona",
            "The Northern Crown", "Perseus", "Cepheus", "Cassiopeia", "The Queen's Chair", "Hydra", "Fomalhaut",
            "Canopus", "Mira", "Alcor", "Mizar", "The Wanderer", "Luna's Thread", "The Silver Gate", "Evening Star",
            "Morning Star", "The Quiet Crown", "Nightingale", "The Lantern Bearer", "Whisperwind", "Starfall",
            "The Glass Bird", "Moonwell", "The Loom", "Ember Crown", "The Veil", "Solstice",
        };

        int[] order;

        public string Get(int levelNumber)
        {
            if (names == null || names.Length == 0) return $"Level {levelNumber}";
            if (order == null || order.Length != names.Length) Shuffle();
            int i = ((levelNumber - 1) % order.Length + order.Length) % order.Length;
            return names[order[i]];
        }

        void Shuffle()
        {
            order = new int[names.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            var rnd = new System.Random(seed);
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        void OnValidate() => order = null;
    }
}
