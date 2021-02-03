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
using Amazon.ECS;
using Amazon.Runtime;
using System.Timers;
using System.Threading;


namespace Backend
{
    public class BackEnd : IBackEndInterfaces
    {
        private static System.Timers.Timer _getAgentConnectTimer = null;
        private static System.Timers.Timer _getAWSResourcesTimer = new System.Timers.Timer(new TimeSpan(0, 1, 0).TotalMilliseconds);

        private static System.Timers.Timer _stopECSTaskTimer = new System.Timers.Timer(new TimeSpan(0, 1, 0).TotalMilliseconds);


        private static List<Agent> _agents = new List<Agent>();
        private static HashSet<LumenisXDevice> _devices = new HashSet<LumenisXDevice>();
        private static Dictionary<string, double> _cpuUtilization = new Dictionary<string, double>();

        private static List<string> _servicesToStop = new List<string>();

        private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private static double _stoppedMinutes = 0;

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
        public async Task Init()
        {
            Utils.LoadConfig();
            try
            {
                Utils.WriteLog($"-----INIT STAGE BEGIN-----", "info");

                int minutesToConnect = int.Parse(Settings.Get("MINUTES_TO_CONNECT"));
                _getAgentConnectTimer = new System.Timers.Timer(new TimeSpan(0, minutesToConnect, 0).TotalMilliseconds);

                _getAgentConnectTimer.Elapsed += GetAgentConnectTimer_Elapsed;
                _getAWSResourcesTimer.Elapsed += GetAWSResourcesTimer_Elapsed;


                _stopECSTaskTimer.Elapsed += StopECSTimer_Elapsed;
                _stopECSTaskTimer.AutoReset = false;
                _getAgentConnectTimer.AutoReset = false;

                var devicesLogsFolder = Settings.Get("DEVICE_LOGS_DIR");
                if (Directory.Exists(devicesLogsFolder))
                {
                    CleanUpFolderContent(devicesLogsFolder);
                }
                
                var devicesResultsFolder = Settings.Get("DEVICE_RESULTS_DIR");
                if (Directory.Exists(devicesResultsFolder))
                {
                    CleanUpFolderContent(devicesResultsFolder);
                }

                DevicesFromCsv();

                _servicesToStop = Settings.Get("SERVICES_TO_STOP").Split(',').ToList();

                try
                {
                    var skipInsert = bool.Parse(Settings.Get("SKIP_INSERT_FROM_API"));

                    if (!skipInsert)
                    {
                        var mode = (InsertionStrategy)int.Parse(Settings.Get("INSERTION_STRATEGY"));

                        InsertDevicesToPortal(Settings.Get("CONFIG_FILE"), mode);

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

        private void StopECSTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                Utils.WriteLog("============================================================", "info");

                var awsEcsClient = new AmazonECSClient(
                    Settings.Get("AWSAccessKey"),
                    Settings.Get("AWSSecretKey"));

                var clusterName = $"{Settings.Get("ENV").ToLower()}-ECS-Cluster";

                var describeServiceRequest = new Amazon.ECS.Model.DescribeServicesRequest();
                describeServiceRequest.Services = _servicesToStop.Select(s => $"{Settings.Get("ENV").ToLower()}-{s}-Service").ToList();
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
                var minutesToKeepStopped = int.Parse(Settings.Get("MINUTES_TO_KEEP_STOPPED"));
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

                var clusterName = $"{Settings.Get("ENV").ToLower()}-ECS-Cluster";

                var awsEcsClient = new AmazonECSClient(
                    Settings.Get("AWSAccessKey"),
                    Settings.Get("AWSSecretKey"));

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
            var fileName = Settings.Get("DEVICES_PATH");
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
            Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));
            DistributeDevicesAmongAgents();
        }

        /// <summary>
        /// Send automation script to ready agents only
        /// </summary>
        private void StartSendingScript()
        { 
            Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));

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
                Utils.LoadConfig();

                Utils.WriteLog($"-----CONNECT STAGE BEGIN-----", "info");
                string agentsPath = Settings.Get("AGENTS_PATH");
                Utils.WriteLog($"Received agent connect from {agentUrl}.", "info");
                string agentsFilePath = Settings.Get("AGENTS_PATH");

                Utils.WriteLog($"Agent {agentUrl} is connecting...", "info");   

                Utils.WriteLog($"Agent {agentUrl}: entering critical code...", "info");

                Utils.WriteLog($"Current agents number: {_agents.Count}.", "info");
                if (_agents.Count == 0)
                {
                    await Init();
                }

                if (Settings.Get("MODE").ToLower() == "debug")
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
                Utils.LoadConfig();
                string agentsFilePath = Settings.Get("AGENTS_PATH");

                if (Settings.Get("MODE").ToLower() == "debug")
                 {
                     url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                 }

                 Utils.WriteLog($"Received agent ready from {url}.", "info");

                 Agent agent = _agents.Find(a => a.URL == url);
                 if (agent == null)
                 {
                     Utils.WriteLog($"Agent {url} does not exist", "error");
                 }
                 agent.IsReady = true;

                 bool allAgentsAreReady = _agents.All(a => a.IsReady);

                 if (allAgentsAreReady)
                 {
                    StartSendingScript();
                    var scenario = int.Parse(Settings.Get("SCENARIO"));

                    if (scenario == 2)
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

            Utils.LoadConfig();

            try
            {
                if (Settings.Get("MODE").ToLower() == "debug")
                {
                    url = url.Replace("127.0.0.1", "localhost").Replace("::1", "localhost");
                }

                var comparisonResults = JsonConvert.DeserializeObject<ComparisonResults>(jsonContent);
                var events = comparisonResults.Events;

                Utils.WriteLog($"-----GET COMPARE RESULTS FOR {comparisonResults.DeviceName} BEGIN-----", "info");

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

                Utils.WriteLog($"-----GET SCRIPT LOG FOR {deviceName} BEGIN-----", "info");

                string logFilePath = Path.Combine(deviceLogsDir, deviceName + "_log.txt");

                Utils.WriteToFile(logFilePath, logContent, append: false);
                Utils.WriteLog($"Log file was received from agent {url}", "info");

                Utils.WriteLog($"-----GET SCRIPT LOG FOR {deviceName} END-----", "info");

                var deviceType = deviceName.Split('_')[1];
                var deviceSerialNumber = deviceName.Split('_')[0];

                _devices.RemoveWhere(d => d.DeviceType == deviceType && d.DeviceSerialNumber == deviceSerialNumber);
            }
            catch (Exception ex)
            {
                Utils.WriteLog($"{ex.Message} {ex.StackTrace}", "error");
            }
            finally
            {
                if (_devices.Count == 0)
                {
                    _getAWSResourcesTimer.Stop();
                    Utils.WriteLog($"Stopping AWS resources timer", "info");
                }
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
                string pythonScriptsFolder = Settings.Get("PYTHON_SCRIPTS_PATH");
                string pythonExePath = Settings.Get("PYTHON");
                Utils.WriteLog($"Python exe path: {pythonExePath}", "info");
                var result = Utils.RunCommand(pythonExePath, "insert_devices.py", $"{configFile} {strategy}", pythonScriptsFolder, Settings.Get("OUTPUT"));

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
                            //if (_cpuUtilization[dimension.Value] < dataPoint.Maximum)
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

        private void GetAWSECSMetrics()
        {

            try
            {
                var metrics = new[] { "CPUUtilization", "MemoryUtilization" };

                var clusterName = $"{Settings.Get("ENV").ToLower()}-ECS-Cluster";
                var serviceNames = new[] { $"{Settings.Get("ENV").ToLower()}-Facade-Service" , $"{Settings.Get("ENV").ToLower()}-Device-Service" , $"{Settings.Get("ENV").ToLower()}-Processing-Service" };

                var client = new AmazonCloudWatchClient(
                     Settings.Get("AWSAccessKey"),
                     Settings.Get("AWSSecretKey")
                     );

                Utils.WriteLog("---------------------------------------------------------------", "info");

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
                            //if (_cpuUtilization.ContainsKey(serviceName))
                            {
                                //if (_cpuUtilization[dimension.Value] < dataPoint.Maximum)
                                {
                                    Utils.WriteLog($"{serviceName} {metric}: Max = {dataPoint.Maximum} Average = {dataPoint.Average} ", "info");
                                    //_cpuUtilization[serviceName] = dataPoint.Maximum;
                                }
                            }
                            // else _cpuUtilization[dimension.Value] = dataPoint.Maximum;

                        }
                    }

                }

                Utils.WriteLog("---------------------------------------------------------------", "info");

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
