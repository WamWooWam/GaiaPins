using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using GaiaPins.Commands;
using GaiaPins.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LogLevel = DSharpPlus.LogLevel;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace GaiaPins
{
    class Startup
    {
        private ILogger<Startup> _logger;
        private ILogger<DiscordClient> _clientLogger;
        private ILogger<CommandsNextExtension> _commandsLogger;
        private DiscordClient _client;
        private CommandsNextExtension _commands;
        private DiscordWebhookClient _webhookClient;

        public Startup(ILogger<Startup> logger,
                       ILogger<DiscordClient> discordLogger,
                       ILogger<CommandsNextExtension> commandsLogger,
                       DiscordClient discord,
                       DiscordWebhookClient webhookClient,
                       CommandsNextExtension commands)
        {
            _logger = logger;
            _clientLogger = discordLogger;
            _commandsLogger = commandsLogger;
            _client = discord;
            _webhookClient = webhookClient;
            _commands = commands;
        }

        public Task Configure(IHost host) // task in case i need to do asynchronous work here
        {
            _client.DebugLogger.LogMessageReceived += DebugLogger_LogMessageReceived;
            _commands.RegisterCommands<PinsCommands>();
            _commands.CommandExecuted += _commands_CommandExecuted;
            _commands.CommandErrored += _commands_CommandErrored;

            return Task.CompletedTask;
        }

        private Task _commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            _commandsLogger.LogInformation("Command '{0}' executed by @{1}#{2} successfully!", e.Command.Name, e.Context.User.Username, e.Context.User.Discriminator);
            return Task.CompletedTask;
        }

        private async Task _commands_CommandErrored(CommandErrorEventArgs e)
        {
            var exception = e.Exception;
            if (e.Exception is TargetInvocationException tie)
            {
                exception = tie.InnerException;
            }

            _commandsLogger.LogError(exception, "Command '{0}' failed to execute!", e.Command?.Name);

            if (e.Context?.Channel != null)
            {
                await e.Context.Channel.SendMessageAsync(
                    $"Something fucked up when running that command, and an {exception.GetType().Name} occured.\n" +
                    $"This is probably my fault.");
            }
        }

        private void DebugLogger_LogMessageReceived(object sender, DebugLogMessageEventArgs e)
        {
            var level = e.Level switch
            {
                LogLevel.Critical => MSLogLevel.Critical,
                LogLevel.Error => MSLogLevel.Error,
                LogLevel.Warning => MSLogLevel.Warning,
                LogLevel.Info => MSLogLevel.Information,
                LogLevel.Debug => MSLogLevel.Debug,
                _ => MSLogLevel.Trace
            };

            if (e.Exception != null)
            {
                _clientLogger.Log(level, e.Exception, e.Message);
            }
            else
            {
                _clientLogger.Log(level, e.Message);
            }
        }
    }
}
