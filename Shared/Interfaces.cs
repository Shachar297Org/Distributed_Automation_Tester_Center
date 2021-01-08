using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public interface IBackEndInterfaces
    {
        Task<bool> Connect(string url);

        Task<bool> AgentReady(string url);

        Task GetScriptLog(string url, string jsonContent);

        Task GetComparisonResults(string url, string jsonContent);

        Task Init();

        string TestCommand(string num);

        List<string> GetAgents();
        void Reset();
    }
}
