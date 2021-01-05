using System;
using System.Collections.Generic;
using System.Text;

namespace Console.Models
{
    public class Event
    {
        public Event(string ga, string sn, string ekey, string evalue, DateTime time)
        {
            EventDeviceType = ga;
            EventDeviceSerialNumber = sn;
            EventKey = ekey;
            EventValue = evalue;
            CreationTime = time;
        }


  
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
