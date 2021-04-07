using Console;
using Console.Interfaces;
using Console.Models;
using Console.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace TestCenterApp.Controllers
{
    [ApiController]
    public class CenterController : Controller
    {
        ILoadTester _backEnd;

        public CenterController(ILoadTester backEnd)
        {
            _backEnd = backEnd;
        }

        // Get: index
        [HttpGet]
        [Route("index")]
        public JsonResult Index()
        {
            string cwd = Directory.GetCurrentDirectory();

            string agentsPath = ""; // Settings.Get("AGENTS_PATH");
            return Json("{About: LumenisX Test Center, cwd: " + cwd + ", agents_path: " + agentsPath + "}");
        }

        // GET: connect?port=<port>
        [HttpGet]
        [Route("connect")]
        public async Task<HttpResponseMessage> Connect(int port)
        {
            int agentPort = port;
            string agentIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
            string agentUrl = string.Join(":", agentIP, agentPort);

            bool result = await _backEnd.Connect(agentUrl);

            if (result)
            {
                string msg = $"Agent-{agentPort} was registered";
                var response = new HttpResponseMessage(HttpStatusCode.Created);
                response.Content = new StringContent(("{status:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            else
            {
                string msg = $"Agent-{agentPort} is already registered";
                var response = new HttpResponseMessage(HttpStatusCode.Conflict);
                response.Content = new StringContent(("{status:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        // GET: connect?port=<port>
        [HttpGet]
        [Route("agentReady")]
        public async Task<HttpResponseMessage> AgentReady(string port)
        {
            string agentPort = port;
            string agentIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
            string url = string.Join(":", agentIP, agentPort);

            try
            {
                bool result = await _backEnd.AgentReady(url);
                string msg = $"Agent-{agentPort} status was updated to Ready";
                var response = new HttpResponseMessage(HttpStatusCode.Conflict);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            catch (Exception ex)
            {
                Utils.WriteLog(ex.Message, "error");
                Utils.WriteLog(ex.StackTrace, "error");

                string msg = $"Error: {ex.Message}";
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        // POST: getScriptLog
        [HttpPost]
        [Route("getScriptLog")]
        public async Task<HttpResponseMessage> GetScriptLog([FromBody] ScriptLog scriptLog)
        {
            string agentIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
            string url = string.Join(':', agentIP, scriptLog.Port);

            try
            {
                await _backEnd.GetScriptLog(url, scriptLog);

                string msg = "Script log was received";
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            catch (Exception ex)
            {
                string msg = "Script log was not received";
                Utils.WriteLog(ex.Message, "error");
                Utils.WriteLog(ex.StackTrace, "error");

                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        // POST: getComparisonResults
        [HttpPost]
        [Route("getComparisonResults")]
        public async Task<HttpResponseMessage> GetComparisonResults()
        {
            string agentIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();

            try
            {
                string jsonContent = await Request.ReadFromJsonAsync<string>();
                await _backEnd.GetComparisonResults(agentIP, jsonContent);
                string msg = "Comparison results were received";
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            catch (Exception ex)
            {
                Utils.WriteLog(ex.Message, "error");
                Utils.WriteLog(ex.StackTrace, "error");

                string msg = "Comparison results were not received";
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        // POST: events
        [HttpPost]
        [Route("sendEventsLog")]
        public async Task<HttpResponseMessage> SendEventsLog([FromBody] EventsLog eventsLog)
        {
            string agentIP = Request.HttpContext.Connection.RemoteIpAddress.ToString();
            var url = agentIP + ":" + eventsLog.Port;

            try
            {
                await _backEnd.GetComparisonResults(url, eventsLog);
                string msg = "Comparison results were received";
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            catch (Exception ex)
            {
                Utils.WriteLog(ex.Message, "error");
                Utils.WriteLog(ex.StackTrace, "error");

                string msg = "Comparison results were not received";
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        [HttpGet]
        [Route("getAgents")]
        public HttpResponseMessage GetAgents()
        {
            List<string> agents = _backEnd.GetAgents();
            string agentsString = string.Join(",", agents);
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(("{agents:" + agentsString + ", count:" + agents.Count + "}"), Encoding.UTF8, "application/json");
            return response;
        }

        [HttpGet]
        [Route("Reset")]
        public HttpResponseMessage Reset()
        {
            _backEnd.Reset();
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(("{Result: center was reset}"), Encoding.UTF8, "application/json");
            return response;
        }

        [HttpGet]
        [Route("testcmd")]
        public HttpResponseMessage TestCommand(string num)
        {

            string result = string.Empty; // _backEnd.TestCommand(num);

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var jsonText = "{returnStatus:" + result + "}";
            response.Content = new StringContent(jsonText, Encoding.UTF8, "application/json");
            return response;
        }
    }
}
