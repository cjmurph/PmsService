using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.IO;

namespace PlexMediaServer_Service
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

        #region Load/Save

        public static string GetSettingsFile()
        {
            return Path.Combine(PlexMediaServerService.APP_DATA_PATH, "Settings.json");
        }

        public void Save()
        {
            //serializer
            JsonSerializer serializer = new JsonSerializer();
            //Allow nulls references to be saved
            serializer.NullValueHandling = NullValueHandling.Ignore;
            //This allows all the DataTags and Enity references to remain their references after deserialization
            serializer.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            //This allow us to serialize and deserialize and array of abstract objects back into their original base types
            serializer.TypeNameHandling = TypeNameHandling.Auto;
            //This makes it look nice
            serializer.Formatting = Formatting.Indented;
            //Circular reference handling
            serializer.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;

            string filePath = GetSettingsFile();

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            using (StreamWriter sw = new StreamWriter(filePath, false))
            {
                using (JsonWriter writer = new JsonTextWriter(sw))
                {
                    serializer.Serialize(writer, this);
                }
            }
        }

        public static Settings Load()
        {
            //serializer
            JsonSerializer serializer = new JsonSerializer();
            //Allow nulls references to be saved
            serializer.NullValueHandling = NullValueHandling.Ignore;
            //This allows all the DataTags and Enity references to remain their references after deserialization
            serializer.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            //This allow us to serialize and deserialize and array of abstract objects back into their original base types
            serializer.TypeNameHandling = TypeNameHandling.Auto;
            //This makes it look nice
            serializer.Formatting = Formatting.Indented;
            //Circular reference handling
            serializer.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;

            string filePath = GetSettingsFile();
            Settings settings = null;
            if (File.Exists(filePath))
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    using (JsonReader reader = new JsonTextReader(sr))
                    {
                        settings = serializer.Deserialize(reader, typeof(Settings)) as Settings;
                    }
                }
            }
            else
            {
                settings = new Settings();
                settings.Save();
            }
            return settings;
        }

        #endregion
    }
}

