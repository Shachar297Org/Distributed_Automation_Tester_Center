using System;
using System.Collections.Generic;
using System.Text;

namespace Console
{
    public class Agent
    {
        public Agent(string ip, int port, bool isReady)
        {
            AgentIP = ip;
            AgentPort = port;
            IsReady = isReady;
            URL = string.Join(":", ip, port);
        }

        public string AgentIP { get; set; }

        public int AgentPort { get; set; }

        public bool IsReady { get; set; }

        public string URL { get; set; }
    }
}
