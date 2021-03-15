using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Console.Models;
using TestCenterConsole.Models;

namespace Console.Interfaces
{
    public interface IBackEndInterface
    {
        Task<bool> Connect(string url);

        Task<bool> AgentReady(string url);

        Task GetScriptLog(string url, string jsonContent);

        Task GetComparisonResults(string url, string jsonContent);

        Task GetComparisonResults(string url, EventsLog events);

        Task Init();

        event EventHandler<AwsMetricsData> AwsDataUpdated;
        event EventHandler<StageData> StageDataUpdated;

        List<string> GetAgents();
        
        void UpdateScenarioSettings(Scenario scenario);
        void UpdateCenterSettings(TestCenterSettings settings);

        void Reset();

    }
}
