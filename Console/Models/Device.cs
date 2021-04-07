using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestCenterConsole.Models
{
    public class LumenisXDevice
    {
        public LumenisXDevice(string ga, string sn)
        {
            DeviceType = ga;
            DeviceSerialNumber = sn;
            Finished = false;
            Success = true;
        }

        [JsonProperty("deviceType")]
        public string DeviceType { get; set; }

        [JsonProperty("deviceSerialNumber")]
        public string DeviceSerialNumber { get; set; }

        public bool Finished { get; set; }
        public int EventsInRDS { get; set; }
        public bool Success { get; set; }
    }
}
