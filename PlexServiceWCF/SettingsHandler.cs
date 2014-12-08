using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PlexServiceCommon;

namespace PlexServiceWCF
{
    /// <summary>
    /// Class for loading and saving settings on the server
    /// Code is here rather than in the settings class as it should only ever be save on the server.
    /// settings are retrieved remotely by calling the wcf service getsettings and setsettings methods
    /// </summary>
    public static class SettingsHandler
    {
        #region Load/Save

        internal static string GetSettingsFile()
        {
            return Path.Combine(TrayInteraction.APP_DATA_PATH, "Settings.json");
        }

        /// <summary>
        /// Save the settings file
        /// </summary>
        internal static void Save(Settings settings)
        {
            string filePath = GetSettingsFile();

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            using (StreamWriter sw = new StreamWriter(filePath, false))
            {
                string rawSettings = settings.Serialize();
                sw.Write(rawSettings);
            }
        }


        /// <summary>
        /// Load the settings from disk
        /// </summary>
        /// <returns></returns>
        public static Settings Load()
        {
            string filePath = GetSettingsFile();
            Settings settings = null;
            if (File.Exists(filePath))
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    string rawSettings = sr.ReadToEnd();
                    settings = Settings.Deserialize(rawSettings);
                }
            }
            else
            {
                settings = new Settings();
                Save(settings);
            }
            return settings;
        }

        #endregion
    }
}
