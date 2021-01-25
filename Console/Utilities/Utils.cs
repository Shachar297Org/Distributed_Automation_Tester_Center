﻿using Console.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Console.Utilities
{
    public static class Utils
    {
        private static ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Run Windows command in background and wait until it finishes
        /// </summary>
        /// <param name="exeFile">Executable file</param>
        /// <param name="cmd">command</param>
        /// <param name="args">arguments</param>
        /// <param name="outputFile">Output file path</param>
        /// <returns>Command return code</returns>
        public static int RunCommand(string exeFile, string cmd, string args, string cwd, string outputFile)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = exeFile;
            start.Arguments = string.Format("{0} {1}", cmd, args);
            start.WorkingDirectory = cwd;
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;

            int exit = 0;

            using (var process = new Process
            {
                StartInfo = start
            })
            {
                process.Start();
                var result = Task.Run(() => process.StandardOutput.ReadToEnd());
                var error = Task.Run(() => process.StandardError.ReadToEnd());

                process.WaitForExit();

                WriteToFile(outputFile, result.Result, append: true);
                WriteToFile(outputFile, error.Result, append: true);

                exit = process.ExitCode;

            }

            return exit;
        }

        /// <summary>
        /// Run Windows command in background asynchronically
        /// </summary>
        /// <param name="exeFile">Executable file</param>
        /// <param name="cmd">command</param>
        /// <param name="args">arguments</param>
        /// <param name="outputFile">Output file path</param>
        /// <returns>Command return code</returns>
        public static async Task RunCommandAsync(string exeFile, string cmd, string args, string cwd, string outputFile)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = exeFile;
            start.Arguments = string.Format("{0} {1}", cmd, args);
            start.WorkingDirectory = cwd;
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;

            var result = await ProcessAsyncHelper.RunAsync(start);

            WriteToFile(outputFile, result.StdOut, append: true);
            WriteToFile(outputFile, result.StdErr, append: true);
            
        }

        private static void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            
        }

        /// <summary>
        /// Read agent list from json file
        /// </summary>
        /// <param name="jsonFile">json file</param>
        /// <returns>list of agent objects</returns>
        public static List<Agent> ReadAgentsFromFile(string agentsFilePath)
        {
            List<Agent> agentList = null;
            try
            {
                using (StreamReader reader = new StreamReader(agentsFilePath))
                {
                    string json = reader.ReadToEnd();
                    agentList = JsonConvert.DeserializeObject<List<Agent>>(json);
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                agentList = null;
            }
            if (agentList == null)
            {
                agentList = new List<Agent>();
            }
            return agentList;
        }

        /// <summary>
        /// Write agent list to json file
        /// </summary>
        /// <param name="list">agent list</param>
        /// <param name="jsonFile">json file</param>
        public static void WriteAgentListToFile(List<Agent> list, string agentsFilePath)
        {
            string jsonObj = JsonConvert.SerializeObject(list);
            WriteToFile(agentsFilePath, jsonObj, append: false);
        }

        /// <summary>
        /// Read file content
        /// </summary>
        /// <param name="filePath">file path</param>
        /// <returns>file content</returns>
        public static string ReadFileContent(string filePath)
        {
            string content = "";
            using (StreamReader reader=new StreamReader(filePath))
            {
                content = reader.ReadToEnd();
            }
            return content;
        }


        /// <summary>
        /// Write content to file
        /// </summary>
        /// <param name="filePath">file path</param>
        /// <param name="content">content</param>
        /// <param name="append">append to file or not</param>
        public static void WriteToFile(string filePath, string content, bool append)
        {
            _readWriteLock.EnterWriteLock();
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, append))
                {
                    writer.Write(content);
                }
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
            
        }

        public static void LoadConfig()
        {
            string configFilePath = "D:/Config/test_center_config.txt";
            using (var streamReader = File.OpenText(configFilePath))
            {
                Settings.settingsDict["CONFIG_FILE"] = configFilePath;
                var lines = streamReader.ReadToEnd().Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] fields = line.Split('=');
                    Settings.settingsDict[fields[0]] = fields[1];
                }
            }
        }

        public static void WriteLog(string msg, string level)
        {
            _readWriteLock.EnterWriteLock();
            try
            {
                using (StreamWriter writer = new StreamWriter(Settings.Get("LOG_FILE_PATH"), append: true))
                {
                    string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    writer.WriteLine($"{nowTime} [{level.ToUpper()}] : {msg}");
                }
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }               
        }

        public static void WriteRecordsToCsv(string csvFileByDevice, List<Event> events)
        {
            using (StreamWriter writer = new StreamWriter(csvFileByDevice))
            {
                writer.WriteLine($"SerialNumber,DeviceType,EventKey,EventValue,CreationTime");
                foreach (Event e in events)
                {
                    writer.WriteLine($"{e.EventDeviceSerialNumber},{e.EventDeviceType},{e.EventKey},{e.EventValue},{e.CreationTime}");
                }     
            }
        }
    }
}
