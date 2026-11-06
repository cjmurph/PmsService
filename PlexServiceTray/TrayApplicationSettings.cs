using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace PlexServiceTray
{
    /// <summary>
    /// Local settings for the tray application to connect to the WCF service
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal class TrayApplicationSettings
    {
        public const int DefaultPort = 8787;

        public const string SystemTheme = "System";

        public static readonly IReadOnlyList<string> BaseThemes = new[] { SystemTheme, "Light", "Dark" };

        #region Properties

        /// <summary>
        /// Address of the server running the wcf service
        /// </summary>
        [JsonProperty]
        public string ServerAddress { get; set; } = "localhost";

        /// <summary>
        /// port of the WCF service endpoint
        /// </summary>
        [JsonProperty]
        public int ServerPort { get; set; } = DefaultPort;

        public bool IsLocal => ServerAddress is "127.0.0.1" or "localhost" or "0.0.0.0" or "::1" ||
                               string.Equals(ServerAddress, Environment.MachineName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// System, Light or Dark (older versions stored values like Dark.Amber)
        /// </summary>
        [JsonProperty]
        public string Theme { get; set; } = SystemTheme;

        [JsonProperty]
        public bool ShowNotifications { get; set; } = true;

        #endregion

        public string BaseTheme
        {
            get
            {
                var value = Theme?.Split('.')[0];
                return value != null && BaseThemes.Contains(value) ? value : SystemTheme;
            }
        }

        /// <summary>
        /// Light or Dark, "System" is resolved from the Windows app mode
        /// </summary>
        public string ResolvedTheme => ResolveTheme(BaseTheme);

        public bool IsDark => ResolvedTheme == "Dark";

        public static string ResolveTheme(string baseTheme)
        {
            if (baseTheme == SystemTheme) baseTheme = AppsUseLightTheme() ? "Light" : "Dark";
            return baseTheme;
        }

        private static bool AppsUseLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Turn the properties into a useful endpoint uri
        /// </summary>
        public string GetServiceAddress()
        {
            return $"net.tcp://{ServerAddress}:{ServerPort}/PlexService/";
        }

        public TrayApplicationSettings Clone()
        {
            return (TrayApplicationSettings)MemberwiseClone();
        }

        #region Load/Save

        /// <summary>
        /// get the settings file location
        /// </summary>
        internal static string GetSettingsFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\LocalSettings.json");
        }

        /// <summary>
        /// Save the settings file
        /// </summary>
        internal void Save()
        {
            var filePath = GetSettingsFile();
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tempFile = filePath + ".tmp";
            File.WriteAllText(tempFile, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(filePath))
            {
                File.Replace(tempFile, filePath, null);
            }
            else
            {
                File.Move(tempFile, filePath);
            }
        }

        /// <summary>
        /// Load the settings from disk, a missing or broken file results in the defaults
        /// </summary>
        internal static TrayApplicationSettings Load()
        {
            TrayApplicationSettings settings = null;
            try
            {
                var filePath = GetSettingsFile();
                if (File.Exists(filePath))
                {
                    settings = JsonConvert.DeserializeObject<TrayApplicationSettings>(File.ReadAllText(filePath));
                }
            }
            catch
            {
                // fall back to defaults below
            }

            settings ??= new TrayApplicationSettings();
            if (string.IsNullOrWhiteSpace(settings.ServerAddress)) settings.ServerAddress = "localhost";
            if (settings.ServerPort is <= 0 or > 65535) settings.ServerPort = DefaultPort;
            return settings;
        }

        #endregion
    }
}
