using System;
using System.Collections.Generic;

namespace Console.Utilities
{
    public static class Settings
    {
        public static Dictionary<string, string> settingsDict = new Dictionary<string, string>();

        public static string Get(string key)
        {
            if (settingsDict.ContainsKey(key))
            {
                return settingsDict[key];
            }
            else
            {
                throw new Exception($"Setting key {key} does not exist");
            }
        }
    }
}
