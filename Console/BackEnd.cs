using Console;
using Console.Models;
using Console.Utilities;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;

namespace Backend
{
    public class BackEnd : IBackEndInterfaces
    {
        System.Timers.Timer _getAgentConnectTimer = new System.Timers.Timer(new TimeSpan(0, 0, 30).TotalMilliseconds);
        System.Timers.Timer _getAgentReadyTimer = new System.Timers.Timer(new TimeSpan(0, 0, 30).TotalMilliseconds);
        List<Agent> _agents = null;
        List<Device> _devices = null;
        Logger _logger;

        /// <summary>
        /// Initialize backend:
        /// Create two timers - first agent connection and ready timers
        /// Init agent list
        /// Insert devices to portal
        /// Collect AWS services and instances
        /// </summary>
        public bool Init()
        {
            try
            {
                Utils.LoadConfig();
                _logger = new Logger(Settings.Get("LOG_FILE_PATH"));
                _logger.WriteLog("Starting connect timer", "info");
                _getAgentConnectTimer.Elapsed += GetAgentConnectTimer_Elapsed;
                _getAgentReadyTimer.Elapsed += GetAgentReadyTimer_Elapsed;
                _getAgentConnectTimer.Start();

                InitAgents();
                InsertDevicesToPortal(Settings.Get("ENV"), Settings.Get("DEVICES_PATH"));
                //CollectAWSServices(Settings.Get("ENV"));
                //CollectAWSInstances();
                return true;
            }
            catch (Exception ex)
            {
                _logger.WriteLog($"Error:{ex.Message} {ex.Source} {ex.StackTrace}", "error");
                return false;
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
            DistributeDevicesAmongAgents();
            _logger.WriteLog($"Starting agent ready timer", "info");
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
            SendAutomationScript();
        }

        /// <summary>
        /// Connect to test center and register the reqested agent
        /// </summary>
        /// <param name="agentUrl">agent URL</param>
        /// <returns>true/false if connection succeeded</returns>
        public bool Connect(string agentUrl)
        {
            Utils.LoadConfig();
            _logger = new Logger(Settings.Get("LOG_FILE_PATH"));
            try
            {
                _logger.WriteLog($"Received agent connect from {agentUrl}.", "info");
                _agents = Utils.ReadAgentsFromFile(Settings.Get("AGENTS_PATH"));

                if (_agents.Count == 0)
                {
                    Init();
                }

                _logger.WriteLog($"Agent {agentUrl} is connecting.", "info");
                Agent agent = _agents.Find(a => a.URL == agentUrl);
                if (agent != null)
                {
                    return false;
                }
                if (Settings.Get("MODE").ToLower() == "debug")
                {
                    agentUrl = agentUrl.Replace("127.0.0.1", "localhost");
                }
                string[] urlStrings = agentUrl.Split(':');
                _logger.WriteLog($"Adding agent {agentUrl} to pool.", "info");
                _agents.Add(new Agent(urlStrings[0], int.Parse(urlStrings[1]), false));
                Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));
                return true;
            }
            catch(Exception ex)
            {
                _logger.WriteLog($"Error in connect: {ex.Message} {ex.StackTrace}", "error");
                return false;
            }
        }

        /// <summary>
        /// Updates requested agent status to Ready
        /// </summary>
        /// <param name="url">agent URL</param>
        public void AgentReady(string url)
        {
            Utils.LoadConfig();
            _logger = new Logger(Settings.Get("LOG_FILE_PATH"));
            try
            {
                _logger.WriteLog($"Received agent ready from {url}.", "info");
                _agents = Utils.ReadAgentsFromFile(Settings.Get("AGENTS_PATH"));
                Agent agent = _agents.Find(a => a.URL == url);
                if (agent == null)
                {
                    _logger.WriteLog($"Agent {url} does not exist", "error");
                    throw new Exception("Agent does not exist.");
                }
                agent.IsReady = true;
                Utils.WriteAgentListToFile(_agents, Settings.Get("AGENTS_PATH"));
            }
            catch (Exception ex)
            {
                _logger.WriteLog($"Error in agentReady: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Get from agent report of script results and write them to file script_results.json
        /// </summary>
        /// <param name="url">agent url</param>
        /// <returns>true/false if operation succeeds</returns>
        public void GetScriptResults(string url)
        {
        }

        /// <summary>
        /// Get from agent report of comparison results
        /// </summary>
        /// <param name="url">agent url</param>
        /// <returns>true/false if operation succeeds</returns>
        public void GetComparisonResults(string url)
        {  
        }

        /// <summary>
        /// Create empty json file agents.json in base folder
        /// </summary>
        private void InitAgents()
        {
            _logger.WriteLog("Init agents file.", "info");
            using (StreamWriter writer = new StreamWriter(Settings.Get("AGENTS_PATH")))
            {
                writer.Write("");
            }         
        }

        public List<Agent> GetAgents()
        {
            return _agents;
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
        private void InsertDevicesToPortal(string env, string devicesCsvFile)
        {
            try
            {
                _logger.WriteLog("Insert devices to portal.", "info");
                string pythonScriptsFolder = Settings.Get("PYTHON_SCRIPTS_PATH");
                string pythonExePath = Settings.Get("PYTHON");
                _logger.WriteLog($"Python scripts folder: {pythonScriptsFolder}, devices csv: {devicesCsvFile}", "info");
                _logger.WriteLog($"Python exe path: {pythonExePath}", "info");
                int returnCode = Utils.RunCommand(pythonExePath, "insert_devices.py", $"{env} {devicesCsvFile}", pythonScriptsFolder, Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                _logger.WriteLog($"Error in insertDevices: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Divide devices among agents in Round-Robin and distribute them to agents
        /// </summary>
        private void DistributeDevicesAmongAgents()
        {
            try
            {
                _logger.WriteLog($"Distribute device among agents.", "info");
                int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "distribute_devices.py", $"{Settings.Get("DEVICES_PATH")} {Settings.Get("AGENTS_PATH")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                _logger.WriteLog($"Error in distributeDevices: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Send automation script only to agents with Ready status
        /// </summary>
        private void SendAutomationScript()
        {
            try
            {
                _logger.WriteLog("Send automation script to agents.", "info");
                int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "send_script.py", $"{Settings.Get("AGENTS_PATH")} {Settings.Get("ACTIVATION_SCRIPT_PATH")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
                Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
            }
            catch (Exception ex)
            {
                _logger.WriteLog($"Error in sendAutomationScript: {ex.Message} {ex.StackTrace}", "error");
            }
        }

        /// <summary>
        /// Collect AWS services of given env (dev/staging/int)
        /// </summary>
        /// <param name="env">environment</param>
        private void CollectAWSServices(string env)
        {
            _logger.WriteLog("Collect AWS services.", "info");
            string cwd = Directory.GetCurrentDirectory();
            Utils.RunCommand("aws", "ecs", $"list-services --cluster {env}-ECS-Cluster", cwd, Settings.Get("AWS_SERVICES_PATH"));
        }

        /// <summary>
        /// Collect AWS instances
        /// </summary>
        private void CollectAWSInstances()
        {
            _logger.WriteLog("Collect AWS instances.", "info");
            int returnCode = Utils.RunCommand(Settings.Get("PYTHON"), "collect_aws_instances.py", $"{Settings.Get("AWS_INSTANCES_PATH")}", Settings.Get("PYTHON_SCRIPTS_PATH"), Settings.Get("OUTPUT"));
            Utils.WriteToFile(Settings.Get("RETURN_CODE"), returnCode.ToString(), false);
        } 
    }
}
