using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace PlexServiceCommon
{
    /// <summary>
    /// List of server side settings
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings
    {
        /// <summary>
        /// User defined auxilliary applications
        /// </summary>
        [JsonProperty]
        public List<AuxiliaryApplication> AuxiliaryApplications { get; set; }

        /// <summary>
        /// Drive mappings to create before starting plex
        /// </summary>
        [JsonProperty]
        public List<DriveMap> DriveMaps { get; set; }

        /// <summary>
        /// port the WCF service should listen on (endpoint port)
        /// </summary>
        [JsonProperty]
        public int ServerPort { get; set; }

        /// <summary>
        /// The plex restart delay
        /// </summary>
        [JsonProperty]
        public int RestartDelay { get; set; }

        /// <summary>
        /// Choose whether plex restarts if it stops
        /// </summary>
        [JsonProperty]
        public bool AutoRestart { get; set; }
        
        /// <summary>
        /// Choose whether to try auto-remounting shares if failed
        /// </summary>
        [JsonProperty]
        public bool AutoRemount { get; set; }

        /// <summary>
        /// How many times to try re-mounting shares before giving up
        /// </summary>
        [JsonProperty]
        public int AutoRemountCount { get; set; } = 5;

        public Settings()
        {
            AuxiliaryApplications = new List<AuxiliaryApplication>();
            DriveMaps = new List<DriveMap>();
            ServerPort = 8787;
            RestartDelay = 300;
        }

        /// <summary>
        /// Serialise the settings into a json formatted string
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, GetSettingsSerializerSettings());
        }

        /// <summary>
        /// Deserialise from a json formatted string to a Settings object
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static Settings Deserialize(string settings)
        {            
            return (Settings)JsonConvert.DeserializeObject(settings, typeof(Settings), GetSettingsSerializerSettings());
        }

        /// <summary>
        /// Default settings serialisation options
        /// </summary>
        /// <returns></returns>
        public static JsonSerializerSettings GetSettingsSerializerSettings()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Serialize
            };
            return serializerSettings;
        }
    }
}

