﻿using Backend;
using Console;
using Console.Models;
using Console.Utilities;
using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;


namespace TestCenterApp.Controllers
{
    public class CenterController : ApiController
    {
        IBackEndInterfaces backEnd;

        public CenterController()
        {
            backEnd = new BackEnd();
        }

        // Get: index
        [HttpGet]
        [Route("index")]
        public JsonResult<string> Index()
        {
            string cwd = Directory.GetCurrentDirectory();
            Utils.LoadConfig();
            string agentsPath = Settings.Get("AGENTS_PATH");
            return Json("{About: LumenisX Test Center, cwd: " + cwd + ", agents_path: " + agentsPath + "}");
        }

        // GET: connect?port=<port>
        [HttpGet]
        [Route("connect")]
        public HttpResponseMessage Connect(int port)
        {
            int agentPort = port;
            string agentIP = HttpContext.Current.Request.ServerVariables["REMOTE_ADDR"];
            string agentUrl = string.Join(":", agentIP, agentPort);
            bool result = backEnd.Connect(agentUrl);

            if (result)
            {
                string msg = $"Agent-{agentPort} was registered";
                var response = Request.CreateResponse(HttpStatusCode.Created);
                response.Content = new StringContent(("{status:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            else
            {
                string msg = $"Agent-{agentPort} is already registered";
                var response = Request.CreateResponse(HttpStatusCode.Conflict);
                response.Content = new StringContent(("{status:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        // GET: connect?port=<port>
        [HttpGet]
        [Route("agentReady")]
        public HttpResponseMessage AgentReady(string port)
        {
            string agentPort = port;
            string agentIP = Request.RequestUri.Host.ToString();
            string url = string.Join(":", agentIP, agentPort);
            try
            {
                backEnd.AgentReady(url);
                string msg = $"Agent-{agentPort} status was updated to Ready";
                var response = Request.CreateResponse(HttpStatusCode.Conflict);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            catch (Exception ex)
            {
                string msg = $"Error: {ex.Message}";
                var response = Request.CreateResponse(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        // POST: getScriptResults
        [HttpPost]
        [Route("getScriptResults")]
        public HttpResponseMessage GetScriptResults()
        {
            string url = Request.RequestUri.AbsoluteUri;
            string agentIP = Request.RequestUri.Host.ToString();
            HttpContent requestContent = Request.Content;
            try
            {
                string content = requestContent.ReadAsStringAsync().Result;
                Utils.WriteToFile(Settings.Get("SCRIPT_RESULTS_PATH"), content, false);
                string msg = "Script results were received";
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            catch (Exception)
            {
                string msg = "Script results were received";
                var response = Request.CreateResponse(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(("{result:" + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        // POST: getComparisonResults
        [HttpPost]
        [Route("getComparisonResults")]
        public HttpResponseMessage GetComparisonResults()
        {
            string url = Request.RequestUri.AbsoluteUri;
            string agentIP = Request.RequestUri.Host.ToString();
            HttpContent requestContent = Request.Content;
            try
            {
                string content = requestContent.ReadAsStringAsync().Result;
                Utils.WriteToFile(Settings.Get("COMPARISON_RESULTS_PATH"), content, false);
                string msg = "Comparison results were received";
                var response = Request.CreateResponse(HttpStatusCode.OK);
                response.Content = new StringContent(("{result: " + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                var response = Request.CreateResponse(HttpStatusCode.InternalServerError);
                response.Content = new StringContent(("{result: " + msg + "}"), Encoding.UTF8, "application/json");
                return response;
            }
        }

        [HttpGet]
        [Route("testcmd")]
        public HttpResponseMessage TestCommand(string num)
        {
            Utils.LoadConfig();
            string pythonPath = (Settings.Get("PYTHON"));
            int returnCode = Utils.RunCommand(pythonPath, "pythonScript.py", num, @"D:\Temp", @"D:\test_center\out.txt");
            var output = Utils.ReadFileContent(@"D:\test_center\out.txt");
            var response = Request.CreateResponse(HttpStatusCode.OK);
            var jsonText = "{returnCode:" + returnCode + ", " + "output:" + output + "}";
            response.Content = new StringContent(jsonText, Encoding.UTF8, "application/json");
            return response;
        }
    }
}
