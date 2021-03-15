using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestCenter.Models
{
    public class AgentViewModel
    {
        public string Name { get; set; }

        public string IPAddress { get; set; }

        public string Port { get; set; }

        public BiotEnvironment Environment { get; set; }

        public string LogFolder { get; set; }

    }
}
