using System;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// The 12 zodiac constellations as star figures: normalized positions (the figure fits a 1 x 1 box centered on 0,
    /// east to the left as seen on the sky), visual magnitudes (lower = brighter) and the lines between stars.
    /// Layouts come from the stars' real right ascension / declination.
    /// </summary>
    [CreateAssetMenu(fileName = "ZodiacConstellations", menuName = "One Line/Zodiac Constellations")]
    public class ZodiacConstellations : ScriptableObject
    {
        [Serializable]
        public class Constellation
        {
            public string name;
            public Vector2[] stars;
            public float[] magnitudes;
            public Vector2Int[] lines;
        }

        public Constellation[] figures =
        {
            Figure("Aries", new Vector2[] { new(0.258f, 0.014f), new(0.481f, -0.190f), new(0.500f, -0.307f), new(-0.500f, 0.307f) },
                new float[] { 2.0f, 2.6f, 3.9f, 3.6f },
                new Vector2Int[] { new(2, 1), new(1, 0), new(0, 3) }),
            Figure("Taurus", new Vector2[] { new(-0.035f, -0.074f), new(-0.415f, 0.312f), new(-0.500f, 0.074f), new(0.020f, -0.094f), new(0.086f, -0.102f), new(0.062f, -0.041f), new(0.020f, 0.011f), new(0.230f, -0.201f), new(0.500f, -0.312f), new(0.329f, 0.168f) },
                new float[] { 0.9f, 1.7f, 3.0f, 3.4f, 3.6f, 3.8f, 3.5f, 3.4f, 3.6f, 2.9f },
                new Vector2Int[] { new(1, 6), new(6, 5), new(5, 4), new(2, 0), new(0, 3), new(3, 4), new(4, 7), new(7, 8) }),
            Figure("Gemini", new Vector2[] { new(-0.376f, 0.402f), new(-0.492f, 0.218f), new(0.244f, -0.334f), new(0.176f, 0.081f), new(0.404f, -0.044f), new(0.492f, -0.044f), new(-0.218f, -0.069f), new(-0.044f, -0.136f), new(-0.196f, -0.327f), new(0.161f, -0.500f), new(-0.120f, 0.324f), new(0.080f, 0.500f), new(-0.483f, 0.046f) },
                new float[] { 1.6f, 1.1f, 1.9f, 3.0f, 2.9f, 3.3f, 3.5f, 3.9f, 3.6f, 3.4f, 4.4f, 3.6f, 3.6f },
                new Vector2Int[] { new(0, 10), new(10, 3), new(3, 4), new(4, 5), new(10, 11), new(1, 12), new(1, 6), new(6, 7), new(7, 2), new(6, 8), new(8, 9), new(0, 1) }),
            Figure("Cancer", new Vector2[] { new(-0.254f, -0.364f), new(0.254f, -0.500f), new(-0.087f, -0.042f), new(-0.070f, 0.127f), new(-0.111f, 0.500f) },
                new float[] { 4.3f, 3.5f, 3.9f, 4.7f, 4.0f },
                new Vector2Int[] { new(1, 2), new(2, 0), new(2, 3), new(3, 4) }),
            Figure("Leo", new Vector2[] { new(0.317f, -0.241f), new(0.326f, -0.076f), new(0.223f, 0.029f), new(0.250f, 0.152f), new(0.444f, 0.241f), new(0.500f, 0.164f), new(-0.216f, 0.053f), new(-0.217f, -0.122f), new(-0.500f, -0.152f) },
                new float[] { 1.4f, 3.5f, 2.0f, 3.4f, 3.9f, 3.0f, 2.6f, 3.3f, 2.1f },
                new Vector2Int[] { new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 5), new(2, 6), new(6, 8), new(8, 7), new(7, 0), new(6, 7) }),
            Figure("Virgo", new Vector2[] { new(-0.038f, -0.252f), new(0.209f, -0.031f), new(0.130f, 0.080f), new(0.093f, 0.252f), new(-0.093f, -0.011f), new(0.500f, 0.042f), new(0.334f, -0.013f), new(-0.328f, -0.134f), new(-0.482f, -0.127f), new(-0.246f, 0.037f), new(-0.500f, 0.045f) },
                new float[] { 1.0f, 2.7f, 3.4f, 2.8f, 3.4f, 3.6f, 3.9f, 4.1f, 3.9f, 4.3f, 3.7f },
                new Vector2Int[] { new(5, 6), new(6, 1), new(1, 2), new(2, 3), new(1, 0), new(0, 4), new(4, 2), new(4, 9), new(9, 10), new(0, 7), new(7, 8) }),
            Figure("Libra", new Vector2[] { new(0.276f, 0.174f), new(-0.025f, 0.500f), new(-0.239f, 0.235f), new(0.124f, -0.279f), new(-0.256f, -0.419f), new(-0.276f, -0.500f) },
                new float[] { 2.7f, 2.6f, 3.9f, 3.3f, 3.6f, 3.7f },
                new Vector2Int[] { new(0, 1), new(1, 2), new(2, 0), new(0, 3), new(2, 4), new(4, 5) }),
            Figure("Scorpio", new Vector2[] { new(0.423f, 0.497f), new(0.469f, 0.377f), new(0.482f, 0.230f), new(0.500f, 0.098f), new(0.280f, 0.252f), new(0.206f, 0.216f), new(0.148f, 0.140f), new(0.019f, -0.117f), new(0.003f, -0.277f), new(-0.016f, -0.459f), new(-0.180f, -0.497f), new(-0.407f, -0.486f), new(-0.500f, -0.365f), new(-0.454f, -0.318f), new(-0.374f, -0.236f), new(-0.348f, -0.245f) },
                new float[] { 2.6f, 2.3f, 2.9f, 3.9f, 2.9f, 1.0f, 2.8f, 2.3f, 3.0f, 3.6f, 3.3f, 1.9f, 3.0f, 2.4f, 1.6f, 2.7f },
                new Vector2Int[] { new(0, 1), new(1, 2), new(2, 3), new(1, 4), new(4, 5), new(5, 6), new(6, 7), new(7, 8), new(8, 9), new(9, 10), new(10, 11), new(11, 12), new(12, 13), new(13, 14), new(14, 15) }),
            Figure("Sagittarius", new Vector2[] { new(0.199f, -0.493f), new(0.251f, -0.156f), new(0.137f, 0.170f), new(0.500f, -0.200f), new(-0.153f, 0.054f), new(-0.310f, 0.105f), new(-0.500f, 0.004f), new(-0.430f, -0.160f), new(0.369f, 0.493f) },
                new float[] { 1.8f, 2.7f, 2.8f, 3.0f, 3.2f, 2.0f, 3.3f, 2.6f, 3.8f },
                new Vector2Int[] { new(3, 1), new(3, 0), new(1, 0), new(1, 2), new(2, 4), new(4, 1), new(4, 7), new(7, 0), new(4, 5), new(5, 6), new(6, 7), new(2, 8) }),
            Figure("Capricorn", new Vector2[] { new(0.500f, 0.345f), new(0.467f, 0.236f), new(0.185f, -0.266f), new(0.121f, -0.345f), new(-0.272f, -0.129f), new(-0.500f, 0.171f), new(-0.422f, 0.146f), new(-0.221f, 0.138f), new(-0.039f, 0.119f) },
                new float[] { 3.6f, 3.1f, 4.1f, 4.1f, 3.7f, 2.9f, 3.7f, 4.3f, 4.1f },
                new Vector2Int[] { new(0, 1), new(1, 2), new(2, 3), new(3, 4), new(4, 5), new(5, 6), new(6, 7), new(7, 8), new(8, 1) }),
            Figure("Aquarius", new Vector2[] { new(-0.027f, 0.188f), new(0.204f, 0.045f), new(-0.134f, 0.159f), new(-0.182f, 0.196f), new(-0.227f, 0.193f), new(-0.159f, 0.234f), new(-0.101f, -0.015f), new(-0.343f, -0.010f), new(-0.356f, -0.234f), new(0.500f, -0.062f), new(-0.031f, -0.181f), new(-0.489f, 0.032f), new(-0.500f, -0.051f) },
                new float[] { 2.9f, 2.9f, 3.8f, 3.6f, 4.0f, 4.7f, 4.2f, 3.7f, 3.3f, 3.8f, 4.3f, 4.2f, 4.2f },
                new Vector2Int[] { new(9, 1), new(1, 0), new(0, 2), new(2, 3), new(3, 4), new(2, 5), new(0, 6), new(6, 7), new(7, 11), new(11, 12), new(7, 8), new(1, 10) }),
            Figure("Pisces", new Vector2[] { new(-0.315f, -0.008f), new(-0.500f, -0.326f), new(-0.399f, -0.164f), new(-0.141f, -0.196f), new(-0.055f, -0.204f), new(0.245f, -0.222f), new(0.362f, -0.253f), new(0.434f, -0.234f), new(0.500f, -0.312f), new(0.441f, -0.363f), new(0.350f, -0.350f), new(-0.375f, -0.257f), new(-0.307f, -0.240f), new(-0.207f, -0.204f), new(-0.194f, 0.135f), new(-0.207f, 0.224f), new(-0.195f, 0.363f), new(-0.242f, 0.292f) },
                new float[] { 3.6f, 3.8f, 4.3f, 4.3f, 4.4f, 4.0f, 4.1f, 4.3f, 3.7f, 4.9f, 4.5f, 4.4f, 4.8f, 5.2f, 4.7f, 4.7f, 4.5f, 4.8f },
                new Vector2Int[] { new(8, 7), new(7, 6), new(6, 10), new(10, 9), new(9, 8), new(6, 5), new(5, 4), new(4, 3), new(3, 13), new(13, 12), new(12, 11), new(11, 1), new(1, 2), new(2, 0), new(0, 14), new(14, 15), new(15, 16), new(16, 17), new(17, 15) }),
        };

        public Constellation Get(int sign) =>
            figures == null || figures.Length == 0 ? null : figures[((sign % figures.Length) + figures.Length) % figures.Length];

        static Constellation Figure(string name, Vector2[] stars, float[] magnitudes, Vector2Int[] lines) =>
            new() { name = name, stars = stars, magnitudes = magnitudes, lines = lines };
    }
}
