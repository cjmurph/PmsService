using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace PlexMediaServer_Service
{
    [JsonObject(MemberSerialization.OptIn)]
    public class AuxiliaryApplication
    {
        [JsonProperty]
        public string Name { get; set; }

        [JsonProperty]
        public string FilePath { get; set; }

        [JsonProperty]
        public string Argument { get; set; }

        [JsonProperty]
        public bool KeepAlive { get; set; }

        public AuxiliaryApplication()
        {
            Name = string.Empty;
            FilePath = string.Empty;
            Argument = string.Empty;
            KeepAlive = true;
        }
    }
}
