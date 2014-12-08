using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using PlexServiceCommon;

namespace PlexServiceWCF
{
    internal static class SettingsHandler
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
            ////serializer
            //JsonSerializer serializer = new JsonSerializer();
            ////Allow nulls references to be saved
            //serializer.NullValueHandling = NullValueHandling.Ignore;
            ////This allows all the DataTags and Enity references to remain their references after deserialization
            //serializer.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            ////This allow us to serialize and deserialize and array of abstract objects back into their original base types
            //serializer.TypeNameHandling = TypeNameHandling.Auto;
            ////This makes it look nice
            //serializer.Formatting = Formatting.Indented;
            ////Circular reference handling
            //serializer.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;

            string filePath = GetSettingsFile();

            if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            }
            using (StreamWriter sw = new StreamWriter(filePath, false))
            {
                string rawSettings = settings.Serialize();
                sw.Write(rawSettings);
                //using (JsonWriter writer = new JsonTextWriter(sw))
                //{
                //    serializer.Serialize(writer, settings);
                //}
            }
        }


        /// <summary>
        /// Load the settings from disk
        /// </summary>
        /// <returns></returns>
        internal static Settings Load()
        {
            //serializer
            //JsonSerializer serializer = new JsonSerializer();

            string filePath = GetSettingsFile();
            Settings settings = null;
            if (File.Exists(filePath))
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    string rawSettings = sr.ReadToEnd();
                    settings = Settings.Deserialize(rawSettings);
                    //using (JsonReader reader = new JsonTextReader(sr))
                    //{
                    //    settings = serializer.Deserialize(reader, typeof(Settings)) as Settings;
                    //}
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
