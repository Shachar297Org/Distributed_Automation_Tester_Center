using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestCenterConsole.Models;

namespace TestCenterConsole.Models
{
    public class AgentMeasurementsData
    {
        public string AgentName { get; set; }

        public int ClientsRunnng { get; set; }

        public int ServersRunning { get; set; }

        public Stage Stage { get; set; }

    }

    public class AwsMetricsData
    {
        public AWSData[] CPUUtilization { get; set; }

        public AWSData[] MemoryUtilization { get; set; }

        public int EventsInRDS { get; set; }

    }

    public class ProgressData
    {
        public ProgressData(string scenarioName="-", int numDevices = 0)
        {
            AgentsData = new List<AgentMeasurementsData>();
            AwsMetricsData = new List<AwsMetricsData>();
            ScenarioName = scenarioName;
            StageData = new List<StageData>
            {
                new StageData { 
                    Stage = "-",
                    Time = DateTime.MinValue,
                    DevicesNumberTotal = numDevices
                }
            };
        }

        public List<AgentMeasurementsData> AgentsData { get; set; }

        public List<AwsMetricsData> AwsMetricsData { get; set; }

        public string ScenarioName { get; set; }

        public List<StageData> StageData { get; set; }

    }

    public class StageData
    {
        public string Stage { get; set; }

        public Stage StageIdx { get; set; }

        public int DevicesNumberTotal { get; set; }

        public int DevicesNumberFinished { get; set; }

        public DateTime Time { get; set; }
    }

    public class AWSData
    {
        public DateTime Time { get; set; }
        public int Value { get; set; }
    }
}
