using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestCenter.Models
{
    public class AgentMeasurementsData
    {
        public string AgentName { get; set; }

        public int ClientsRunnng { get; set; }

        public int ServersRunning { get; set; }

        public Stage Stage { get; set; }

    }

    public class MeasurementData
    {
        public IEnumerable<AWSData> CPUUtilization { get; set; }

        public IEnumerable<AWSData> MemoryUtilization { get; set; }

        public int EventsInRDS { get; set; }

        public IEnumerable<AgentMeasurementsData> AgentsData { get; set; }

    }

    public class AWSData
    {
        public DateTime Time { get; set; }
        public int Value { get; set; }
    }
}
