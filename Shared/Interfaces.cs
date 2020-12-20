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

        void GetScriptLog(string url, string jsonContent);

        void GetComparisonResults(string url, string jsonContent);

        void Init();

        string TestCommand(string num);
    }
}
