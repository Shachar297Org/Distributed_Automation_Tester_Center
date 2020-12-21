using Console;
using Console.Models;
using Console.Utilities;
using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Backend
{
    public class BackEnd : IBackEndInterfaces
    {
        System.Timers.Timer _getAgentConnectTimer = new System.Timers.Timer(new TimeSpan(0, 0, 30).TotalMilliseconds);
        System.Timers.Timer _getAgentReadyTimer = new System.Timers.Timer(new TimeSpan(0, 1, 0).TotalMilliseconds);
        private static List<Agent> _agents = new List<Agent>();
        List<Device> _devices = null;
        private static readonly object _lock = new object();
        //Logger _logger;

        /// <summary>
        /// Initialize backend:
        /// Create two timers - first agent connection and ready timers
        /// Init agent list
        /// Insert devices to portal
        /// Collect AWS services and instances
        /// </summary>
        public void Init()
        {
            Utils.LoadConfig();
            //_logger = new Logger(Settings.Get("LOG_FILE_PATH"));
            try
            {
                //int connectTimerSec = int.Parse(Settings.Get("CONNECT_TIMER_SEC"));
                //int readyTimerSec = int.Parse(Settings.Get("READY_TIMER_SEC"));
               // _getAgentConnectTimer = new System.Timers.Timer(new TimeSpan(0, 0, connectTimerSec).TotalMilliseconds);
               // _getAgentReadyTimer = new System.Timers.Timer(new TimeSpan(0, 0, readyTimerSec).TotalMilliseconds);
                _getAgentConnectTimer.Elapsed += GetAgentConnectTimer_Elapsed;
                _getAgentReadyTimer.Elapsed += GetAgentReadyTimer_Elapsed;

                _getAgentConnectTimer.Start();
                Utils.WriteLog("Starting connect timer", "info");

                //InitAgents(agentsFile);
                InsertDevicesToPortal(Settings.Get("CONFIG_FILE"));
                //InsertDevicesToPortal(Settings.Get("ENV"), Settings.Get("DEVICES_PATH"));
                //CollectAWSServices(Settings.Get("ENV"));
                //CollectAWSInstances();
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error:{ex.Message} {ex.Source} {ex.StackTrace}", "error");
            }
            

            /*
             * 1. get device list from portal (BIOT API) - done [insert_devices.py]
             * 2. insert new devices that are not exist in the portal (BIOT API) - done
             * 3. wait 2 min for agent to connect - done
             * 4. after 2 min divide device list according to agent number and prepare list of devices per agent - done
             * 5. send device list to all agents and wait 2 min until agent signal that they finished to create all the devices. [distribute_devices.py] - done
             *    the agent will signal by using the API  "AgentReady" - done
             * 6. after waiting 2 minutes itereate agent list and map only the agent that are ready. - done
             * 7. send only to the ready agents the automation script - done
             * 8. T.B.D
             * */
        }

        /// <summary>
        /// Distribute devices among devices and wait for agents to be ready
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">event</param>
        private void GetAgentConnectTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {  
            _getAgentConnectTimer.Stop();
            Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));
            DistributeDevicesAmongAgents();
            Utils.WriteLog($"Starting agent ready timer", "info");
            _getAgentReadyTimer.Start();
        }

        /// <summary>
        /// Send automation script to ready agents only
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">event</param>
        private void GetAgentReadyTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _getAgentReadyTimer.Stop();
            Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));
            SendAutomationScript();
        }

        /// <summary>
        /// Connect to test center and register the reqested agent
        /// </summary>
        /// <param name="agentUrl">agent URL</param>
        /// <returns>true/false if connection succeeded</returns>
        public async Task<bool> Connect(string agentUrl)
        {
            Utils.LoadConfig();
            //_logger = new Logger(Settings.Get("LOG_FILE_PATH"));
            try
            {
                string agentsPath = Settings.Get("AGENTS_PATH");
                Utils.WriteLog($"Received agent connect from {agentUrl}.", "info");
                string agentsFilePath = Settings.Get("AGENTS_PATH");

                Utils.WriteLog($"Agent {agentUrl} is connecting.", "info");

                // lock code
                lock (_lock)
                {
                    // Init agents file when first agent is connecting
                    //_agents = Utils.ReadAgentsFromFile(agentsFilePath);

                    Utils.WriteLog($"Current agents number: {_agents.Count}.", "info");

                    if (_agents.Count == 0)
                    {
                        Init();
                    }
    
                    Agent agent = _agents.Find(a => a.URL == agentUrl);
                    if (agent != null)
                    {
                        Utils.WriteLog($"Agent {agentUrl} already exists.", "info");
                    }
                    else
                    {
                        if (Settings.Get("MODE").ToLower() == "debug")
                        {
                            agentUrl = agentUrl.Replace("127.0.0.1", "localhost");
                        }
                        string[] urlStrings = agentUrl.Split(':');
                        Utils.WriteLog($"Adding agent {agentUrl} to pool.", "info");
                        _agents.Add(new Agent(urlStrings[0], int.Parse(urlStrings[1]), false));
                        Utils.WriteLog($"Agents count: {_agents.Count}.", "info");
                        //Utils.WriteAgentListToFile(_agents, agentsFilePath);
                    }
                }

                await Task.Delay(10);
                return true;
            }
            catch(Exception ex)
            {
                Utils.WriteLog($"Error in connect: {ex.Message} {ex.StackTrace}", "error");
                return false;
            }
        }

        /// <summary>
        /// Updates requested agent status to Ready
        /// </summary>
        /// <param name="url">agent URL</param>
        public async Task<bool> AgentReady(string url)
        {
            Utils.LoadConfig();
            //_logger = new Logger(Settings.Get("LOG_FILE_PATH"));
            string agentsFilePath = Settings.Get("AGENTS_PATH");

            try
            {
                if (Settings.Get("MODE").ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost");
                }

                Utils.WriteLog($"Received agent ready from {url}.", "info");

                // lock code
                lock (_lock)
                {
                    //_agents = Utils.ReadAgentsFromFile(agentsFilePath);
                    Agent agent = _agents.Find(a => a.URL == url);
                    if (agent == null)
                    {
                        Utils.WriteLog($"Agent {url} does not exist", "error");
                    }
                    agent.IsReady = true;
                    //Utils.WriteAgentListToFile(_agents, agentsFilePath);
                }
                await Task.Delay(10);
                return true;
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in agentReady: {ex.Message} {ex.StackTrace}", "error");
                return false;
            }
        }

        public void GetComparisonResults(string url, string jsonContent)
        {
            Utils.LoadConfig();
            //_logger = new Logger(Settings.Get("LOG_FILE_PATH"));
            try
            {
                if (Settings.Get("MODE").ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost");
                }

                List<Event> events = JsonConvert.DeserializeObject<List<Event>>(jsonContent);

                string deviceResultsDir = Settings.Get("DEVICE_RESULTS_DIR");
                if (!Directory.Exists(deviceResultsDir))
                {
                    Directory.CreateDirectory(deviceResultsDir);
                }

                Dictionary<string, List<Event>> deviceResultsDict = new Dictionary<string, List<Event>>();

                // Initialize dictionary of <deviceName, eventsList>
                foreach (Event eventObj in events)
                {
                    string ga = eventObj.EventDeviceType;
                    string sn = eventObj.EventDeviceSerialNumber;
                    string deviceName = string.Join("_", new string[] { sn, ga });
                    deviceResultsDict[deviceName] = new List<Event>();
                }

                // Fill dictionary of <deviceName, eventsList>
                foreach (Event eventObj in events)
                {
                    string ga = eventObj.EventDeviceType;
                    string sn = eventObj.EventDeviceSerialNumber;
                    string eventKey = eventObj.EventKey;
                    string eventValue = eventObj.EventValue;
                    DateTime creationTime = eventObj.CreationTime;
                    string deviceName = string.Join("_", new string[] { sn, ga });
                    deviceResultsDict[deviceName].Add(eventObj);
                }

                // Create json files
                foreach (string deviceName in deviceResultsDict.Keys)
                {
                    string deviceFolderPath = Path.Combine(deviceResultsDir, deviceName);
                    Directory.CreateDirectory(deviceFolderPath);
                    string jsonContentByDevice = JsonConvert.SerializeObject(deviceResultsDict[deviceName]);
                    string csvFileByDevice = Path.Combine(deviceResultsDir, deviceName, deviceName + ".csv");
                    //Utils.WriteToFile(jsonFileByDevice, jsonContentByDevice, append: false);
                    Utils.WriteRecordsToCsv(csvFileByDevice, deviceResultsDict[deviceName]);
                }
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Get from agent report of script log and write them to file <deviceName>_log.txt
        /// </summary>
        /// <param name="url">agent url</param>
        /// <param name="content">Response content</param>
        /// <returns>true/false if operation succeeds</returns>
        public void GetScriptLog(string url, string jsonContent)
        {
            Utils.LoadConfig();
            //_logger = new Logger(Settings.Get("LOG_FILE_PATH"));
            try
            {
                if (Settings.Get("MODE").ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost");
                }

                string deviceLogsDir = Settings.Get("DEVICE_LOGS_DIR");
                if (!Directory.Exists(deviceLogsDir))
                {
                    Directory.CreateDirectory(deviceLogsDir);
                }

                ScriptLog scriptLogObj = JsonConvert.DeserializeObject<ScriptLog>(jsonContent);
                string deviceName = scriptLogObj.DeviceName;
                string logContent = scriptLogObj.Content;

                string logFilePath = Path.Combine(deviceLogsDir, deviceName + "_log.txt");

                Utils.WriteToFile(logFilePath, logContent, append: false);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Create empty json file agents.json in base folder
        /// </summary>
        private void InitAgents(FileStream agentsFile)
        {
            Utils.WriteLog("Init agents file.", "info");
            using (StreamWriter writer = new StreamWriter(agentsFile))
            {
                writer.Write("");
            }
        }

        public List<string> GetAgents()
        {
            List<string> agentUrls = (from agent in _agents select agent.URL).ToList();
            return agentUrls;
        }

        public List<Device> GetDevices()
        {
            return _devices;
        }

        /// <summary>
        /// Retrieve all devices from portal, read devices from csv file,
        /// create delta list containing only non-existing devices and insert them to portal
        /// </summary>
        /// <param name="env">env value (dev/staging/int)</param>
        /// <param name="devicesCsvFile">csv file containing devices to insert</param>
        private void InsertDevicesToPortal(string configFile)
        {
            try
            {
                Utils.WriteLog("Insert devices to portal.", "info");
                string pythonScriptsFolder = Settings.Get("PYTHON_SCRIPTS_PATH");
                string pythonExePath = Settings.Get("PYTHON");
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                Utils.RunCommand(pythonExePath, "insert_devices.py", $"{configFile}", pythonScriptsFolder, Settings.Get("OUTPUT"));
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in insertDevices: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Divide devices among agents in Round-Robin and distribute them to agents
        /// </summary>
        private void DistributeDevicesAmongAgents()
        {
            try
            {
                Utils.WriteLog($"Distribute device among agents.", "info");
                int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "distribute_devices.py", $"{Settings.Get("CONFIG_FILE")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in distributeDevices: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Send automation script only to agents with Ready status
        /// </summary>
        private void SendAutomationScript()
        {
            try
            {
                Utils.WriteLog("Send automation script to agents.", "info");
                int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "send_script.py", $"{Settings.Get("CONFIG_FILE")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in sendAutomationScript: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Collect AWS services of given env (dev/staging/int)
        /// </summary>
        /// <param name="env">environment</param>
        private void CollectAWSServices(string env)
        {
            Utils.WriteLog("Collect AWS services.", "info");
            string cwd = Directory.GetCurrentDirectory();
            Utils.RunCommand("aws", "ecs", $"list-services --cluster {env}-ECS-Cluster", cwd, Settings.Get("AWS_SERVICES_PATH"));
        }

        /// <summary>
        /// Collect AWS instances
        /// </summary>
        private void CollectAWSInstances()
        {
            Utils.WriteLog("Collect AWS instances.", "info");
            int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "collect_aws_instances.py", $"{Settings.Get("AWS_INSTANCES_PATH")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
            Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
        }

        public void Reset()
        {
            Utils.LoadConfig();
            _agents.Clear();
            string agentsPath = Settings.Get("AGENTS_PATH");
            string outputPath = Settings.Get("OUTPUT");
            string returncodePath = Settings.Get("RETURN_CODE");
            string logfilePath =  Settings.Get("LOG_FILE_PATH");
            File.Delete(agentsPath);
            File.Delete(outputPath);
            File.Delete(returncodePath);
            File.Delete(logfilePath);
        }

        public string TestCommand(string num)
        {
            Utils.LoadConfig();
            //_logger = new Logger(Settings.Get("LOG_FILE_PATH"));

            try
            {
                string pythonPath = Settings.Get("PYTHON");
                string pythonScriptsPath = Settings.Get("PYTHON_SCRIPTS_PATH");
                int returnCode = Utils.RunCommand(pythonPath, "pythonScript.py", num, pythonScriptsPath, @"D:\test_center\out.txt");
                var output = Utils.ReadFileContent(@"D:\test_center\out.txt");
                return "success";
            }

            catch (Exception ex)
            {
                Utils.WriteLog($"Error: {ex.StackTrace}", "error");
                return "fail";
            }
        }
    }
}
