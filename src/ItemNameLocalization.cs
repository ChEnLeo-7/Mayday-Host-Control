using System;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;

namespace MaydayHostControl
{
    internal static class ItemNameLocalization
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new(StringComparer.Ordinal);

        internal static string Get(string key, string id, string language)
        {
            if (!string.IsNullOrEmpty(key))
            {
                if (Table(language).TryGetValue(key, out var name)) return name;
                if (language != "en" && Table("en").TryGetValue(key, out name)) return name;
            }
            return id ?? string.Empty;
        }

        private static Dictionary<string, string> Table(string language)
        {
            if (Tables.TryGetValue(language, out var cached)) return cached;
            // Resource folders use the game's language identifiers, not the panel's locale codes.
            var folder = language switch
            {
                "zh-CN" => "schinese", "fr" => "french", "de" => "german", "es" => "spanish",
                "pt-BR" => "brazilian", "ru" => "russian", "ja" => "japanese", "ko" => "koreana",
                "pl" => "polish", _ => null
            };
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            var path = folder == null ? "Loc" : "Loc/" + folder;
            try
            {
                foreach (var asset in Resources.LoadAll<TextAsset>(path))
                {
                    if (asset == null) continue;
                    try
                    {
                        using var document = JsonDocument.Parse(asset.text);
                        var root = document.RootElement;
                        if (root.ValueKind != JsonValueKind.Object) continue;
                        if (folder == null)
                        {
                            // Base files store English and Turkish in rows. Ignore any nested overlays.
                            if (!root.TryGetProperty("entries", out var rows) || rows.ValueKind != JsonValueKind.Array) continue;
                            foreach (var row in rows.EnumerateArray())
                                if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty("k", out var key) &&
                                    key.ValueKind == JsonValueKind.String && row.TryGetProperty(language, out var value))
                                    Add(result, key.GetString(), value);
                        }
                        else
                        {
                            foreach (var property in root.EnumerateObject()) Add(result, property.Name, property.Value);
                        }
                    }
                    catch (JsonException e)
                    {
                        Plugin.Log?.LogWarning("Invalid item translation resource " + path + "/" + asset.name + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.LogWarning("Unable to load item translations from " + path + ": " + e.Message);
            }
            if (result.Count > 0) Tables[language] = result;
            else Plugin.Log?.LogWarning("No item translations found for panel language " + language + " in " + path);
            return result;
        }

        private static void Add(Dictionary<string, string> table, string key, JsonElement value)
        {
            if (key == null || !key.StartsWith("item.", StringComparison.Ordinal) || value.ValueKind != JsonValueKind.String) return;
            var name = value.GetString();
            if (!string.IsNullOrWhiteSpace(name)) table[key] = name;
        }
    }
}
