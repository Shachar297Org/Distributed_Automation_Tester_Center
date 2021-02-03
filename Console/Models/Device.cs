using System;
using System.Collections.Generic;
using System.Text;

namespace Console.Models
{
    public class LumenisXDevice
    {
        public LumenisXDevice(string ga, string sn)
        {
            DeviceType = ga;
            DeviceSerialNumber = sn;
        }

        public string DeviceType { get; set; }
        public string DeviceSerialNumber { get; set; }
    }
}
