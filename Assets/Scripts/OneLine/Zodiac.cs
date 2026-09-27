using System.Collections.Generic;
using UnityEngine;

namespace OneLine
{
    /// <summary>
    /// The 12 zodiac houses. The full 500-level run is split evenly across them in zodiac order (~42 levels each);
    /// difficulty is unaffected — this is theme and naming only.
    /// </summary>
    public static class Zodiac
    {
        public enum Element { Fire, Earth, Air, Water }

        public const int SignCount = 12;

        public static readonly string[] Names =
        {
            "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo",
            "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces",
        };

        // Upper-case copies so the HUD never calls ToUpper at runtime.
        public static readonly string[] UpperNames =
        {
            "ARIES", "TAURUS", "GEMINI", "CANCER", "LEO", "VIRGO",
            "LIBRA", "SCORPIO", "SAGITTARIUS", "CAPRICORN", "AQUARIUS", "PISCES",
        };

        public static readonly string[] Epithets =
        {
            "The Ram", "The Bull", "The Twins", "The Crab", "The Lion", "The Maiden",
            "The Scales", "The Scorpion", "The Archer", "The Sea-Goat", "The Water-Bearer", "The Fishes",
        };

        /// <summary>Levels in the whole run (Difficulty: 50 tiers of 10).</summary>
        public static int TotalLevels => Difficulty.TierCount * Difficulty.LevelsPerTier;

        /// <summary>0 = Aries … 11 = Pisces. Levels past the run stay in Pisces.</summary>
        public static int SignOf(int levelNumber) =>
            Mathf.Clamp((levelNumber - 1) * SignCount / TotalLevels, 0, SignCount - 1);

        /// <summary>First level number of a sign.</summary>
        public static int FirstLevel(int sign) => Mathf.CeilToInt(sign * TotalLevels / (float)SignCount) + 1;

        public static bool IsFirstOfSign(int levelNumber) => levelNumber == FirstLevel(SignOf(levelNumber));

        /// <summary>Fire, earth, air, water, repeating from Aries.</summary>
        public static Element ElementOf(int sign) => (Element)(((sign % 4) + 4) % 4);
    }

    /// <summary>
    /// Zodiac and planet glyphs as vector strokes (in -1..1, y up). Drawn as geometry or rasterized, never as text,
    /// so they can't turn into color emoji on iOS / Android and need no symbol font.
    /// </summary>
    public static class ZodiacGlyphs
    {
        static List<Vector2[]>[] signs, planets;

        /// <summary>Strokes of a sign glyph (0 = ♈ Aries … 11 = ♓ Pisces).</summary>
        public static IReadOnlyList<Vector2[]> Sign(int sign)
        {
            signs ??= BuildSigns();
            return signs[((sign % 12) + 12) % 12];
        }

        public const int PlanetCount = 7;

        /// <summary>Strokes of a planet glyph: ☉ Sun, ☽ Moon, ☿ Mercury, ♀ Venus, ♂ Mars, ♃ Jupiter, ♄ Saturn.</summary>
        public static IReadOnlyList<Vector2[]> Planet(int planet)
        {
            planets ??= BuildPlanets();
            return planets[((planet % PlanetCount) + PlanetCount) % PlanetCount];
        }

        // ---------- shapes ----------

        static List<Vector2[]>[] BuildSigns()
        {
            var s = new List<Vector2[]>[12];

            // ♈ Aries: a stem splitting into two curled horns.
            s[0] = new()
            {
                Join(Bez(V(0, -0.8f), V(0, 0.35f), V(-0.12f, 0.75f), V(-0.45f, 0.75f)),
                     Bez(V(-0.45f, 0.75f), V(-0.75f, 0.75f), V(-0.9f, 0.45f), V(-0.72f, 0.22f))),
                Join(Bez(V(0, -0.8f), V(0, 0.35f), V(0.12f, 0.75f), V(0.45f, 0.75f)),
                     Bez(V(0.45f, 0.75f), V(0.75f, 0.75f), V(0.9f, 0.45f), V(0.72f, 0.22f))),
            };
            // ♉ Taurus: a circle with a crescent of horns.
            s[1] = new() { Arc(V(0, -0.3f), 0.48f, 0f, 360f, 48), Arc(V(0, 0.74f), 0.56f, 190f, 350f, 24) };
            // ♊ Gemini: two pillars between curved lintels.
            s[2] = new()
            {
                Line(V(-0.32f, -0.66f), V(-0.32f, 0.66f)), Line(V(0.32f, -0.66f), V(0.32f, 0.66f)),
                Bez(V(-0.75f, 0.85f), V(-0.3f, 0.6f), V(0.3f, 0.6f), V(0.75f, 0.85f)),
                Bez(V(-0.75f, -0.85f), V(-0.3f, -0.6f), V(0.3f, -0.6f), V(0.75f, -0.85f)),
            };
            // ♋ Cancer: two curled claws, one above the other, turned half around.
            s[3] = new()
            {
                Arc(V(-0.42f, 0.22f), 0.2f, 0f, 360f, 28), Bez(V(-0.42f, 0.42f), V(-0.2f, 0.85f), V(0.5f, 0.8f), V(0.82f, 0.35f)),
                Arc(V(0.42f, -0.22f), 0.2f, 0f, 360f, 28), Bez(V(0.42f, -0.42f), V(0.2f, -0.85f), V(-0.5f, -0.8f), V(-0.82f, -0.35f)),
            };
            // ♌ Leo: a small loop and the sweep of the mane.
            s[4] = new()
            {
                Arc(V(-0.5f, -0.35f), 0.22f, 0f, 360f, 28),
                Join(Bez(V(-0.344f, -0.194f), V(-0.1f, 0.3f), V(-0.2f, 0.85f), V(0.25f, 0.85f)),
                     Bez(V(0.25f, 0.85f), V(0.7f, 0.85f), V(0.65f, 0.35f), V(0.4f, 0f)),
                     Bez(V(0.4f, 0f), V(0.2f, -0.3f), V(0.4f, -0.75f), V(0.8f, -0.6f))),
            };
            // ♍ Virgo: an "m" whose last leg loops back.
            s[5] = new()
            {
                Line(V(-0.8f, 0.55f), V(-0.8f, -0.75f)),
                Join(Bez(V(-0.8f, 0.3f), V(-0.8f, 0.68f), V(-0.35f, 0.68f), V(-0.35f, 0.3f)), Line(V(-0.35f, 0.3f), V(-0.35f, -0.75f))),
                Join(Bez(V(-0.35f, 0.3f), V(-0.35f, 0.68f), V(0.1f, 0.68f), V(0.1f, 0.3f)), Line(V(0.1f, 0.3f), V(0.1f, -0.35f)),
                     Bez(V(0.1f, -0.35f), V(0.1f, 0.2f), V(0.75f, 0.2f), V(0.62f, -0.2f)),
                     Bez(V(0.62f, -0.2f), V(0.52f, -0.55f), V(0.28f, -0.75f), V(0.05f, -0.9f))),
            };
            // ♎ Libra: the setting sun above the horizon.
            s[6] = new()
            {
                Line(V(-0.8f, -0.5f), V(0.8f, -0.5f)),
                Join(Line(V(-0.8f, -0.12f), V(-0.272f, -0.12f)), Arc(V(0, 0.2f), 0.42f, 229.6f, -49.6f, 28, clockwise: true),
                     Line(V(0.272f, -0.12f), V(0.8f, -0.12f))),
            };
            // ♏ Scorpio: an "m" ending in the sting.
            s[7] = new()
            {
                Line(V(-0.8f, 0.55f), V(-0.8f, -0.75f)),
                Join(Bez(V(-0.8f, 0.3f), V(-0.8f, 0.68f), V(-0.35f, 0.68f), V(-0.35f, 0.3f)), Line(V(-0.35f, 0.3f), V(-0.35f, -0.75f))),
                Join(Bez(V(-0.35f, 0.3f), V(-0.35f, 0.68f), V(0.1f, 0.68f), V(0.1f, 0.3f)), Line(V(0.1f, 0.3f), V(0.1f, -0.5f)),
                     Bez(V(0.1f, -0.5f), V(0.1f, -0.8f), V(0.4f, -0.8f), V(0.78f, -0.8f))),
                Line(V(0.55f, -0.6f), V(0.78f, -0.8f), V(0.55f, -1f)),
            };
            // ♐ Sagittarius: the arrow with a crossbar.
            s[8] = new()
            {
                Line(V(-0.75f, -0.75f), V(0.75f, 0.75f)), Line(V(0.2f, 0.75f), V(0.75f, 0.75f), V(0.75f, 0.2f)),
                Line(V(-0.55f, -0.05f), V(-0.05f, -0.55f)),
            };
            // ♑ Capricorn: the goat's "V" running into the fish-tail loop.
            s[9] = new()
            {
                Join(Line(V(-0.85f, 0.55f), V(-0.55f, -0.55f)),
                     Bez(V(-0.55f, -0.55f), V(-0.4f, 0.2f), V(-0.2f, 0.7f), V(0.05f, 0.6f)),
                     Bez(V(0.05f, 0.6f), V(0.2f, 0.5f), V(0.15f, -0.2f), V(0.15f, -0.35f)),
                     Bez(V(0.15f, -0.35f), V(0.15f, -0.02f), V(0.78f, -0.02f), V(0.7f, -0.45f)),
                     Bez(V(0.7f, -0.45f), V(0.64f, -0.8f), V(0.22f, -0.85f), V(0.05f, -0.62f))),
            };
            // ♒ Aquarius: two waves.
            s[10] = new() { Wave(0.3f), Wave(-0.3f) };
            // ♓ Pisces: two fishes back to back, tied by a cord.
            s[11] = new()
            {
                Arc(V(-1.35f, 0f), 0.95f, -42f, 42f, 20), Arc(V(1.35f, 0f), 0.95f, 138f, 222f, 20), Line(V(-0.55f, 0f), V(0.55f, 0f)),
            };
            return s;
        }

        static List<Vector2[]>[] BuildPlanets()
        {
            var p = new List<Vector2[]>[PlanetCount];
            p[0] = new() { Arc(V(0, 0), 0.7f, 0f, 360f, 48), Arc(V(0, 0), 0.08f, 0f, 360f, 10) };                 // ☉
            p[1] = new() { Join(Arc(V(0, 0), 0.7f, -100f, 100f, 32), Arc(V(-0.4f, 0f), 0.744f, 67.9f, -67.9f, 24, clockwise: true)) }; // ☽
            p[2] = new()                                                                                               // ☿
            {
                Arc(V(0, 0.05f), 0.3f, 0f, 360f, 32), Arc(V(0, 0.62f), 0.28f, 200f, 340f, 16),
                Line(V(0, -0.25f), V(0, -0.85f)), Line(V(-0.25f, -0.58f), V(0.25f, -0.58f)),
            };
            p[3] = new() { Arc(V(0, 0.3f), 0.4f, 0f, 360f, 36), Line(V(0, -0.1f), V(0, -0.85f)), Line(V(-0.28f, -0.5f), V(0.28f, -0.5f)) }; // ♀
            p[4] = new() { Arc(V(-0.2f, -0.2f), 0.42f, 0f, 360f, 36), Line(V(0.097f, 0.097f), V(0.7f, 0.7f)),        // ♂
                           Line(V(0.3f, 0.7f), V(0.7f, 0.7f), V(0.7f, 0.3f)) };
            p[5] = new()                                                                                               // ♃
            {
                Join(Bez(V(-0.6f, 0.45f), V(-0.6f, 0.9f), V(0.1f, 0.85f), V(0f, 0.35f)), Line(V(0f, 0.35f), V(-0.65f, -0.3f), V(0.75f, -0.3f))),
                Line(V(0.35f, 0.5f), V(0.35f, -0.85f)),
            };
            p[6] = new()                                                                                               // ♄
            {
                Line(V(-0.3f, 0.85f), V(-0.3f, -0.6f)), Line(V(-0.6f, 0.55f), V(0f, 0.55f)),
                Join(Bez(V(-0.3f, 0.05f), V(-0.1f, 0.4f), V(0.5f, 0.4f), V(0.4f, -0.1f)),
                     Bez(V(0.4f, -0.1f), V(0.3f, -0.5f), V(0.3f, -0.8f), V(0.65f, -0.75f))),
            };
            return p;
        }

        // ---------- helpers ----------

        static Vector2 V(float x, float y) => new(x, y);

        static Vector2[] Line(params Vector2[] points) => points;

        static Vector2[] Wave(float y)
        {
            var w = new Vector2[6];
            for (int i = 0; i < 6; i++) w[i] = new Vector2(-0.85f + i * 0.34f, y + (i % 2 == 0 ? -0.15f : 0.15f));
            return w;
        }

        // Cubic Bezier sampled into 16 segments.
        static Vector2[] Bez(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int n = 16)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                pts[i] = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
            }
            return pts;
        }

        // Arc from a0 to a1 (degrees). Counter-clockwise unless clockwise is set.
        static Vector2[] Arc(Vector2 c, float r, float a0, float a1, int n, bool clockwise = false)
        {
            if (clockwise && a1 > a0) a1 -= 360f;
            if (!clockwise && a1 < a0) a1 += 360f;
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            return pts;
        }

        // Concatenates strokes into one, dropping the duplicated joint points.
        static Vector2[] Join(params Vector2[][] parts)
        {
            var list = new List<Vector2>();
            foreach (var part in parts)
                foreach (var p in part)
                    if (list.Count == 0 || (list[^1] - p).sqrMagnitude > 1e-6f) list.Add(p);
            return list.ToArray();
        }
    }
}
