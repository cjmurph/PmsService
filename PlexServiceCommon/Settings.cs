using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace PlexServiceCommon
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings
    {
        [JsonProperty]
        public List<AuxiliaryApplication> AuxiliaryApplications { get; set; }

        public Settings()
        {
            AuxiliaryApplications = new List<AuxiliaryApplication>();
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, GetSettingsSerializerSettings());
        }

        public static Settings Deserialize(string settings)
        {            
            return (Settings)JsonConvert.DeserializeObject(settings, typeof(Settings), GetSettingsSerializerSettings());
        }

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

