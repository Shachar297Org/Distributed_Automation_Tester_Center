using System;
using System.Collections.Generic;
using System.Text;

namespace Console.Models
{
    public class Event
    {
        public string DeviceType { get; set; }

        public string DeviceSerialNumber { get; set; }

        public string EventKey { get; set; }

        public string EventValue { get; set; }

        public DateTime CreationTime { get; set; }
    }
}
