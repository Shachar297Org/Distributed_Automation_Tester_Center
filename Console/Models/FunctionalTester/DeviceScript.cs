using Console.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using TestCenterConsole.Models;

namespace Console.Models.FunctionalTester
{
    public class DeviceScript : IExecutionScript
    {
        public LumenisXDevice Device { get; set; }

        public void Pause()
        {
            throw new NotImplementedException();
        }

        public void Resume()
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            
        }
    }
}
