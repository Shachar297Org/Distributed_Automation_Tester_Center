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
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using System.Timers;

namespace Backend
{
    public class BackEnd : IBackEndInterfaces
    {
        private static System.Timers.Timer _getAgentConnectTimer = new System.Timers.Timer(new TimeSpan(0, 10, 0).TotalMilliseconds);
        private static System.Timers.Timer _getAgentReadyTimer = new System.Timers.Timer(new TimeSpan(0, 10, 0).TotalMilliseconds);
        private static System.Timers.Timer _getAWSResourcesTimer = new System.Timers.Timer(new TimeSpan(0, 1, 0).TotalMilliseconds);

        private static List<Agent> _agents = new List<Agent>();
        private static List<Device> _devices = null;
        private static Dictionary<string, double> _cpuUtilization = new Dictionary<string, double>();

        private static object _lock = new object();

        enum DeviceData
        {
            commands,
            events
        }

        enum InsertionStrategy
        {
            intersect,
            all_new,
            union
        }

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
            try
            {
                Utils.WriteLog($"-----INIT STAGE BEGIN-----", "info");
                _getAgentConnectTimer.Elapsed += GetAgentConnectTimer_Elapsed;
                _getAgentReadyTimer.Elapsed += GetAgentReadyTimer_Elapsed;
                _getAWSResourcesTimer.Elapsed += GetAWSResourcesTimer_Elapsed;

                _getAgentConnectTimer.Start();
                Utils.WriteLog("*****Connect timer start*****", "info");

                var devicesLogsFolder = Settings.Get("DEVICE_LOGS_DIR");
                if (Directory.Exists(devicesLogsFolder))
                {
                    CleanUpFolderContent(devicesLogsFolder);
                }
                
                var devicesResultsFolder = Settings.Get("DEVICE_RESULTS_DIR");
                if (Directory.Exists(devicesLogsFolder))
                {
                    CleanUpFolderContent(devicesResultsFolder);
                }

                Task t = Task.Factory.StartNew(() =>
                {
                    InsertDevicesToPortal(Settings.Get("CONFIG_FILE"), InsertionStrategy.union);
                });
                if(t.Wait(new TimeSpan(0, 5, 0)))
                {
                    Utils.WriteLog("---Inserting devices finished within 5 min", "info");
                }
                else
                {
                    Utils.WriteLog("---Inserting devices didn't finished within 5 min", "info");
                }           
               

                // Measure cpuUtilization
                GetAWSMetrics();

                //InsertDevicesToPortal(Settings.Get("ENV"), Settings.Get("DEVICES_PATH"));
                //CollectAWSServices(Settings.Get("ENV"));
                //CollectAWSInstances();                
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error:{ex.Message} {ex.Source} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog($"-----INIT STAGE END-----", "info");
            }
        }

        private void GetAWSResourcesTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            GetAWSMetrics();
        }

        private void CleanUpFolderContent(string folderPath)
        {

            DirectoryInfo di = new DirectoryInfo(folderPath);

            foreach (FileInfo file in di.EnumerateFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.EnumerateDirectories())
            {
                dir.Delete(true);
            }
        }

        /// <summary>
        /// Distribute devices among devices and wait for agents to be ready
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">event</param>
        private void GetAgentConnectTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {  
            _getAgentConnectTimer.Stop();
            Utils.WriteLog($"*****Connect timer stop*****", "info");
            Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));
            _getAgentReadyTimer.Start();
            Utils.WriteLog($"*****Ready timer start*****", "info");
            DistributeDevicesAmongAgents();
        }

        /// <summary>
        /// Send automation script to ready agents only
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">event</param>
        private void GetAgentReadyTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _getAgentReadyTimer.Stop();
            Utils.WriteLog($"*****Ready timer stop*****", "info");
            Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));
            SendAutomationScript();

            Utils.WriteLog($"Starting AWS resources timer", "info");
            _getAWSResourcesTimer.Start();
        }

        /// <summary>
        /// Connect to test center and register the reqested agent
        /// </summary>
        /// <param name="agentUrl">agent URL</param>
        /// <returns>true/false if connection succeeded</returns>
        public async Task<bool> Connect(string agentUrl)
        {
            lock (_lock)
            {
                Utils.LoadConfig();
                try
                {
                    Utils.WriteLog($"-----CONNECT STAGE BEGIN-----", "info");
                    string agentsPath = Settings.Get("AGENTS_PATH");
                    Utils.WriteLog($"Received agent connect from {agentUrl}.", "info");
                    string agentsFilePath = Settings.Get("AGENTS_PATH");

                    Utils.WriteLog($"Agent {agentUrl} is connecting...", "info");   

                    Utils.WriteLog($"Agent {agentUrl}: entering critical code...", "info");
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
                            agentUrl = agentUrl.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                        }
                        string[] urlStrings = agentUrl.Split(':');
                        Utils.WriteLog($"Adding agent {agentUrl} to pool.", "info");
                        _agents.Add(new Agent(urlStrings[0], int.Parse(urlStrings[1]), false));
                        Utils.WriteLog($"Agents count: {_agents.Count}.", "info");
                        //Utils.WriteAgentListToFile(_agents, agentsFilePath);
                    }
                    Utils.WriteLog($"Agent {agentUrl}: exiting critical code...", "info");

                    Utils.WriteLog($"Agent {agentUrl} was connected successfully.", "info");

                    return true;
                }
                catch (Exception ex)
                {
                    Utils.WriteLog($"Error in connect: {ex.Message} {ex.StackTrace}", "error");
                    return false;
                }
                finally
                {
                    Utils.WriteLog($"-----CONNECT STAGE END-----", "info");
                }
            }
        }

        /// <summary>
        /// Updates requested agent status to Ready
        /// </summary>
        /// <param name="url">agent URL</param>
        public async Task<bool> AgentReady(string url)
        {
            lock (_lock)
            {
                Utils.LoadConfig();
                string agentsFilePath = Settings.Get("AGENTS_PATH"); 

                try
                {
                    if (Settings.Get("MODE").ToLower() == "debug")
                    {
                        url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                    }

                    Utils.WriteLog($"Received agent ready from {url}.", "info");

                    //_agents = Utils.ReadAgentsFromFile(agentsFilePath);
                    Agent agent = _agents.Find(a => a.URL == url);
                    if (agent == null)
                    {
                        Utils.WriteLog($"Agent {url} does not exist", "error");
                    }
                    agent.IsReady = true;
                    //Utils.WriteAgentListToFile(_agents, agentsFilePath);

                    return true;
                }
                catch (Exception ex)
                {
                    Utils.WriteLog($"Error in agentReady: {ex.Message} {ex.StackTrace}", "error");
                    return false;
                }                
            }
        }

        public async Task GetComparisonResults(string url, string jsonContent)
        {
            // stop aws resource meaurement
            Utils.WriteLog($"Stopping AWS resources timer", "info");
            _getAWSResourcesTimer.Stop();

            Utils.LoadConfig();            
            try
            {
                Utils.WriteLog($"-----GET COMPARE RESULTS BEGIN-----", "info");

                if (Settings.Get("MODE").ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
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
                Utils.WriteLog($"Comparison files was received from agent {url}", "info");
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog($"-----GET COMPARE RESULTS END-----", "info");
            }
        }

        /// <summary>
        /// Get from agent report of script log and write them to file <deviceName>_log.txt
        /// </summary>
        /// <param name="url">agent url</param>
        /// <param name="content">Response content</param>
        /// <returns>true/false if operation succeeds</returns>
        public async Task GetScriptLog(string url, string jsonContent)
        {
            Utils.LoadConfig();

            try
            {
                Utils.WriteLog($"-----GET SCRIPT LOG BEGIN-----", "info");

                if (Settings.Get("MODE").ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
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
                Utils.WriteLog($"Log file was received from agent {url}", "info");
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog($"-----GET SCRIPT LOG END-----", "info");
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
        private void InsertDevicesToPortal(string configFile, InsertionStrategy strategy)
        {
            try
            {

                Utils.WriteLog("-----INSERT DEVICES BEGIN----.", "info");
                string pythonScriptsFolder = Settings.Get("PYTHON_SCRIPTS_PATH");
                string pythonExePath = Settings.Get("PYTHON");
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                Utils.RunCommand(pythonExePath, "insert_devices.py", $"{configFile} {strategy}", pythonScriptsFolder, Settings.Get("OUTPUT"));                
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in insertDevices: {ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog("-----INSERT DEVICES END----.", "info");
            }
        }

        /// <summary>
        /// Divide devices among agents in Round-Robin and distribute them to agents
        /// </summary>
        private void DistributeDevicesAmongAgents()
        {
            try
            {
                Utils.WriteLog($"-----DISTRIBUTE STAGE BEGIN-----", "info");
                int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "distribute_devices.py", $"{Settings.Get("CONFIG_FILE")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in distributeDevices: {ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog($"-----DISTRIBUTE STAGE END-----", "info");
            }
        }

        /// <summary>
        /// Delete device data from portal
        /// 
        /// </summary>
        /// <param name="type">env value (devices/events/commands)</param>
        private void DeleteDeviceDataFromPortal(string deviceSerialNumber, string deviceType, string configFile, DeviceData dataType,  DateTime fromDate, DateTime toDate)
        {
            try
            {
                var fromDateStr = fromDate.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                var toDateStr = toDate.ToString("s", System.Globalization.CultureInfo.InvariantCulture);

                Utils.WriteLog($"Deleting {dataType} from portal.", "info");
                if (!string.IsNullOrEmpty(fromDateStr) && !string.IsNullOrEmpty(toDateStr)) 
                    Utils.WriteLog($"From date {fromDate} to date {toDate}", "info");

                string pythonScriptsFolder = Settings.Get("PYTHON_SCRIPTS_PATH");
                string pythonExePath = Settings.Get("PYTHON");
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                int returnCode = Utils.RunCommand(pythonExePath, "delete_device.py", $"{deviceSerialNumber} {deviceType} {configFile} {dataType} {fromDateStr} {toDateStr}", pythonScriptsFolder, Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in DeleteDeviceDataFromPortal: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Delete device from portal
        /// 
        /// </summary>
        /// <param name="type">env value (devices/events/commands)</param>
        private void DeleteDeviceFromPortal(string deviceSerialNumber, string deviceType, string configFile)
        {
            try
            {
                Utils.WriteLog($"Deleting device from portal.", "info");
                
                string pythonScriptsFolder = Settings.Get("PYTHON_SCRIPTS_PATH");
                string pythonExePath = Settings.Get("PYTHON");
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                int returnCode = Utils.RunCommand(pythonExePath, "delete_device.py", $"{deviceSerialNumber} {deviceType} {configFile}", pythonScriptsFolder, Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in DeleteDeviceFromPortal: {ex.Message} {ex.StackTrace}", "error");
            }
        }


        /// <summary>
        /// Send automation script only to agents with Ready status
        /// </summary>
        private void SendAutomationScript()
        {
            try
            {
                Utils.WriteLog("-----SEND SCRIPT STAGE BEGIN-----", "info");
                Utils.WriteLog("Send automation script to agents.", "info");
                int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "send_script.py", $"{Settings.Get("CONFIG_FILE")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in sendAutomationScript: {ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog("-----SEND SCRIPT STAGE END-----", "info");
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

        private void GetAWSMetrics()
        {
            try
            {
                var instanceIds = Settings.Get("AWS_EC2_INSTANCES").Split(',');

                var client = new AmazonCloudWatchClient(
                     Settings.Get("AWSAccessKey"),
                     Settings.Get("AWSSecretKey")
                     );


                foreach (var instanceId in instanceIds)
                {
                    var request = new GetMetricStatisticsRequest();
                    request.MetricName = "CPUUtilization";
                    request.Period = 60;
                    request.Statistics.Add("Maximum");
                    request.Namespace = "AWS/EC2";
                    request.Unit = "Percent";

                    var dimension = new Dimension
                    {
                        Name = "InstanceId",
                        Value = instanceId,
                    };

                    request.Dimensions.Add(dimension);

                    var currentTime = DateTime.UtcNow;
                    var startTime = currentTime.AddMinutes(-20);
                    string currentTimeString = currentTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    string startTimeString = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

                    request.StartTimeUtc = Convert.ToDateTime(startTimeString);
                    request.EndTimeUtc = Convert.ToDateTime(currentTimeString);

                    var response = client.GetMetricStatisticsAsync(request).Result;

                    if (response.Datapoints.Count > 0)
                    {
                        var dataPoint = response.Datapoints[0];
                        if (_cpuUtilization.ContainsKey(dimension.Value))
                        {
                            if (_cpuUtilization[dimension.Value] < dataPoint.Maximum)
                            {
                                Utils.WriteLog($"Instance: {dimension.Value} CPU Max load increased from: {_cpuUtilization[dimension.Value]} to {dataPoint.Maximum}", "info");
                                _cpuUtilization[dimension.Value] = dataPoint.Maximum;
                            }
                        }
                        else _cpuUtilization[dimension.Value] = dataPoint.Maximum;

                    }
                }
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in GetAWSMetrics: {ex.Message} {ex.StackTrace}", "error");
            }

        }
 
        /// <summary>
        /// Resets backend
        /// </summary>
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
