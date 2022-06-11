using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace PlexServiceCommon
{
    /// <summary>
    /// List of server side settings
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Settings
    {
        /// <summary>
        /// User defined auxiliary applications
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
        [DefaultValue(8787)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int ServerPort { get; set; }

        /// <summary>
        /// The plex restart delay
        /// </summary>
        [DefaultValue(5)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int RestartDelay { get; set; }

        /// <summary>
        /// Choose whether plex restarts if it stops
        /// </summary>
        [DefaultValue(false)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool AutoRestart { get; set; }

        /// <summary>
        /// Choose whether to try auto-remounting shares if failed
        /// </summary>
        [DefaultValue(true)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool AutoRemount { get; set; }

        /// <summary>
        /// How many times to try re-mounting shares before giving up
        /// </summary>
        [DefaultValue(5)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int AutoRemountCount { get; set; }
        
        /// <summary>
        /// Delay (in seconds) to wait before attempting to re-mount
        /// </summary>
        [DefaultValue(5)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public int AutoRemountDelay { get; set; }
        
        /// <summary>
        /// Should plex be started if mounting fails? Defaults to true (original behavior)
        /// </summary>
        [DefaultValue(true)]            
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool StartPlexOnMountFail { get; set; }

        public Settings()
        {
            AuxiliaryApplications = new List<AuxiliaryApplication>();
            DriveMaps = new List<DriveMap>();
        }

    }
}

