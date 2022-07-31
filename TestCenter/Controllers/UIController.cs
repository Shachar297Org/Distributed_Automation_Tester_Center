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
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Drawing;
using Syncfusion.Pdf.Grid;
using Syncfusion.Pdf.Tables;
using System.Data;

namespace TestCenterApp.Controllers
{
    public class UIController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        private ILoadTester _loadTester;
        private Settings _settings;

        private static bool _subscribed = false;
        //private readonly ITestCenterService _testCenterDbService;

        private static TestCenterViewModel _model = new TestCenterViewModel();

        private IHubContext<TestCenterHub> _hub;

        public UIController(ILoadTester backEnd, 
                            Settings settings, 
                            IHubContext<TestCenterHub> hub,
                            IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
            _loadTester = backEnd;
            _settings = settings;
            _hub = hub;

            if (!_subscribed)
            {
                _loadTester.AwsDataUpdated += UpdateAwsDataUI;
                _loadTester.StageDataUpdated += UpdateStageDataUI;
                _loadTester.AgentDataUpdated += UpdateAgentDataUI;

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
            var msmtIndex = measurementData.CPUUtilization.ToList().FindIndex(data => data != null);
            if (msmtIndex == -1)
            {
                return;
            }            

            var awsDataPred = _model.ProgressData.AwsMetricsData.Where(d => d.CPUUtilization[msmtIndex].Time.Equals(measurementData.CPUUtilization[msmtIndex].Time));
            
            if (awsDataPred.Count() == 0)
            {
                if (_model.ProgressData.AwsMetricsData.Count > 0)
                {
                    var lastData = _model.ProgressData.AwsMetricsData.Last();
                    for (int i = 0; i < measurementData.CPUUtilization.Length; i++)
                    {
                        if (measurementData.CPUUtilization[i] == null)
                        {
                            measurementData.CPUUtilization[i] = lastData.CPUUtilization[i];
                            measurementData.CPUUtilization[i].Time = measurementData.CPUUtilization[msmtIndex].Time;
                        }

                        if (measurementData.MemoryUtilization[i] == null)
                        {
                            measurementData.MemoryUtilization[i] = lastData.MemoryUtilization[i];
                            measurementData.MemoryUtilization[i].Time = measurementData.MemoryUtilization[msmtIndex].Time;
                        }
                    }
                }
                

                _model.ProgressData.AwsMetricsData.Add(measurementData);
                _hub.Clients.All.SendAsync("awsData", measurementData);
            }            
            else
            {
                var awsData = new AwsMetricsData();
                awsData.EventsInRDS = measurementData.EventsInRDS;

                var awsDataModel = awsDataPred.LastOrDefault();
                awsDataModel.EventsInRDS = measurementData.EventsInRDS;

                _hub.Clients.All.SendAsync("awsData", awsData);
            }
        }

        private void UpdateStageDataUI(object sender, StageData stageData)
        {
            if (stageData.IsNewStage)
            {
                _model.ProgressData.StageData.Add(stageData);
            }
            else
            {
                _model.ProgressData.StageData.Last().DevicesNumberFinished = stageData.DevicesNumberFinished;                
            }

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
            _loadTester.UpdateCenterSettings(settings);

            return RedirectToAction("Index");

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
            var scriptLog = _loadTester.GetScriptLog(device);

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

            return RedirectToAction("Index");
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
            
            _loadTester.UpdateScenarioSettings(scenario);

            return RedirectToAction("Index");

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
                    agent.AgentDirPath = dic["agentDirPath"];

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


        private PdfDocument CreateReport()
        {
            //Create a new PDF document.
            /* PdfDocument document = new PdfDocument();

            //Add a page to the document.
            PdfPage page = document.Pages.Add();

            //Create PDF graphics for the page.
            PdfGraphics graphics = page.Graphics;

            //Set the standard font.
            PdfFont font = new PdfStandardFont(PdfFontFamily.Helvetica, 20);

            //Draw the text.
            graphics.DrawString("Hello World!", font, PdfBrushes.Black, new PointF(0, 0));
            
            return document; */

            var report = new PdfDocument();

            try
            {
                //Adds a page.
                PdfPage page = report.Pages.Add();

                //Create PDF graphics for the page.
                PdfGraphics graphics = page.Graphics;

                //Set the standard font.
                PdfFont fontHeader = new PdfStandardFont(PdfFontFamily.Helvetica, 18);

                //Draw the header

                PdfStringFormat headerFormat = new PdfStringFormat() { 
                    WordWrap = PdfWordWrapType.Word, 
                    LineLimit = true, 
                    Alignment = PdfTextAlignment.Center 
                };

                PdfTextElement headerElement = new PdfTextElement("Scale Test Report") { 
                    Font = fontHeader, 
                    Brush = PdfBrushes.Black, 
                    StringFormat = headerFormat
                };

                PdfLayoutResult headerResult = headerElement.Draw(page, new RectangleF(0, 10, report.PageSettings.Width, 30));

                //Set the standard font.
                PdfFont fontTitle = new PdfStandardFont(PdfFontFamily.Helvetica, 12);

                //Draw the header

                PdfStringFormat titleFormat = new PdfStringFormat()
                {
                    WordWrap = PdfWordWrapType.Word,
                    LineLimit = true,
                    Alignment = PdfTextAlignment.Left
                };

                var startTime = _model.ProgressData.StageData[1].Time;
                PdfTextElement startTimeTitleElement = new PdfTextElement($"Test start time: {startTime}")
                {
                    Font = new PdfStandardFont(PdfFontFamily.Helvetica, 8),
                    Brush = PdfBrushes.Black,
                    StringFormat = titleFormat
                };

                var bottom = headerResult.Bounds.Top + headerResult.Bounds.Height + 40;
                PdfLayoutResult startTimeTitleResult = startTimeTitleElement.Draw(page, new RectangleF(0, bottom, report.PageSettings.Width, bottom));

                PdfTextElement generalTitleElement = new PdfTextElement("General data")
                {
                    Font = fontTitle,
                    Brush = PdfBrushes.Black,
                    StringFormat = titleFormat
                };

                bottom = startTimeTitleResult.Bounds.Top + startTimeTitleResult.Bounds.Height + 20;
                PdfLayoutResult generalTitleResult = generalTitleElement.Draw(page, new RectangleF(0, bottom, report.PageSettings.Width, bottom));

                //Create a PdfGrid
                PdfGrid generalTable = new PdfGrid();

                //Create a DataTable
                DataTable dataTable = new DataTable();

                //Add columns to the DataTable
                dataTable.Columns.Add("Scenario name");
                dataTable.Columns.Add("Devices type");
                dataTable.Columns.Add("Number of devices");
                dataTable.Columns.Add("Events per device");
                dataTable.Columns.Add("Number of agents");
                dataTable.Columns.Add("Test duration");

                //Add rows to the DataTable
                var finishedScenario = _model.Scenarios.Where(sc => sc.Status == ScenarioStatus.FINISHED).FirstOrDefault();

                var endTime = _model.ProgressData.StageData.Last().Time;

                var diff = endTime.Subtract(startTime);
                var res = String.Format("{0}h:{1}m:{2}sec", diff.Hours, diff.Minutes, diff.Seconds);
                
                //Adds row.
                dataTable.Rows.Add(new object[] { finishedScenario.Name, finishedScenario.DevicesType, finishedScenario.DevicesNumber,
                                        _loadTester.GetAgentData().FirstOrDefault().TotalEvents, _model.Agents.Count, res });


                //Assign data source.
                generalTable.DataSource = dataTable;

                //Create string format for PdfGrid
                PdfStringFormat format = new PdfStringFormat();
                format.Alignment = PdfTextAlignment.Center;
                format.LineAlignment = PdfVerticalAlignment.Bottom;

                //Assign string format for each columns in PdfGrid
                foreach (PdfGridColumn column in generalTable.Columns)
                    column.Format = format;

                //Apply a built in style
                generalTable.ApplyBuiltinStyle(PdfGridBuiltinStyle.GridTable4Accent6);


                //Set properties to paginate the grid.
                PdfGridLayoutFormat layoutFormat = new PdfGridLayoutFormat();
                layoutFormat.Break = PdfLayoutBreakType.FitPage;
                layoutFormat.Layout = PdfLayoutType.Paginate;


                //Draw grid to the page of PDF document
                bottom = generalTitleResult.Bounds.Top + generalTitleResult.Bounds.Height + 10;
                PdfLayoutResult generalTableResult = generalTable.Draw(page, new PointF(0, bottom), layoutFormat);

                PdfTextElement awsTitleElement = new PdfTextElement("Max AWS Services CPU and Memory usages in %")
                {
                    Font = fontTitle,
                    Brush = PdfBrushes.Black,
                    StringFormat = titleFormat
                };

                bottom = generalTableResult.Bounds.Top + generalTableResult.Bounds.Height + 20;
                PdfLayoutResult awsTitleResult = awsTitleElement.Draw(page, new RectangleF(0, bottom, report.PageSettings.Width, bottom));


                //var secondPage = report.Pages.Add();
                //Create a PdfGrid
                PdfGrid awsDataTable = new PdfGrid();

                //Create a DataTable
                dataTable = new DataTable();

                dataTable.Columns.Add("Device Service CPU %");
                dataTable.Columns.Add("Device Service Memory %");
                dataTable.Columns.Add("Facade Service CPU %");
                dataTable.Columns.Add("Facade Service Memory %");
                dataTable.Columns.Add("Processing Service CPU %");
                dataTable.Columns.Add("Processing Service Memory %");

                dataTable.Rows.Add(new object[] { _model.ProgressData.AwsMetricsData.Max(d => d.CPUUtilization[0].Value),
                                                     _model.ProgressData.AwsMetricsData.Max(d => d.MemoryUtilization[0].Value),
                                                     _model.ProgressData.AwsMetricsData.Max(d => d.CPUUtilization[1].Value),
                                                     _model.ProgressData.AwsMetricsData.Max(d => d.MemoryUtilization[1].Value),
                                                     _model.ProgressData.AwsMetricsData.Max(d => d.CPUUtilization[2].Value),
                                                     _model.ProgressData.AwsMetricsData.Max(d => d.MemoryUtilization[2].Value) });

               
                //Assign data source.
                awsDataTable.DataSource = dataTable;

                //Assign string format for each columns in PdfGrid
                foreach (PdfGridColumn column in awsDataTable.Columns)
                    column.Format = format;

                //Apply a built in style
                awsDataTable.ApplyBuiltinStyle(PdfGridBuiltinStyle.GridTable4Accent6);

                //Draw grid to the page of PDF document
                bottom = awsTitleResult.Bounds.Top + awsTitleResult.Bounds.Height + 10;
                PdfLayoutResult awsTableResult = awsDataTable.Draw(page, new PointF(0, bottom), layoutFormat);

                /* 
                 * For testing
                 * 
                var devicesA = new List<LumenisXDevice>();
                var devicesB = new List<LumenisXDevice>();
                for (int i = 0; i < 400; i++)
                {
                    var device = new LumenisXDevice("AURA", i.ToString());
                    device.Finished = true;
                    device.Success = true;
                    device.EventsInRDS = 7;

                    if (i % 2 == 0)
                    {
                        devicesA.Add(device);
                    }
                    else
                    {
                        devicesB.Add(device);
                    }
                }

                var agentA = new AgentData();
                agentA.Devices = devicesA;

                var agentB = new AgentData();
                agentB.Devices = devicesB;                

                _model.ProgressData.AgentsData = new List<AgentData> { agentA, agentB };
                */

                //Create a DataTable
                dataTable = new DataTable();

                dataTable.Columns.Add("Device Serial Number");
                dataTable.Columns.Add("Device Type");
                dataTable.Columns.Add("Events in RDS");

                bool success = true;

                foreach (var agent in _model.ProgressData.AgentsData)
                {
                    var failedDevices = agent.Devices.Where(d => !d.Success);

                    if (failedDevices != null && failedDevices.Count() > 0)
                    {
                        foreach (var failedDevice in failedDevices)
                        {
                            dataTable.Rows.Add(new object[] { failedDevice.DeviceSerialNumber, failedDevice.DeviceType, failedDevice.EventsInRDS });
                        }

                        success = false;

                    }

                }

                if (success)
                {
                    PdfTextElement successElement = new PdfTextElement("There are no failed devices. The scale test finished successfuly!")
                    {
                        Font = fontTitle,
                        Brush = PdfBrushes.Green,
                        StringFormat = titleFormat
                    };

                    bottom = awsTableResult.Bounds.Top + awsTableResult.Bounds.Height + 20;
                    PdfLayoutResult failedDevicesTitleResult = successElement.Draw(page, new RectangleF(0, bottom, report.PageSettings.Width, bottom));

                }
                else
                {
                    PdfTextElement failedDevicesTitleElement = new PdfTextElement("Failed devices list")
                    {
                        Font = fontTitle,
                        Brush = PdfBrushes.Red,
                        StringFormat = titleFormat
                    };

                    bottom = awsTableResult.Bounds.Top + awsTableResult.Bounds.Height + 20;
                    PdfLayoutResult failedDevicesTitleResult = failedDevicesTitleElement.Draw(page, new RectangleF(0, bottom, report.PageSettings.Width, bottom));

                    bottom = failedDevicesTitleResult.Bounds.Top + failedDevicesTitleResult.Bounds.Height + 10;

                    var faildeAgentTable = new PdfGrid();

                    //Assign data source.
                    faildeAgentTable.DataSource = dataTable;

                    //Assign string format for each columns in PdfGrid
                    foreach (PdfGridColumn column in faildeAgentTable.Columns)
                        column.Format = format;

                    //Apply a built in style
                    faildeAgentTable.ApplyBuiltinStyle(PdfGridBuiltinStyle.GridTable4Accent6);

                    //Draw grid to the page of PDF document                        
                    PdfLayoutResult faildeAgentTableResult = faildeAgentTable.Draw(page, new PointF(0, bottom), layoutFormat);
                }


            }
            catch (Exception ex)
            {
                Utils.WriteLog(ex.Message, "error");
                Utils.WriteLog(ex.StackTrace, "error");
            }

            return report;

        }

        
        public async Task<IActionResult> StopScenario([FromQuery]string action)
        {
            var runningScenario = _model.Scenarios.Where(sc => sc.Status == ScenarioStatus.EXECUTING).FirstOrDefault();
            if (runningScenario != null)
            {
                runningScenario.Status = ScenarioStatus.FINISHED;
            }
            else
            {
                return Ok();
            }

            foreach (var agentProgress in _model.ProgressData.AgentsData)
            {
                agentProgress.Status = AgentStatus.FINISHED.ToString();
                foreach (var device in agentProgress.Devices)
                {
                    device.Finished = true;
                }
                agentProgress.ClientsNumber = 0;
                agentProgress.ServersNumber = 0;
            }
            

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
                        var result  = bool.Parse(dic["result"]);
                        
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

            _loadTester.Stop();

            return Ok();

        }


        public IActionResult DownloadReport()
        {

            var report = CreateReport();

            var fileName = $"{_model.ProgressData.ScenarioName}-{_model.Scenarios.Where(sc => sc.Status == ScenarioStatus.FINISHED).FirstOrDefault().DevicesType}.pdf";

            //Creates an instance of memory stream
            MemoryStream stream = new MemoryStream();

            //Save the document stream
            report.Save(stream);

            //close the document
            //report.Close(true);

            // Set the position as '0'.
            stream.Position = 0;

            //return new FileStreamResult(stream, "application/pdf") { FileDownloadName = fileName };
            return File(stream, "application/pdf", fileName);

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
                        var res = bool.Parse(dic["result"]);

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

            return await Task.Run<ActionResult>(() => { return Ok(); });
            
        }

        
        public IActionResult Progress()
        {
            return View("Progress", _model);
        }

        public IActionResult Results()
        {
            return View("Results", _model);
        }

        public IActionResult Reset()
        {
            _model = new TestCenterViewModel();

            return View("Index", _model);

        }

    }
}
