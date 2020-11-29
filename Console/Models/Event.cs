using System;
using System.Collections.Generic;
using System.Text;

namespace Console.Models
{
    public class Event
    {
        public string EventDeviceType { get; set; }

        public string EventSerialNumber { get; set; }

        public string EventKey { get; set; }

        public string EventValue { get; set; }

        public DateTime CreationTime { get; set; }
    }
}
