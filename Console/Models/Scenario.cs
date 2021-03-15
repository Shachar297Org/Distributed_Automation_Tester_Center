using System;
using System.Collections.Generic;
using System.Text;
using TestCenterConsole.Models;

namespace Console.Models
{
    public class Scenario
    {
        public string Name { get; set; }

        public int DevicesNumber { get; set; }

        public string DevicesType { get; set; }

        public InsertionStrategy InsertionStrategy { get; set; }

        public int MinutesToWaitForAgents { get; set; }

        public bool? StopAws { get; set; }

        public IEnumerable<AWSServices> Services { get; set; }

        public int MinutesServicesStopped { get; set; }
    }
}
