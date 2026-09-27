using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>Ordered list of levels. The game plays them in this order.</summary>
    [CreateAssetMenu(fileName = "LevelPack", menuName = "One Line/Level Pack")]
    public class LevelPack : ScriptableObject
    {
        public List<LevelData> levels = new();

        public int Count => levels.Count;
        public LevelData this[int i] => levels[i];
    }
}
