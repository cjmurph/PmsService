using System;
using System.IO;
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
        public string GetServiceAddress()
        {
            return $"net.tcp://{ServerAddress}:{ServerPort}/PlexService/";
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
        /// Save the settings file
        /// </summary>
        internal void Save()
        {
            var filePath = GetSettingsFile();

            if (!Directory.Exists(Path.GetDirectoryName(filePath))) {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) {
                    Directory.CreateDirectory(dir);    
                } else {
                    throw new DirectoryNotFoundException(dir);
                }
            }

            using var sw = new StreamWriter(filePath, false);
            var rawSettings = JsonConvert.SerializeObject(this);
            sw.Write(rawSettings);
        }


        /// <summary>
        /// Load the settings from disk
        /// </summary>
        /// <returns></returns>
        internal static ConnectionSettings Load()
        {
            var filePath = GetSettingsFile();
            ConnectionSettings settings;
            if (File.Exists(filePath)) {
                using var sr = new StreamReader(filePath);
                var rawSettings = sr.ReadToEnd();
                settings = JsonConvert.DeserializeObject<ConnectionSettings>(rawSettings);
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
