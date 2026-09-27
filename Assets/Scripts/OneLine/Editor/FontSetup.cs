using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OneLine.EditorTools
{
    /// <summary>
    /// Turns the TTF fonts in Assets/Fonts into TMP SDF font assets (Latin + Turkish characters) and assigns them to
    /// UIStyle: a file whose name starts with "Cinzel" or "Cormorant" becomes the title font, "Quicksand" or "Nunito"
    /// the body font. Glyphs the font lacks fall back to TMP's default font.
    /// </summary>
    public static class FontSetup
    {
        const string FontFolder = "Assets/Fonts";
        const string UIStylePath = "Assets/Settings/UIStyle.asset";

        static readonly string Characters =
            new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + // printable ASCII
            "çğıİöşüÇĞÖŞÜ" + "·—–’‘“”…•★";

        [MenuItem("One Line/Build UI Fonts")]
        public static string BuildFonts()
        {
            var style = AssetDatabase.LoadAssetAtPath<UIStyle>(UIStylePath);
            if (!style) return $"No UIStyle at {UIStylePath} — run 'One Line/Setup Levels + Scene' first.";
            if (!AssetDatabase.IsValidFolder(FontFolder)) return $"No {FontFolder} folder — put the TTF files there.";

            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                bool title = file.StartsWith("Cinzel") || file.StartsWith("Cormorant");
                bool body = file.StartsWith("Quicksand") || file.StartsWith("Nunito");
                if (!title && !body) continue;

                var asset = Build(path, file);
                if (title) style.titleFont = asset;
                else style.bodyFont = asset;
                log.Add($"{file} -> {(title ? "title" : "body")} font");
            }
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssets();
            string result = log.Count == 0 ? "No Cinzel/Cormorant/Quicksand/Nunito TTF found in Assets/Fonts." : string.Join(", ", log);
            Debug.Log("Build UI Fonts: " + result);
            return result;
        }

        static TMP_FontAsset Build(string ttfPath, string name)
        {
            string assetPath = $"{FontFolder}/{Sanitize(name)} SDF.asset";
            AssetDatabase.DeleteAsset(assetPath); // rebuild from scratch
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            asset.name = $"{Sanitize(name)} SDF";
            asset.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"{name}: characters not in the font (TMP fallback used): {missing}");
            asset.atlasPopulationMode = AtlasPopulationMode.Static; // glyphs baked; nothing changes at runtime
            if (TMP_Settings.defaultFontAsset)
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { TMP_Settings.defaultFontAsset };

            AssetDatabase.CreateAsset(asset, assetPath);
            foreach (var tex in asset.atlasTextures)
            {
                tex.name = asset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static string Sanitize(string s) => s.Replace("[", "").Replace("]", "").Replace(",", "-");
    }
}
