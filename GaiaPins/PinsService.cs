using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
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
    public class PinsService : IHostedService
    {
        private IServiceProvider _services;
        private ILogger<PinsService> _logger;
        private ILogger<CommandsNextExtension> _commandsLogger;
        private ILogger<DiscordClient> _clientLogger;
        private DiscordClient _client;
        private DiscordWebhookClient _webhookClient;
        private CommandsNextExtension _commands;

        public PinsService(
            IServiceProvider services,
            ILogger<PinsService> logger,
            ILogger<DiscordClient> clientLogger,
            ILogger<CommandsNextExtension> commandsLogger,
            DiscordClient client,
            DiscordWebhookClient webhookClient,
            CommandsNextExtension commands)
        {
            _logger = logger;
            _services = services;
            _client = client;
            _webhookClient = webhookClient;
            _commands = commands;
            _commandsLogger = commandsLogger;
            _clientLogger = clientLogger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.DebugLogger.LogMessageReceived += DebugLogger_LogMessageReceived;
            _client.ChannelPinsUpdated += Client_ChannelPinsUpdated;
            _commands.CommandExecuted += Commands_CommandExecuted;
            _commands.CommandErrored += Commands_CommandErrored;
            _commands.RegisterCommands<PinsCommands>();

            await _client.ConnectAsync();

            var db = _services.GetService<PinsDbContext>();
            await db.Database.MigrateAsync();

            _logger.LogInformation("Loading Guild webhooks");

            var info = db.Guilds
                .Include(p => p.PinnedMessages);
            await info.LoadAsync();

            var failedGuilds = new List<GuildInfo>();
            foreach (var guild in info)
            {
                try
                {
                    var server = await _client.GetGuildAsync((ulong)guild.Id);
                    var hook = await _webhookClient.AddWebhookAsync((ulong)guild.WebhookId, guild.WebhookToken);

                    _logger.LogInformation("Got Webhook! Guild: {0} Hook: {1}", server.Name, hook.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to get Webhook for Guild {0}!", guild.Id);
                    failedGuilds.Add(guild);
                }
            }

            foreach (var guild in failedGuilds)
            {
                guild.PinnedMessages.Clear();
                db.Guilds.Remove(guild);
            }

            await db.SaveChangesAsync();

            _logger.LogInformation("Loaded {0} Webhooks for {1} guilds with {2} errors!", _webhookClient.Webhooks.Count, await db.Guilds.CountAsync(), failedGuilds.Count);
        }

        private Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            _commandsLogger.LogInformation("Command '{0}' executed by @{1}#{2} successfully!", e.Command.Name, e.Context.User.Username, e.Context.User.Discriminator);
            return Task.CompletedTask;
        }

        private async Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            var exception = e.Exception;
            if (e.Exception is TargetInvocationException tie)
            {
                exception = tie.InnerException;
            }

            _commandsLogger.LogError(exception, "Command '{0}' failed to execute!", e.Command?.Name);

            if (e.Context?.Channel != null)
            {
                if (exception is ChecksFailedException)
                {
                    await e.Context.Channel.SendMessageAsync(
                        $"It looks like either you or the bot don't have permission to run this command! Sorry!");
                }
                else
                {
                    await e.Context.Channel.SendMessageAsync(
                        $"Something fucked up when running that command, and an {exception.GetType().Name} occured.\n" +
                        $"This is probably my fault.");
                }
            }
        }

        private async Task Client_ChannelPinsUpdated(ChannelPinsUpdateEventArgs e)
        {
            if (e.Channel.Guild == null)
                return;

            using (_services.CreateScope())
            {
                var db = _services.GetService<PinsDbContext>();
                var info = await db.Guilds
                    .Include(p => p.PinnedMessages)
                    .FirstOrDefaultAsync(b => b.Id == (long)e.Channel.Guild.Id);

                if (info == null)
                {
                    _logger.LogInformation("Ignoring pins update for {0} because the guild isn't in the database.", e.Channel);
                    return;
                }

                if (e.Channel.IsNSFW && !info.IncludeNSFW)
                {
                    _logger.LogInformation("Ignoring pins update for {0} because it's marked as NSFW.", e.Channel);
                    return;
                }

                var hook = _webhookClient.GetRegisteredWebhook((ulong)info.WebhookId);
                if (hook == null)
                {
                    _logger.LogInformation("Ignoring pins update for {0} because the webhook is unavailable.", e.Channel);
                    return;
                }

                var pins = await e.Channel.GetPinnedMessagesAsync();
                var newPins = pins.Reverse().Where(p => !info.PinnedMessages.Any(m => m.Id == (long)p.Id));
                foreach (var pin in newPins)
                {
                    await CopyPinAsync(hook, pin, info, db);
                }

                await db.SaveChangesAsync();
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

        public async Task CopyPinAsync(DiscordWebhook hook, DiscordMessage message, GuildInfo info, PinsDbContext db)
        {
            var content = new StringBuilder();
            var attachments = message.Attachments.ToList();

            var messageLink = $"https://discordapp.com/channels/{message.Channel.GuildId}/{message.ChannelId}/{message.Id}";
            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{message.Author.Username}", messageLink, message.Author.GetAvatarUrl(ImageFormat.Png, 128))
                .WithDescription(message.Content.Length <= 1000 ? message.Content : $"{message.Content.Substring(0, 997)}...")
                .WithTimestamp(message.Timestamp)
                .WithFooter($"In #{message.Channel.Name}");

            if ((message.Author as DiscordMember).Color.Value != default)
            {
                embed.WithColor((message.Author as DiscordMember).Color);
            }

            foreach (var attachment in attachments)
            {
                var ext = Path.GetExtension(attachment.FileName.ToLowerInvariant());
                if ((ext == ".jpg" || ext == ".png" || ext == ".gif" || ext == ".webp") && attachment.Width != 0 && embed.ImageUrl == null)
                {
                    embed.WithImageUrl(attachment.Url);
                }
                else
                {
                    content.AppendLine(attachment.Url);
                }
            }

            var currentMember = message.Channel.Guild.CurrentMember;
            await hook.ExecuteAsync(content.ToString(), currentMember.Username, currentMember.AvatarUrl, false, new[] { embed.Build() }, null);

            var dbPin = new PinnedMessage() { GuildId = info.Id, Id = (long)message.Id };
            info.PinnedMessages.Add(dbPin);

            await db.AddAsync(dbPin);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.DisconnectAsync();
        }
    }
}
