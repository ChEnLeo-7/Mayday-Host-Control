using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace BepInEx
{
    internal static class Paths
    {
        internal static string GameRootPath => MelonEnvironment.GameRootDirectory;
        internal static string ConfigPath
        {
            get
            {
                var path = Path.Combine(MelonEnvironment.UserDataDirectory, "MaydayHostControl");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        internal static string LegacyBepInExConfigPath => Path.Combine(MelonEnvironment.GameRootDirectory, "BepInEx", "config");
    }
}

namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        private readonly string _name;

        internal ManualLogSource(string name) => _name = name;

        internal void LogInfo(object value) => MelonLogger.Msg("[" + _name + "] " + value);
        internal void LogWarning(object value) => MelonLogger.Warning("[" + _name + "] " + value);
        internal void LogError(object value) => MelonLogger.Error("[" + _name + "] " + value);
        internal void LogDebug(object value)
        {
            if (MaydayHostControl.Plugin.DebugLoggingEnabled) MelonLogger.Msg("[" + _name + "][Debug] " + value);
        }
    }
}

namespace BepInEx.Configuration
{
    internal sealed class ConfigEntry<T>
    {
        private readonly MelonPreferences_Entry<T> _entry;

        internal ConfigEntry(MelonPreferences_Entry<T> entry, bool existedAtBind)
        {
            _entry = entry;
            ExistedAtBind = existedAtBind;
        }

        internal bool ExistedAtBind { get; }

        internal T Value
        {
            get => _entry.Value;
            set => _entry.Value = value;
        }
    }

    internal sealed class ConfigFile
    {
        private readonly MelonPreferences_Category _category;

        internal ConfigFile(string prefix)
        {
            _category = MelonPreferences.CreateCategory(prefix, prefix);
        }

        internal ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
        {
            var entryId = section + "_" + key;
            var existed = _category.HasEntry(entryId);
            var entry = existed ? _category.GetEntry<T>(entryId) : _category.CreateEntry(entryId, defaultValue, description);
            return new ConfigEntry<T>(entry, existed);
        }

        internal void Save() => MelonPreferences.Save();
    }
}
