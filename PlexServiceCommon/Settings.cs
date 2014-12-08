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
        /// port the WCF service should listen on (endpoint port)
        /// </summary>
        [JsonProperty]
        public int ServerPort { get; set; }

        public Settings()
        {
            AuxiliaryApplications = new List<AuxiliaryApplication>();
            ServerPort = 8787;
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

