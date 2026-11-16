using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace MaydayHostControl
{
    internal static class Loc
    {
        internal static readonly string[] LanguageCodes = { "zh-CN", "en", "fr", "de", "es", "pt-BR", "tr", "ru", "ja", "ko", "pl" };
        internal static readonly string[] LanguageNames = { "简体中文", "English", "Français", "Deutsch", "Español", "Português (Brasil)", "Türkçe", "Русский", "日本語", "한국어", "Polski" };
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = LoadTables();
        internal static int CurrentIndex => Math.Max(0, Array.IndexOf(LanguageCodes, Plugin.PanelLanguage?.Value ?? "en"));
        internal static bool TryNormalizeLanguage(string value, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var normalized = value.Trim().Replace('_', '-');
            if (string.Equals(normalized, "zh", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
                normalized = "zh-CN";
            else if (string.Equals(normalized, "pt", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith("pt-", StringComparison.OrdinalIgnoreCase))
                normalized = "pt-BR";
            else if (normalized.Length >= 2)
                normalized = normalized.Substring(0, 2).ToLowerInvariant();
            code = LanguageCodes.FirstOrDefault(candidate => string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase));
            return code != null;
        }

        internal static string FromSystemLanguage(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "en";
            switch (value.Trim().ToLowerInvariant())
            {
                case "chinese": case "simplifiedchinese": case "traditionalchinese": return "zh-CN";
                case "french": return "fr";
                case "german": return "de";
                case "spanish": return "es";
                case "portuguese": return "pt-BR";
                case "turkish": return "tr";
                case "russian": return "ru";
                case "japanese": return "ja";
                case "korean": return "ko";
                case "polish": return "pl";
                default: return "en";
            }
        }
        private static Dictionary<string, Dictionary<string, string>> LoadTables()
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            var assembly = typeof(Loc).Assembly;
            foreach (var code in LanguageCodes)
            {
                using var stream = assembly.GetManifestResourceStream("MaydayHostControl.Locales." + code + ".json");
                if (stream != null) result[code] = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
            }
            return result;
        }
        internal static string T(string key, params object[] args)
        {
            if (key == null) return null;
            var code = LanguageCodes[CurrentIndex];
            var value = Tables.TryGetValue(code, out var table) && table.TryGetValue(key, out var translated) ? translated : key;
            return args.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, args);
        }
        internal static string RecoveryMessage(string value)
        {
            if (value == null) return null;
            if (RecoveryKeys.TryGetValue(value, out var key)) return T(key);
            // Backend details remain in logs; show a translated summary rather than untranslated internals.
            if (value.Any(c => c >= '\u3400' && c <= '\u9fff')) return T("Recovery details unavailable");
            return T(value);
        }
        private static readonly Dictionary<string, string> RecoveryKeys = new()
        {
            ["尚未恢复"] = "No recovery yet",
            ["恢复不完整"] = "Recovery incomplete",
            ["恢复未完整完成"] = "Recovery incomplete",
            ["场景已恢复，但至少一名玩家的背包恢复超时；请查看诊断日志"] = "Some player inventories could not be restored",
            ["场景和所有已保存玩家均已恢复"] = "Recovery completed",
            ["恢复已取消：服务器会话已变化"] = "Recovery cancelled: session changed",
            ["IDLE"] = "No recovery yet", ["SUCCEEDED"] = "Recovery completed",
            ["PLAYER_PENDING"] = "Waiting for player recovery", ["PLAYER_PARTIAL"] = "Some player inventories could not be restored",
            ["FATAL"] = "Recovery failed; create a new room", ["CANCELLED"] = "Recovery cancelled: session changed",
            ["WAITING_RUNTIME"] = "Waiting for game scene", ["WAITING_AIRPORT"] = "Waiting for game scene", ["APPLYING"] = "Restoring game"
        };
    }
}
