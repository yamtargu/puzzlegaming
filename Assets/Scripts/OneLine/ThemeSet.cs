using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>One name + palette per difficulty tier (10 levels each). Edit freely in the Inspector.</summary>
    [CreateAssetMenu(fileName = "ThemeSet", menuName = "One Line/Theme Set")]
    public class ThemeSet : ScriptableObject
    {
        [Serializable]
        public class Tier
        {
            public string name;
            public Color background;
            public Color edge;
            public Color node;
            public Color path;
        }

        public List<Tier> tiers = new();

        /// <summary>Theme for a tier index; levels past the last tier keep the last theme.</summary>
        public Tier Get(int tierIndex) =>
            tiers.Count == 0 ? null : tiers[Mathf.Clamp(tierIndex, 0, tiers.Count - 1)];
    }
}
