using System;
using System.Collections.Generic;
using System.Text;

namespace Console.Models
{
    public class ScriptLog
    {
        public ScriptLog(string deviceName, string content)
        {
            DeviceName = deviceName;
            Content = content;
        }

        public string DeviceName { get; set; }
        public string Content { get; set; }
    }
}
