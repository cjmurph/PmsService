using System;
using System.IO;
using Newtonsoft.Json;
using PlexServiceCommon;
using Serilog;

namespace PlexServiceWCF
{
    /// <summary>
    /// Class for loading and saving settings on the server
    /// Code is here rather than in the settings class as it should only ever be save on the server.
    /// settings are retrieved remotely by calling the wcf service GetSettings and SetSettings methods
    /// </summary>
    public static class SettingsHandler
    {
        private static readonly object Sync = new();

        private static string GetSettingsFile()
        {
            return Path.Combine(TrayInteraction.AppDataPath, "Settings.json");
        }

        /// <summary>
        /// Save the settings file
        /// </summary>
        internal static void Save(Settings settings)
        {
            settings.Sanitize();
            var filePath = GetSettingsFile();
            lock (Sync)
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                // write to a temp file first so a crash can't leave an empty settings file behind
                var tempFile = filePath + ".tmp";
                File.WriteAllText(tempFile, JsonConvert.SerializeObject(settings, Formatting.Indented));
                if (File.Exists(filePath))
                {
                    File.Replace(tempFile, filePath, null);
                }
                else
                {
                    File.Move(tempFile, filePath);
                }
            }
        }

        /// <summary>
        /// Load the settings from disk
        /// </summary>
        /// <returns></returns>
        public static Settings Load()
        {
            var filePath = GetSettingsFile();
            Settings settings = null;
            lock (Sync)
            {
                if (File.Exists(filePath))
                {
                    try
                    {
                        settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(filePath));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Settings file is damaged, using defaults. {ex.Message}");
                        TryBackup(filePath);
                    }
                }
            }

            if (settings == null)
            {
                settings = new Settings();
                try
                {
                    Save(settings);
                }
                catch (Exception ex)
                {
                    Log.Warning("Unable to save default settings: " + ex.Message);
                }
            }

            settings.Sanitize();
            return settings;
        }

        private static void TryBackup(string filePath)
        {
            try
            {
                File.Copy(filePath, filePath + ".bad", true);
            }
            catch
            {
                // nothing we can do
            }
        }
    }
}
