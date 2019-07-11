using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.EventArgs;
using GaiaPins.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using DSharpPlus.Entities;
using WamWooWam.Core;
using System.IO;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;

namespace GaiaPins
{
    class PinsService : IHostedService
    {
        private ILogger<PinsService> _logger;
        private IServiceProvider _services;
        private DiscordClient _client;
        private DiscordWebhookClient _webhookClient;
        private CommandsNextExtension _commands;
        private static readonly HttpClient _httpClient = new HttpClient();

        public PinsService(
            ILogger<PinsService> logger,
            IServiceProvider services,
            DiscordClient client,
            DiscordWebhookClient webhookClient,
            CommandsNextExtension commands)
        {
            _logger = logger;
            _services = services;
            _client = client;
            _webhookClient = webhookClient;
            _commands = commands;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _client.ConnectAsync();

            _client.ChannelPinsUpdated += Client_ChannelPinsUpdated;

            await Task.Delay(-1, cancellationToken);
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

        public static async Task CopyPinAsync(DiscordWebhook hook, DiscordMessage message, GuildInfo info, PinsDbContext db)
        {
            var content = new StringBuilder();
            var attachments = message.Attachments.ToList();

            var messageLink = $"https://discordapp.com/channels/{message.Channel.GuildId}/{message.ChannelId}/{message.Id}";
            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{message.Author.Username}", messageLink, message.Author.GetAvatarUrl(ImageFormat.Png, 128))
                .WithDescription(message.Content.Truncate(1000))
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
