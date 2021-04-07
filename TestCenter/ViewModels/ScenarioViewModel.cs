using Console.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestCenter.Models
{
    public class ScenarioViewModel
    {
        public string Name { get; set; }

        public int DevicesNumber { get; set; }

        public InsertionStrategy InsertionStrategy { get; set; }

        public int MinutesToWaitForAgents { get; set; }

        public IEnumerable<AWSServices> Services { get; set; }

        public int MinutesServicesStopped { get; set; }
    }
}
