using Console;
using Console.Interfaces;
using TestCenterConsole.Models;
using TestCenterConsole.Utilities;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using TestCenter.LiteDb;
using TestCenter.ViewModels;
using Console.Models;
using Microsoft.AspNetCore.SignalR;
using TestCenter.Hubs;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using Console.Utilities;

namespace TestCenterApp.Controllers
{
    public class UIController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        private IBackEndInterface _backEnd;
        private Settings _settings;

        private static bool _subscribed = false;
        //private readonly ITestCenterService _testCenterDbService;

        private static TestCenterViewModel _model = new TestCenterViewModel();

        private IHubContext<TestCenterHub> _hub;

        public UIController(IBackEndInterface backEnd, 
                            Settings settings, 
                            IHubContext<TestCenterHub> hub,
                            IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _backEnd = backEnd;
            _settings = settings;
            _hub = hub;

            if (!_subscribed)
            {
                _backEnd.AwsDataUpdated += UpdateAwsDataUI;
                _backEnd.StageDataUpdated += UpdateStageDataUI;
                _backEnd.AgentDataUpdated += UpdateAgentDataUI;

                _subscribed = true;
            }
            
        }

        private void UpdateAgentDataUI(object sender, AgentData agentData)
        {
            var agentIndex = _model.ProgressData.AgentsData.FindIndex(a => a.URL == agentData.URL);
            if (agentIndex == -1)
            {
                _model.ProgressData.AgentsData.Add(agentData);
            }
            else
            {
                _model.ProgressData.AgentsData[agentIndex].ClientsNumber = agentData.ClientsNumber;
                _model.ProgressData.AgentsData[agentIndex].ServersNumber = agentData.ServersNumber;
                _model.ProgressData.AgentsData[agentIndex].TotalEvents = agentData.TotalEvents;
               
                if (agentData.Devices.Count != 0)
                {
                    _model.ProgressData.AgentsData[agentIndex].Devices = agentData.Devices;
                }               

            }

            _hub.Clients.All.SendAsync("agentsData", _model.ProgressData.AgentsData);
        }

        private void UpdateAwsDataUI(object sender, AwsMetricsData measurementData)
        {
            var awsDataPred = _model.ProgressData.AwsMetricsData.Where(d => d.CPUUtilization[0].Time.Equals(measurementData.CPUUtilization[0].Time));
            
            if (awsDataPred.Count() == 0)
            {
                _model.ProgressData.AwsMetricsData.Add(measurementData);
                _hub.Clients.All.SendAsync("awsData", measurementData);
            }            
            else
            {
                var awsData = new AwsMetricsData();
                awsData.EventsInRDS = measurementData.EventsInRDS;

                var awsDataModel = awsDataPred.FirstOrDefault();
                awsDataModel.EventsInRDS = measurementData.EventsInRDS;

                _hub.Clients.All.SendAsync("awsData", awsData);
            }
        }

        private void UpdateStageDataUI(object sender, StageData stageData)
        {
            //var stagePred = _model.ProgressData.StageData.Where(s => s.Stage == stageData.Stage);
            //if (stagePred.Count() == 0)
            {
                _model.ProgressData.StageData.Add(stageData);
            }
            //else
            //{
            //    stagePred.FirstOrDefault().DevicesNumberFinished = stageData.DevicesNumberFinished;
            //}

            _hub.Clients.All.SendAsync("stageData", stageData);
        }

        public async Task<ActionResult> Index()
        {
            var feature = HttpContext.Features.Get<IHttpConnectionFeature>();
            _model.IPAddress = feature?.LocalIpAddress?.ToString();
            _model.Port = feature?.LocalPort.ToString();

            List<AgentViewModel> newAgents = new List<AgentViewModel>();
            foreach (var agent in _model.Agents)
            {
                newAgents.Add(await GetAgentInitData(agent));
            }

            _model.Agents = newAgents;
            return View(_model);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult SetSettings([FromForm] TestCenterSettings settings)
        {
            _backEnd.UpdateCenterSettings(settings);

            return RedirectToAction("index");

        }

        public IActionResult ShowCenterSettings()
        {
            _model.Environment = BiotEnvironment.INT;
            _model.LogFilePath = _settings["LOG_FILE_PATH"];
            _model.ResultsFolder = _settings["DEVICE_RESULTS_DIR"];
            _model.ScriptResultsFolder = _settings["DEVICE_LOGS_DIR"];
            _model.Status = AgentStatus.LIVE;

            //specify the name or path of the partial view
            return PartialView("_Modals/_CenterSettings", _model);
        }

        public IActionResult GetScriptLog(string device)
        {
            var scriptLog = _backEnd.GetScriptLog(device);

            return Ok(scriptLog);
        }

        public IActionResult ShowAddAgent(int? id)
        {
            AgentViewModel agent = null;
            if (!id.HasValue)
            {
                agent = new AgentViewModel();
                agent.Environment = (BiotEnvironment)Enum.Parse(typeof(BiotEnvironment), _settings["ENV"], true);
                agent.Port = 5001;
                agent.Status = AgentStatus.OFFLINE;
            }
            else
            {                
                agent = _model.Agents[id.Value];
                agent.Id = id.Value;
            }

            //specify the name or path of the partial view
            return PartialView("_Modals/_AddAgent", agent);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult AddAgent([FromForm] AgentViewModel agentVM)
        {
            if (agentVM.Id.HasValue)
            {
                _model.Agents[agentVM.Id.Value] = agentVM;                              
            }
            else
            {
                if (_model.Agents.Count(a => (a.Name == agentVM.Name) || (a.IPAddress == agentVM.IPAddress && a.Port == agentVM.Port)) == 0)
                {
                    _model.Agents.Add(agentVM);
                }                
            }            

            return RedirectToAction("index");
        }

        public IActionResult ShowAddScenario(int? id)
        {
            ScenarioViewModel scenario = null;
            if (!id.HasValue)
            {
                scenario = new ScenarioViewModel();
                scenario.InsertionStrategy = (InsertionStrategy)Enum.Parse(typeof(InsertionStrategy), _settings["SCENARIO_INSERTION_STRATEGY"]);
                scenario.MinutesServicesStopped = int.Parse(_settings["SCENARIO_MINUTES_TO_KEEP_STOPPED"]);
                scenario.StopAws = bool.Parse(_settings["SCENARIO_STOP_AWS"]);
                scenario.MinutesToWaitForAgents = int.Parse(_settings["SCENARIO_MINUTES_TO_CONNECT"]);
                scenario.DevicesNumber = 1;

                var servicesStr = _settings["SCENARIO_SERVICES_TO_STOP"].Split(',');

                var services = new List<AWSServices>();
                foreach (var item in servicesStr)
                {
                    services.Add((AWSServices)Enum.Parse(typeof(AWSServices), item));
                }

                scenario.Services = services;
            }
            else
            {
                scenario = _model.Scenarios[id.Value];
                scenario.Id = id.Value;
            }

            //specify the name or path of the partial view
            return PartialView("_Modals/_AddScenario", scenario);
        }


        [ValidateAntiForgeryToken]
        [HttpPost]
        public ActionResult AddScenario([FromForm] ScenarioViewModel scenarioVM)
        {
            if (scenarioVM.Id.HasValue)
            {
                _model.Scenarios[scenarioVM.Id.Value] = scenarioVM;
            }
            else
            {
                _model.Scenarios.Add(scenarioVM);
            }

            var scenario = new Scenario();
            scenario.DevicesNumber = scenarioVM.DevicesNumber;
            scenario.DevicesType = scenarioVM.DevicesType;
            scenario.InsertionStrategy = scenarioVM.InsertionStrategy;
            scenario.MinutesServicesStopped = scenarioVM.MinutesServicesStopped;
            scenario.MinutesToWaitForAgents = scenarioVM.MinutesToWaitForAgents;
            scenario.Name = scenarioVM.Name;
            scenario.Services = scenarioVM.Services;
            scenario.StopAws = scenarioVM.StopAws;
            
            _backEnd.UpdateScenarioSettings(scenario);

            return RedirectToAction("index");

        }


        private async Task<AgentViewModel> GetAgentInitData(AgentViewModel agent)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, agent.IPAddress + ":" + agent.Port.ToString() + "/getAgentSettings");

            var client = _clientFactory.CreateClient();
            bool isLive = false;
            try
            {
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();

                    var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
                    agent.AgentDirPath = dic["AgentDirPath"];

                    isLive = true;
                }
            }
            catch (Exception ex)
            {
                Utils.WriteLog(ex.Message, "error");
                Utils.WriteLog(ex.StackTrace, "error");
            }
            agent.Status = isLive ? AgentStatus.LIVE : AgentStatus.OFFLINE;

            return agent;
        }

        public async Task<IActionResult> StopScenario()
        {

            foreach (var agent in _model.Agents)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, agent.IPAddress + ":" + agent.Port.ToString() + "/stop");

                var client = _clientFactory.CreateClient();
                
                try
                {
                    var response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();

                        var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
                        var result  = bool.Parse(dic["Result"]);
                        
                        if (result)
                        {
                            agent.Status = AgentStatus.LIVE;
                        }
                    }
                }
                catch(Exception ex)
                {
                    Utils.WriteLog(ex.Message, "error");
                    Utils.WriteLog(ex.StackTrace, "error");
                }
            }

            return RedirectToAction("index");
        }


        public async Task<IActionResult> StartScenario(int id)
        {
            bool result = true;
            foreach (var agent in _model.Agents.Where(a => a.Status == AgentStatus.LIVE))
            {                

                var client = _clientFactory.CreateClient();

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, agent.IPAddress + ":" + agent.Port.ToString() + "/stop");
                    var response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        Utils.WriteLog($"Stopped running devices processes on agent {agent.Name}", "info");
                    }

                    request = new HttpRequestMessage(HttpMethod.Get, agent.IPAddress + ":" + agent.Port.ToString() + "/init");
                    response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();

                        var dic = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseString);
                        var res = bool.Parse(dic["Result"]);

                        Utils.WriteLog($"Sent init to on agent {agent.Name}", "info");

                        result = result && res;
                    }
                }
                catch (Exception ex)
                {
                    Utils.WriteLog(ex.Message, "error");
                    Utils.WriteLog(ex.StackTrace, "error");
                }
            }

            if (result)
            {
                _model.Scenarios[id].Status = ScenarioStatus.EXECUTING;
                _model.ProgressData.ScenarioName = _model.Scenarios[id].Name;

            }

            return await Task.Run<ActionResult>(() => { return RedirectToAction("index"); });
            
        }

        
        public IActionResult Progress()
        {
            return View("Progress", _model);
        }

        public IActionResult Results()
        {
            return View("Results", _model);
        }

    }
}
