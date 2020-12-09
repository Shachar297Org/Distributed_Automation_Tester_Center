using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public interface IBackEndInterfaces
    {
        bool Connect(string url);

        void AgentReady(string url);

        void GetScriptLog(string url, string jsonContent);

        void GetComparisonResults(string url, string jsonContent);

        bool Init();

        string TestCommand(string num);
    }
}
