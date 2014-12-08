using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace PlexServiceTray
{
    /// <summary>
    /// Local settings for the tray application to connect to the WCF service
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal class ConnectionSettings
    {
        #region Properties

        /// <summary>
        /// Address of the server running the wcf service
        /// </summary>
        [JsonProperty]
        public string ServerAddress { get; set; }

        /// <summary>
        /// port of the WCF service endpoint
        /// </summary>
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

        /// <summary>
        /// Turn the properties into a useful endpoint uri
        /// </summary>
        /// <returns></returns>
        public string getServiceAddress()
        {
            return string.Format("http://{0}:{1}/PlexService/", ServerAddress, ServerPort);
        }

        #region Load/Save

        /// <summary>
        /// get the settings file location
        /// </summary>
        /// <returns></returns>
        internal static string GetSettingsFile()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Plex Service\LocalSettings.json");
        }

        /// <summary>
        /// Get the common serialiser settings (shared with server settings)
        /// </summary>
        /// <returns></returns>
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
