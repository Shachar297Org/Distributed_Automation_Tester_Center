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

        void GetScriptResults(string url, string content);

        bool Init();

        string TestCommand(string num);
    }
}
