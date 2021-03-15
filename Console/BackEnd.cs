using Console;
using TestCenterConsole.Models;
using Console.Models;
using Console.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;
using Amazon.ECS;
using Amazon.Runtime;
using System.Timers;
using System.Threading;
using Amazon.RDS;
using Amazon.RDS.Model;
using Console.Interfaces;
using Event = Console.Models.Event;
using Amazon;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace Console
{
    public class BackEnd : IBackEndInterface
    {
        private static System.Timers.Timer _getAgentConnectTimer = null;
        private static System.Timers.Timer _getAWSResourcesTimer = new System.Timers.Timer(new TimeSpan(0, 2, 0).TotalMilliseconds);
        private static System.Timers.Timer _stopECSTaskTimer = new System.Timers.Timer(new TimeSpan(0, 1, 0).TotalMilliseconds);

        private static List<Agent> _agents = new List<Agent>();
       
        private static HashSet<LumenisXDevice> _devices = new HashSet<LumenisXDevice>();

        private static List<string> _servicesToStop = new List<string>();

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private static double _stoppedMinutes = 0;

        private static Settings Settings;

        public event EventHandler<AwsMetricsData> AwsDataUpdated;
        public event EventHandler<StageData> StageDataUpdated;

        IHttpClientFactory _clientFactory;

        public BackEnd(Settings settings, IHttpClientFactory clientFactory)
        {
            Settings = settings;
            _clientFactory = clientFactory;
        }

        public void UpdateCenterSettings(TestCenterSettings settings)
        {
            Settings["ENV"] = settings.Environment.ToString().ToLower();
            Settings["LOG_FILE_PATH"] = !string.IsNullOrWhiteSpace(settings.LogFilePath) ? settings.LogFilePath : Settings["LOG_FILE_PATH"];
            Settings["DEVICE_LOGS_DIR"] = !string.IsNullOrWhiteSpace(settings.ScriptResultsFolder) ? settings.ScriptResultsFolder : Settings["DEVICE_LOGS_DIR"];
            Settings["DEVICE_RESULTS_DIR"] = !string.IsNullOrWhiteSpace(settings.ResultsFolder) ? settings.ResultsFolder : Settings["DEVICE_RESULTS_DIR"];
        }

        public void UpdateScenarioSettings(Scenario scenario)
        {
            //if (Settings["SCENARIO_DEVICE_NUMBER"] != scenario.DevicesNumber.ToString() ||
            //        Settings["SCENARIO_DEVICE_TYPE"] != scenario.DevicesType)
            {
                List<LumenisXDevice> devices = new List<LumenisXDevice>();
                for (int i = 1; i <= scenario.DevicesNumber; i++)
                {
                    var device = new LumenisXDevice(scenario.DevicesType, $"{scenario.Name}-{i}");
                    devices.Add(device);
                }

                Utils.WriteDevicesToCsv(Settings["DEVICES_PATH"], devices);
            }
            

            Settings["SCENARIO_DEVICE_NUMBER"] = scenario.DevicesNumber.ToString();
            Settings["SCENARIO_DEVICE_TYPE"] = scenario.DevicesType;
            Settings["SCENARIO_INSERTION_STRATEGY"] = scenario.InsertionStrategy.ToString().ToLower();

            Settings["SCENARIO_STOP_AWS"] = scenario.StopAws.ToString();

            Settings["SCENARIO_MINUTES_TO_KEEP_STOPPED"] = scenario.MinutesServicesStopped.ToString();
            Settings["SCENARIO_MINUTES_TO_CONNECT"] = scenario.MinutesToWaitForAgents.ToString();
            Settings["SCENARIO_SERVICES_TO_STOP"] = string.Join(",", scenario.Services.Select(s => s.ToString()).ToList());

            Settings["SCENARIO_NAME"] = scenario.Name;
            
        }


        private void TriggerAwsDataUpdate(AwsMetricsData data)
        {
            // call all subscribers
            if (AwsDataUpdated != null) // make sure there are subscribers!
                AwsDataUpdated(this, data); // trigger the event
        }

        private void TriggerStageDataUpdate(StageData data)
        {
            // call all subscribers
            if (StageDataUpdated != null) // make sure there are subscribers!
                StageDataUpdated(this, data); // trigger the event
        }      


        /// <summary>
        /// Initialize backend:
        /// Create two timers - first agent connection and ready timers
        /// Init agent list
        /// Insert devices to portal
        /// Collect AWS services and instances
        /// </summary>
        public async Task Init()
        {
            //Utils.LoadConfig();
            try
            {
                Utils.WriteLog($"-----INIT STAGE BEGIN-----", "info");

                int minutesToConnect = int.Parse(Settings["SCENARIO_MINUTES_TO_CONNECT"]);
                _getAgentConnectTimer = new System.Timers.Timer(new TimeSpan(0, minutesToConnect, 0).TotalMilliseconds);

                _getAgentConnectTimer.Elapsed += GetAgentConnectTimer_Elapsed;
                _getAWSResourcesTimer.Elapsed += GetAWSResourcesTimer_Elapsed;

                _stopECSTaskTimer.Elapsed += StopECSTimer_Elapsed;
                _stopECSTaskTimer.AutoReset = false;
                _getAgentConnectTimer.AutoReset = false;

                PrepareCenter();

                DevicesFromCsv();

                _servicesToStop = Settings["SCENARIO_SERVICES_TO_STOP"].Split(',').ToList();

                try
                {
                    var skipInsert = bool.Parse(Settings["SCENARIO_SKIP_INSERT_FROM_API"]);

                    if (!skipInsert)
                    {
                        var mode = Enum.Parse(typeof(InsertionStrategy), Settings["SCENARIO_INSERTION_STRATEGY"]);

                        InsertDevicesToPortal(Settings["CONFIG_FILE"], (InsertionStrategy)mode);

                    }
                    else
                    {
                        Utils.WriteLog("Skipped inserting the devices from API...", "info");
                    }

                    _getAgentConnectTimer.Start();
                    Utils.WriteLog("*****Connect timer start*****", "info");

                }
                catch (Exception ex)
                {
                    Utils.WriteLog(ex.Message, "error");
                    Utils.WriteLog(ex.StackTrace, "error");
                }


                // Measure services
                GetAWSECSMetrics();
           
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error:{ex.Message} {ex.Source} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog($"-----INIT STAGE END-----", "info");

                var stageData = new StageData();
                stageData.Stage = Stage.AGENTS_CONNECT.ToString();
                stageData.StageIdx = Stage.AGENTS_CONNECT;
                stageData.Time = DateTime.Now;
                TriggerStageDataUpdate(stageData);
            }
        }

        private void StopECSTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                Utils.WriteLog("============================================================", "info");

                var awsEcsClient = new AmazonECSClient(
                    Settings["AWSAccessKey"],
                    Settings["AWSSecretKey"], RegionEndpoint.USEast1);

                var clusterName = $"{Settings["ENV"].ToLower()}-ECS-Cluster";

                var describeServiceRequest = new Amazon.ECS.Model.DescribeServicesRequest();
                describeServiceRequest.Services = _servicesToStop.Select(s => $"{Settings["ENV"].ToLower()}-{s}-Service").ToList();
                describeServiceRequest.Cluster = clusterName;

                var describeServiceResponse = awsEcsClient.DescribeServicesAsync(describeServiceRequest);

                foreach (var ecsService in describeServiceResponse.Result.Services)
                {
                    if (ecsService.RunningCount != 0 || ecsService.PendingCount != 0)
                    {
                        Utils.WriteLog($"Service {ecsService.ServiceName} has some running tasks...", "info");
                        StopAWSECSTasks(ecsService.ServiceName).Wait();
                    }
                    else
                    {
                        Utils.WriteLog($"Service {ecsService} tasks are not up yet. Skipping...", "info");
                    }
                }

            }
            catch(Exception ex)
            {
                Utils.WriteLog($"Error in StopECSTime: {ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog("============================================================", "info");
                var minutesToKeepStopped = int.Parse(Settings["SCENARIO_MINUTES_TO_KEEP_STOPPED"]);
                if (_stoppedMinutes <= minutesToKeepStopped)
                {
                    _stopECSTaskTimer.Interval = new TimeSpan(0, 0, 30).TotalMilliseconds;
                    _stopECSTaskTimer.Start();
                }
                _stoppedMinutes += 0.5;               
                
            }
        }

        private async Task StopAWSECSTasks(string serviceWhereStopTasks)
        {
            try
            {
                Utils.WriteLog($"Stopping ALL tasks for service {serviceWhereStopTasks}", "info");

                var clusterName = $"{Settings["ENV"].ToLower()}-ECS-Cluster";

                var awsEcsClient = new AmazonECSClient(
                    Settings["AWSAccessKey"],
                    Settings["AWSSecretKey"], RegionEndpoint.USEast1);

                var listTasksRequest = new Amazon.ECS.Model.ListTasksRequest();
                listTasksRequest.Cluster = clusterName;

                var listTasksResponse = await awsEcsClient.ListTasksAsync(listTasksRequest);

                var describeTasksRequest = new Amazon.ECS.Model.DescribeTasksRequest();
                describeTasksRequest.Cluster = clusterName;
                describeTasksRequest.Tasks = listTasksResponse.TaskArns;

                var describeTasksResponse = await awsEcsClient.DescribeTasksAsync(describeTasksRequest);
                var tasks = describeTasksResponse.Tasks;

                var tasksToStop = tasks.Where(t => t.Group.Contains(serviceWhereStopTasks));                

                foreach (var task in tasksToStop)
                {
                    var stopTaskRequst = new Amazon.ECS.Model.StopTaskRequest();
                    stopTaskRequst.Cluster = clusterName;
                    stopTaskRequst.Task = task.TaskArn;

                    stopTaskRequst.Reason = $"Stopping task {stopTaskRequst.Task} to test availability";

                    var stopTaskResponse = await awsEcsClient.StopTaskAsync(stopTaskRequst);

                    if (stopTaskResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                    {
                        Utils.WriteLog($"{stopTaskResponse.Task.TaskArn} was stopped ", "info");
                    }
                    else
                    {
                        Utils.WriteLog($"Failed to stop task {stopTaskResponse.Task.TaskArn}. Response: {stopTaskResponse.HttpStatusCode}", "info");
                    }
                    
                }
              
            }
            catch(Exception ex)
            {
                Utils.WriteLog($"Error in connect: {ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                
            }            
        }

        private void GetAWSResourcesTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            GetAWSECSMetrics();
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

        private void DevicesFromCsv()
        {
            var fileName = Settings["DEVICES_PATH"];
            var lines = File.ReadAllLines(fileName).Skip(1).Select(a => a.Split(','));
            var devices = from line in lines
                      select new LumenisXDevice(line[0], line[1]);

            foreach (var device in devices)
            {
                _devices.Add(device);
            }            
        }

        /// <summary>
        /// Distribute devices among devices and wait for agents to be ready
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="e">event</param>
        private void GetAgentConnectTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {  
            Utils.WriteLog($"*****Connect timer stop*****", "info");
            Utils.WriteAgentListToFile(_agents, Settings["AGENTS_PATH"]);

            var stageData = new StageData();
            stageData.Stage = Stage.DISTRIBUTE_DEVICES.ToString();
            stageData.StageIdx = Stage.DISTRIBUTE_DEVICES;
            stageData.Time = DateTime.Now;
            TriggerStageDataUpdate(stageData);
            
            DistributeDevicesAmongAgents();
        }

        /// <summary>
        /// Send automation script to ready agents only
        /// </summary>
        private void StartSendingScript()
        { 
            Utils.WriteAgentListToFile(_agents, Settings["AGENTS_PATH"]);

            Utils.WriteLog($"Starting AWS resources timer", "info");
            _getAWSResourcesTimer.Start();

            SendAutomationScript();
        }

        /// <summary>
        /// Connect to test center and register the reqested agent
        /// </summary>
        /// <param name="agentUrl">agent URL</param>
        /// <returns>true/false if connection succeeded</returns>
        public async Task<bool> Connect(string agentUrl)
        {
            await semaphoreSlim.WaitAsync();

            try
            {
                var stageData = new StageData();
                stageData.Stage = Stage.INIT.ToString();
                stageData.StageIdx = Stage.INIT;
                stageData.Time = DateTime.Now;
                TriggerStageDataUpdate(stageData);
                
                Utils.WriteLog($"-----CONNECT STAGE BEGIN-----", "info");
                string agentsPath = Settings["AGENTS_PATH"];
                Utils.WriteLog($"Received agent connect from {agentUrl}.", "info");
                string agentsFilePath = Settings["AGENTS_PATH"];

                Utils.WriteLog($"Agent {agentUrl} is connecting...", "info");   

                Utils.WriteLog($"Agent {agentUrl}: entering critical code...", "info");

                Utils.WriteLog($"Current agents number: {_agents.Count}.", "info");

                if (_agents.Count == 0)
                {
                    await Init();
                }

                if (Settings["MODE"].ToLower() == "debug")
                {
                    agentUrl = agentUrl.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                }

                Agent agent = _agents.Find(a => a.URL == agentUrl);
                if (agent != null)
                {
                    Utils.WriteLog($"Agent {agentUrl} already exists.", "info");
                }
                else
                {
                    string[] urlStrings = agentUrl.Split(':');
                    Utils.WriteLog($"Adding agent {agentUrl} to pool.", "info");
                    _agents.Add(new Agent(urlStrings[0], int.Parse(urlStrings[1]), false));
                    Utils.WriteLog($"Agents count: {_agents.Count}.", "info");
                    
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
                semaphoreSlim.Release();
            }
        }

        /// <summary>
        /// Updates requested agent status to Ready
        /// </summary>
        /// <param name="url">agent URL</param>
        public async Task<bool> AgentReady(string url)
        {

             try
             {
                string agentsFilePath = Settings["AGENTS_PATH"];

                if (Settings["MODE"].ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                }

                Utils.WriteLog($"Received agent ready from {url}.", "info");

                Agent agent = _agents.Find(a => a.URL == url);
                if (agent == null)
                {
                    Utils.WriteLog($"Agent {url} does not exist", "error");
                    return false;
                }
                
                agent.IsReady = true;

                bool allAgentsAreReady = _agents.All(a => a.IsReady);

                if (allAgentsAreReady)
                {
                   StartSendingScript();
                   var stopAWSServices = bool.Parse(Settings["SCENARIO_STOP_AWS"]);

                   if (stopAWSServices)
                   {            

                       _stopECSTaskTimer.Start();
                   }
                }
                
                return true;
             }
             catch (Exception ex)
             {
                 Utils.WriteLog($"Error in agentReady: {ex.Message} {ex.StackTrace}", "error");
                 return false;
             }
            
        }

        public async Task GetComparisonResults(string url, string jsonContent)
        {

            try
            {
                if (Settings["MODE"].ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                }

                var comparisonResults = JsonConvert.DeserializeObject<ComparisonResults>(jsonContent);
                var events = comparisonResults.Events;

                Utils.WriteLog($"-----GET COMPARE RESULTS FOR {comparisonResults.DeviceName} BEGIN-----", "info");

                string deviceResultsDir = Settings["DEVICE_RESULTS_DIR"];
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
                    Utils.WriteEventsToCsv(csvFileByDevice, deviceResultsDict[deviceName]);
                }
                Utils.WriteLog($"Comparison files was received from agent {url}", "info");

                Utils.WriteLog($"-----GET COMPARE RESULTS FOR {comparisonResults.DeviceName} END-----", "info");                

            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                
            }
        }

        public async Task GetComparisonResults(string url, EventsLog eventsLog)
        {

            try
            {

                var stageData = new StageData();
                stageData.Stage = Stage.GET_RESULTS.ToString();
                stageData.StageIdx = Stage.GET_RESULTS;
                stageData.Time = DateTime.Now;
                TriggerStageDataUpdate(stageData);

                if (Settings["MODE"].ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                }

                var events = eventsLog.EventsJson;

                var eventLogFileDir = Path.Combine(Settings["DEVICE_RESULTS_DIR"], eventsLog.DeviceName);
                var eventLogFilePath = Path.Combine(eventLogFileDir, "clientEventLog.csv");

                if (!Directory.Exists(eventLogFileDir))
                {
                    Directory.CreateDirectory(eventLogFileDir);
                }

                Utils.WriteToFile(eventLogFilePath, events, false);

                var sn = eventsLog.DeviceName.Split('_')[0];
                var ga = eventsLog.DeviceName.Split('_')[1];

                Utils.WriteLog($"-----GET COMPARE RESULTS FOR {eventsLog.DeviceName} BEGIN-----", "info");

                int returnCode = Utils.RunCommand(Settings["PYTHON"], "compare_events.py", $"{Settings["CONFIG_FILE"]} {sn} {ga} {eventLogFilePath}", Settings["PYTHON_SCRIPTS_PATH"], Settings["OUTPUT"]);

                if (returnCode == 0)
                {
                    Utils.WriteLog($"Comparison results were received by test center.", "info");
                }
                else
                {
                    Utils.WriteLog($"Test center failed to receive comparison results", "info");
                }

                if (Directory.GetFiles(eventLogFileDir).Length < 2)
                {
                    Directory.Delete(eventLogFileDir, recursive: true);
                }

                _devices.Where(d => d.DeviceType == ga && d.DeviceSerialNumber == sn).FirstOrDefault().Finished = true;

                var stData = new StageData();
                stData.DevicesNumberFinished = _devices.Where(d => d.Finished == true).Count();
                TriggerStageDataUpdate(stData);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog($"-----GET COMPARE RESULTS FOR {eventsLog.DeviceName} END-----", "info");

                if (_devices.Where(d => d.Finished == false).Count() == 0)
                {
                    _getAWSResourcesTimer.Stop();
                    Utils.WriteLog($"Stopping AWS resources timer", "info");

                    var stageData = new StageData();
                    stageData.Stage = Stage.FINISHED.ToString();
                    stageData.StageIdx = Stage.FINISHED;
                    stageData.Time = DateTime.Now;
                    stageData.DevicesNumberFinished = _devices.Count;
                    TriggerStageDataUpdate(stageData);
                }
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
            
            try
            {
                
                if (Settings["MODE"].ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                }

                string deviceLogsDir = Settings["DEVICE_LOGS_DIR"];
                if (!Directory.Exists(deviceLogsDir))
                {
                    Directory.CreateDirectory(deviceLogsDir);
                }

                ScriptLog scriptLogObj = JsonConvert.DeserializeObject<ScriptLog>(jsonContent);
                string deviceName = scriptLogObj.DeviceName;
                string logContent = scriptLogObj.Content;

                Utils.WriteLog($"-----GET SCRIPT LOG FOR {deviceName} BEGIN-----", "info");

                string logFilePath = Path.Combine(deviceLogsDir, deviceName + "_log.txt");

                Utils.WriteToFile(logFilePath, logContent, append: false);
                Utils.WriteLog($"Log file was received from agent {url}", "info");

                Utils.WriteLog($"-----GET SCRIPT LOG FOR {deviceName} END-----", "info");

                var deviceType = deviceName.Split('_')[1];
                var deviceSerialNumber = deviceName.Split('_')[0];
                
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
 
            }
        }

        public List<string> GetAgents()
        {
            List<string> agentUrls = (from agent in _agents select agent.URL).ToList();
            return agentUrls;
        }


        /// <summary>
        /// Retrieve all devices from portal, read devices from csv file,
        /// create delta list containing only non-existing devices and insert them to portal
        /// </summary>
        /// <param name="env">env value (dev/staging/int)</param>
        /// <param name="devicesCsvFile">csv file containing devices to insert</param>
        private bool InsertDevicesToPortal(string configFile, InsertionStrategy strategy, int? timeout=null)
        {
            try
            {

                Utils.WriteLog("-----INSERT DEVICES BEGIN----.", "info");
                string pythonScriptsFolder = Settings["PYTHON_SCRIPTS_PATH"];
                string pythonExePath = Settings["PYTHON"];
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                var result = Utils.RunCommand(pythonExePath, "insert_devices.py", $"{configFile} {strategy}", pythonScriptsFolder, Settings["OUTPUT"]);

                var resultStr = result==1 ? "not completed" : "completed";
                Utils.WriteLog($"Insertion has {resultStr} successfully", "info");

                return result == 0;
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in insertDevices: {ex.Message} {ex.StackTrace}", "error");
                return false;
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
                int returnCode = Utils.RunCommand(Settings["PYTHON"], "distribute_devices.py", $"{Settings["CONFIG_FILE"]}", Settings["PYTHON_SCRIPTS_PATH"], Settings["OUTPUT"]);
                
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

                string pythonScriptsFolder = Settings["PYTHON_SCRIPTS_PATH"];
                string pythonExePath = Settings["PYTHON"];
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                int returnCode = Utils.RunCommand(pythonExePath, "delete_device.py", $"{deviceSerialNumber} {deviceType} {configFile} {dataType} {fromDateStr} {toDateStr}", pythonScriptsFolder, Settings["OUTPUT"]);
                
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
                
                string pythonScriptsFolder = Settings["PYTHON_SCRIPTS_PATH"];
                string pythonExePath = Settings["PYTHON"];
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                int returnCode = Utils.RunCommand(pythonExePath, "delete_device.py", $"{deviceSerialNumber} {deviceType} {configFile}", pythonScriptsFolder, Settings["OUTPUT"]);
                
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
                int returnCode = Utils.RunCommand(Settings["PYTHON"], "send_script.py", $"{Settings["CONFIG_FILE"]}", Settings["PYTHON_SCRIPTS_PATH"], Settings["OUTPUT"]);
                
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in sendAutomationScript: {ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                Utils.WriteLog("-----SEND SCRIPT STAGE END-----", "info");
                var stageData = new StageData();
                stageData.Stage = Stage.RUN_DEVICES.ToString();
                stageData.StageIdx = Stage.RUN_DEVICES;
                stageData.Time = DateTime.Now;
                TriggerStageDataUpdate(stageData);
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
            Utils.RunCommand("aws", "ecs", $"list-services --cluster {env}-ECS-Cluster", cwd, Settings["AWS_SERVICES_PATH"]);
        }

        /// <summary>
        /// Collect AWS instances
        /// </summary>
        private void CollectAWSInstances()
        {
            Utils.WriteLog("Collect AWS instances.", "info");
            int returnCode = Utils.RunCommand(Settings["PYTHON"], "collect_aws_instances.py", $"{Settings["AWS_INSTANCES_PATH"]}", Settings["PYTHON_SCRIPTS_PATH"], Settings["OUTPUT"]);
            
        }

        private void GetAWSMetrics()
        {

            try
            {
                var instanceIds = Settings["AWS_EC2_INSTANCES"].Split(',');

                var client = new AmazonCloudWatchClient(
                     Settings["AWSAccessKey"],
                     Settings["AWSSecretKey"],
                     RegionEndpoint.USEast1
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
                        //if (_cpuUtilization.ContainsKey(dimension.Value))
                        {
                            //if (_cpuUtilization[dimension.Value] < dataPoint.Maximum)
                            {
                                //Utils.WriteLog($"Instance: {dimension.Value} CPU Max load increased from: {_cpuUtilization[dimension.Value]} to {dataPoint.Maximum}", "info");
                                //_cpuUtilization[dimension.Value] = dataPoint.Maximum;
                            }
                        }
                        //else _cpuUtilization[dimension.Value] = dataPoint.Maximum;

                    }
                }
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in GetAWSMetrics: {ex.Message} {ex.StackTrace}", "error");
            }

        }

        private void GetAWSECSMetrics()
        {

            try
            {
                var metrics = new[] { "CPUUtilization", "MemoryUtilization" };

                var clusterName = $"{Settings["ENV"].ToLower()}-ECS-Cluster";
                var serviceNames = new[] { $"{Settings["ENV"].ToLower()}-Facade-Service" , $"{Settings["ENV"].ToLower()}-Device-Service" , $"{Settings["ENV"].ToLower()}-Processing-Service" };

                var client = new AmazonCloudWatchClient(
                     Settings["AWSAccessKey"],
                     Settings["AWSSecretKey"], RegionEndpoint.USEast1
                     );

                Utils.WriteLog("---------------------------------------------------------------", "info");

                var awsData = new AwsMetricsData();
                awsData.CPUUtilization = new AWSData[serviceNames.Length];
                awsData.MemoryUtilization = new AWSData[serviceNames.Length];

                awsData.EventsInRDS = GetEventsNumber();              

                foreach (var serviceName in serviceNames)
                {
                    foreach (var metric in metrics)
                    {
                        var request = new GetMetricStatisticsRequest();

                        request.MetricName = metric;
                        request.Period = 60;
                        request.Statistics.Add("Average");
                        request.Statistics.Add("Maximum");
                        request.Namespace = "AWS/ECS";
                        request.Unit = "Percent";

                        var cluster = new Dimension
                        {
                            Name = "ClusterName",
                            Value = clusterName,
                        };

                        var service = new Dimension
                        {
                            Name = "ServiceName",
                            Value = serviceName,
                        };

                        request.Dimensions.Add(cluster);
                        request.Dimensions.Add(service);

                        var currentTime = DateTime.UtcNow;
                        var startTime = currentTime.AddMinutes(-5);
                        string currentTimeString = currentTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
                        string startTimeString = startTime.ToString("yyyy-MM-ddTHH:mm:ssZ");

                        request.StartTimeUtc = Convert.ToDateTime(startTimeString);
                        request.EndTimeUtc = Convert.ToDateTime(currentTimeString);

                        var response = client.GetMetricStatisticsAsync(request).Result;
                        var resonseData = response.Datapoints.OrderByDescending(x => x.Timestamp).ToList();

                        if (resonseData.Count > 0)
                        {
                            var dataPoint = response.Datapoints[0];
                            Utils.WriteLog($"{serviceName} {metric}: Max = {dataPoint.Maximum} Average = {dataPoint.Average} ", "info");

                            var awsServiceName = serviceName.Split('-')[1];
                            var serviceId = (int)Enum.Parse(typeof(AWSServices), awsServiceName);
                            
                            if (metric == "CPUUtilization")
                            {
                                awsData.CPUUtilization[serviceId] = new AWSData()
                                {
                                    Time = dataPoint.Timestamp.ToLocalTime(),
                                    Value = (int)(dataPoint.Maximum)
                                };
                            }
                            else
                            {
                                awsData.MemoryUtilization[serviceId] = new AWSData()
                                {
                                    Time = dataPoint.Timestamp.ToLocalTime(),
                                    Value = (int)(dataPoint.Maximum)
                                };
                            }
                            
                        }
                    }

                }

                TriggerAwsDataUpdate(awsData);

                Utils.WriteLog("---------------------------------------------------------------", "info");

            }
            catch (Exception ex)
            {
                Utils.WriteLog($"Error in GetAWSMetrics: {ex.Message} {ex.StackTrace}", "error");
            }

        }

        private void LumenisAuthenticate()
        {
            if (!string.IsNullOrEmpty(Settings["ACCESS_TOKEN"]))
                return;

            var authModel = new
            {
                email = Settings["API_USER"],
                password = Settings["API_PASS"]
            };

            var client = _clientFactory.CreateClient();
            var stringContent = new StringContent(JsonConvert.SerializeObject(authModel), Encoding.UTF8, "application/json");

            var response = client.PostAsync(Settings["API_LOGIN_HOST"], stringContent).Result;

            if (response.IsSuccessStatusCode)
            {
                var responseContent = response.Content;

                // by calling .Result you are synchronously reading the result
                string responseString = responseContent.ReadAsStringAsync().Result;
                var obj = JObject.Parse(responseString);

                Settings["ACCESS_TOKEN"] = obj["accessToken"].ToString();
            }

        }

        private int GetEventsNumber()
        {
            int result = 0;
            foreach (var device in _devices)
            {
                var eventsCount = 0;
                string deviceType = device.DeviceType;
                string deviceSerialNumber = device.DeviceSerialNumber;

                LumenisAuthenticate();

                var apiEventHost = Settings["API_PROCESSING_URL"];
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");

                var apiEventUrl = $"{apiEventHost}/events?deviceSerialNumber={deviceSerialNumber}&deviceType={deviceType}&from={yesterday}T00:00:00.015Z&to={today}T23:59:59.015Z";

                var requestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(apiEventUrl)
                };

                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Settings["ACCESS_TOKEN"]);

                var client = _clientFactory.CreateClient();
                var response = client.SendAsync(requestMessage).Result;

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = response.Content;

                    // by calling .Result you are synchronously reading the result
                    string responseString = responseContent.ReadAsStringAsync().Result;
                    var obj = JObject.Parse(responseString);

                    eventsCount = ((JArray)obj["data"]).Count;
                }

                result += eventsCount;
            }

            return result;
        }

        /// <summary>
        /// Resets backend
        /// </summary>
        public void Reset()
        {
            
            _agents.Clear();
            string agentsPath = Settings["AGENTS_PATH"];
            string outputPath = Settings["OUTPUT"];
            
            string logfilePath =  Settings["LOG_FILE_PATH"];
            File.Delete(agentsPath);
            File.Delete(outputPath);
            
            File.Delete(logfilePath);
        }

        public string TestCommand(string num)
        {
            
            try
            {
                string pythonPath = Settings["PYTHON"];
                string pythonScriptsPath = Settings["PYTHON_SCRIPTS_PATH"];
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

        public void PrepareCenter()
        {
            var devicesLogsFolder = Settings["DEVICE_LOGS_DIR"];
            if (Directory.Exists(devicesLogsFolder))
            {
                CleanUpFolderContent(devicesLogsFolder);
            }

            var devicesResultsFolder = Settings["DEVICE_RESULTS_DIR"];
            if (Directory.Exists(devicesResultsFolder))
            {
                CleanUpFolderContent(devicesResultsFolder);
            }

            var logFilePath = Settings["LOG_FILE_PATH"];
            if (File.Exists(logFilePath))
            {
                File.Delete(logFilePath);
            }

            var outputFilePath = Settings["OUTPUT"];
            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }

            var returnFilePath = Settings["RETURN_CODE"];
            if (File.Exists(returnFilePath))
            {
                File.Delete(returnFilePath);
            }
        }
    }
}
