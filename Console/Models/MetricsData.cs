using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TestCenterConsole.Models;

namespace TestCenterConsole.Models
{
    public enum AgentStatus
    {
        LIVE,
        OFFLINE,
        INIT,
        CREATING_DEVICE_FOLDERS,
        RUNNING,
        FINISHED
    }

    public class AgentData
    {
        public AgentData()
        {
            Devices = new List<LumenisXDevice>();
        }

        public int ServersNumber { get; set; }
        public int ClientsNumber { get; set; }

        public List<LumenisXDevice> Devices { get; set; }
        
        public string Status { get; set; }

        public string URL { get; set; }
        public int TotalEvents { get; set; }

        public bool IsReady { get; set; }
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
            AgentsData = new List<AgentData>();
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

        public List<AgentData> AgentsData { get; set; }

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

        public bool IsNewStage { get; set; }
    }

    public class AWSData
    {
        public DateTime Time { get; set; }
        public int Value { get; set; }
    }
}
