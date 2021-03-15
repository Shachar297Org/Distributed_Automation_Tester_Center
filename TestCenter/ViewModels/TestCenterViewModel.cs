using Console.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using TestCenterConsole.Models;

namespace TestCenter.ViewModels
{
    public enum AgentStatus
    {
        LIVE,
        OFFLINE,
        RUNNING
    }

    public enum ScenarioStatus
    {
        READY,
        EXECUTING,
        FINISHED
    }

    public class TestCenterViewModel
    {
        public TestCenterViewModel()
        {
            Agents = new List<AgentViewModel>();
            Scenarios = new List<ScenarioViewModel>();
            ProgressData = new ProgressData();
        }

        public string IPAddress { get; set; }

        public string Port { get; set; }

        [EnumDataType(typeof(BiotEnvironment))]
        public BiotEnvironment Environment { get; set; }

        public string LogFilePath { get; set; }

        public string ScriptResultsFolder { get; set; }

        public string ResultsFolder { get; set; }

        public AgentStatus Status { get; set; }

        public List<AgentViewModel> Agents { get; set; }

        public List<ScenarioViewModel> Scenarios { get; set; }

        public ProgressData ProgressData { get; set; }

    }

    public class AgentViewModel
    {
        public int? Id { get; set; }

        public string Name { get; set; }

        public string IPAddress { get; set; }

        public int Port { get; set; }

        [EnumDataType(typeof(BiotEnvironment))]
        public BiotEnvironment Environment { get; set; }

        public string AgentDirPath { get; set; }

        public AgentStatus Status { get; set; }

    }

    public class ScenarioViewModel
    {
        public ScenarioViewModel()
        {
            Status = ScenarioStatus.READY;
        }

        public int? Id { get; set; }

        public string Name { get; set; }

        public int DevicesNumber { get; set; }

        public string DevicesType { get; set; }

        [EnumDataType(typeof(InsertionStrategy))]
        public InsertionStrategy InsertionStrategy { get; set; }

        public int MinutesToWaitForAgents { get; set; }

        public bool StopAws { get; set; }

        public IEnumerable<AWSServices> Services { get; set; }

        public int MinutesServicesStopped { get; set; }

        public ScenarioStatus Status { get; set; }

    }


}
