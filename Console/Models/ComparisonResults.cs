using System;
using System.Collections.Generic;
using System.Text;

namespace Console.Models
{
    public class ComparisonResults
    {
        public string DeviceName { get; set; }
        public List<Event> Events { get; set; }
    }

    public class EventsLog
    {
        public string DeviceName { get; set; }
        public string EventsJson { get; set; }
        public string Port { get; set; }
    }
}
