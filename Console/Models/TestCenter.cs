using System;
using System.Collections.Generic;
using System.Text;
using TestCenterConsole.Models;

namespace TestCenterConsole.Models
{
    public class TestCenterSettings
    {
        public BiotEnvironment Environment { get; set; }

        public string LogFilePath { get; set; }

        public string ScriptResultsFolder { get; set; }

        public string ResultsFolder { get; set; }
    }
}
