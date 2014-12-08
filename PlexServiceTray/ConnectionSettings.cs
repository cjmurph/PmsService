using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace PlexServiceTray
{
    [JsonObject(MemberSerialization.OptIn)]
    internal class ConnectionSettings
    {
        #region Properties

        [JsonProperty]
        public string ServerAddress { get; set; }

        [JsonProperty]
        public int ServerPort { get; set; }

        #endregion

        #region Constructor

        private ConnectionSettings()
        {
            ServerAddress = "localhost";
            ServerPort = 8787;
        }

        #endregion

        public string getServiceAddress()
        {
            return string.Format("http://{0}:{1}/PlexService/", ServerAddress, ServerPort);
        }

        #region Load/Save

        internal static string GetSettingsFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\LocalSettings.json");
        }

        private static JsonSerializerSettings getSerializerSettings()
        {
            return PlexServiceCommon.Settings.GetSettingsSerializerSettings();
        }

        /// <summary>
        /// Save the settings file
        /// </summary>
        internal void Save()
        {
            string filePath = GetSettingsFile();

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            using (StreamWriter sw = new StreamWriter(filePath, false))
            {
                string rawSettings = JsonConvert.SerializeObject(this, getSerializerSettings());
                sw.Write(rawSettings);
            }
        }


        /// <summary>
        /// Load the settings from disk
        /// </summary>
        /// <returns></returns>
        internal static ConnectionSettings Load()
        {
            //serializer
            //JsonSerializer serializer = new JsonSerializer();

            string filePath = GetSettingsFile();
            ConnectionSettings settings = null;
            if (File.Exists(filePath))
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    string rawSettings = sr.ReadToEnd();
                    settings = (ConnectionSettings)JsonConvert.DeserializeObject(rawSettings, typeof(ConnectionSettings), getSerializerSettings());
                }
            }
            else
            {
                settings = new ConnectionSettings();
                settings.Save();
            }
            return settings;
        }

        #endregion
    }
}
