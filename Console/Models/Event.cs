using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Console.Models
{
    public class Event
    {
        [JsonProperty("deviceType")]
        public string EventDeviceType { get; set; }

        [JsonProperty("deviceSerialNumber")]
        public string EventDeviceSerialNumber { get; set; }

        [JsonProperty("entryKey")]
        public string EventKey { get; set; }

        [JsonProperty("entryValue")]
        public string EventValue { get; set; }

        [JsonProperty("entryTimeStamp")]
        public DateTime CreationTime { get; set; }
    }
}
