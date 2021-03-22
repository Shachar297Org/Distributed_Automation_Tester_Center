using System;
using System.Collections.Generic;
using System.IO;

namespace TestCenterConsole.Utilities
{
    public class Settings
    {       
        private static Dictionary<string, string> settingsDict = new Dictionary<string, string>();

        private static Settings _instance = null;

        private Settings(string configFilePath)
        {
            
            using (var streamReader = File.OpenText(configFilePath))
            {
                settingsDict["CONFIG_FILE"] = configFilePath;
                var lines = streamReader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] fields = line.Split('=');
                    settingsDict[fields[0]] = fields[1];
                }
            }

            settingsDict["ACCESS_TOKEN"] = string.Empty;
        }

        public static Settings GetInstance(string configFilePath)
        {
            if (_instance == null)
            {
                _instance = new Settings(configFilePath);
            }
            return _instance;            
        }

        private string Get(string key)
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

        private void Set(string key, string value)
        {
            settingsDict[key] = value;
        }

        public string this[string key]
        {
            get => Get(key);
            set => Set(key, value);
        }

    }
}
