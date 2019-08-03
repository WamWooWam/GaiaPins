using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using GaiaPins.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GaiaPins.Commands
{
    [RequireGuild]
    [ModuleLifespan(ModuleLifespan.Transient)]
    public class PinsCommands : BaseCommandModule
    {
        private ILogger<PinsCommands> _logger;
        private PinsDbContext _database;
        private DiscordWebhookClient _webhookClient;

        public PinsCommands(ILogger<PinsCommands> logger,
                            PinsDbContext database,
                            DiscordWebhookClient webhookClient)
        {
            _logger = logger;
            _database = database;
            _webhookClient = webhookClient;
        }

        [Command("enable")]
        [Description("Sets up pin redirection for this server.")]
        [RequireUserPermissions(Permissions.ManageWebhooks | Permissions.ManageMessages)]
        public async Task SetupAsync(CommandContext ctx,
                                     [Description("Channel to direct pinned messages to")] DiscordChannel channel,
                                     [Description("The URL of the webhook to use, including a token (optional)")] Uri webhookUrl = null)
        {
            if (!ctx.Guild.Channels.ContainsKey(channel.Id))
            {
                await ctx.RespondAsync("Are you trying to migrate pins across servers?? That ain't gonna fly, kid.");
                return;
            }

            var info = await _database.FindAsync<GuildInfo>((long)ctx.Guild.Id);
            if (info != null)
            {
                // TODO: prompt to reconfigure/setup
                _logger.LogError("Unable to setup pins in {0} because it is already setup.", ctx.Guild);
                await ctx.RespondAsync("Pinned message redireciton is already enabled in this server!");
                return;
            }

            var perms = channel.PermissionsFor(ctx.Guild.CurrentMember);
            if (!perms.HasPermission(Permissions.ManageWebhooks) && webhookUrl == null)
            {
                _logger.LogError("Unable to setup pins in {0} due to lack of permissions.", ctx.Guild);
                await ctx.RespondAsync("I can't setup pins without a Webhook URL or permission to manage webhooks! Sorry!");
                return;
            }

            DiscordWebhook hook;
            if (webhookUrl != null)
            {
                try
                {
                    hook = await _webhookClient.AddWebhookAsync(webhookUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch webhook from URL.");
                    await ctx.RespondAsync("The Webhook URL you specified was invalid!");
                    return;
                }
            }
            else
            {
                hook = await channel.CreateWebhookAsync("GAiA Pins", reason: $"Pinned Message redirection setup by @{ctx.User.Username}#{ctx.User.Discriminator} ({ctx.User.Id})");
                _webhookClient.AddWebhook(hook);
            }

            var guild = new GuildInfo() { Id = (long)ctx.Guild.Id, WebhookId = (long)hook.Id, WebhookToken = hook.Token, PinsChannelId = (long)channel.Id };
            _database.Add(guild);

            await _database.SaveChangesAsync();
            await ctx.RespondAsync(
                "Pinned messages are now setup for this server!\n" +
                "To migrate pins, use `p;migrate`, and to configure further, use `p;configure`.\n" +
                "Disable at any time with `p;disable`.");
        }

        [Command("migrate")]
        [Description("Migrates all pinned messages in the server. This may take a while.")]
        [RequireUserPermissions(Permissions.ManageWebhooks | Permissions.ManageMessages)]
        public async Task MigrateAsync(CommandContext ctx)
        {
            var info = await _database.Guilds
                .Include(p => p.PinnedMessages)
                .FirstOrDefaultAsync(b => b.Id == (long)ctx.Guild.Id);

            if (info == null)
            {
                // TODO: prompt to reconfigure/setup
                _logger.LogError("Unable to migrate pins in {0} because it isn't setup.", ctx.Guild);
                await ctx.RespondAsync("Pinned message redireciton isn't enabled in this server!");
                return;
            }

            var hook = _webhookClient.GetRegisteredWebhook((ulong)info.WebhookId);
            var channels = ctx.Guild.Channels.Values
                .Where(c => c.Type == ChannelType.Text && (!c.IsNSFW || info.IncludeNSFW))
                .Where(c => c.PermissionsFor(ctx.Guild.CurrentMember).HasPermission(Permissions.AccessChannels | Permissions.ReadMessageHistory))
                .ToList();

            var messages = new List<DiscordMessage>();
            var message = await ctx.RespondAsync($"Migrating messages for {channels.Count()} channels, this may take a while!");

            foreach (var channel in channels)
            {
                messages.AddRange(await channel.GetPinnedMessagesAsync());
            }

            messages = messages.Where(m => !info.PinnedMessages.Any(i => i.Id == (long)m.Id)).ToList();
            await message.ModifyAsync($"Migrating {messages.Count} messages from {channels.Count()} channels, this may take a while!");

            foreach (var msg in messages.OrderBy(m => m.Timestamp))
            {
                await PinsService.CopyPinAsync(hook, msg, info, _database);
            }

            await ctx.RespondAsync("Pins copied!");
            await _database.SaveChangesAsync();
        }

        [Command("disable")]
        [Description("Disables pinned message migration in this server.")]
        [RequireUserPermissions(Permissions.ManageWebhooks | Permissions.ManageMessages)]
        public async Task DisableAsync(CommandContext ctx)
        {
            var info = await _database.FindAsync<GuildInfo>((long)ctx.Guild.Id);
            if (info == null)
            {
                // TODO: prompt to reconfigure/setup
                _logger.LogError("Unable to disable pins in {0} because it isn't setup.", ctx.Guild);
                await ctx.RespondAsync("Pinned message redireciton isn't enabled in this server!");
                return;
            }

            info.PinnedMessages.Clear();
            _database.Guilds.Remove(info);

            await _database.SaveChangesAsync();
            await ctx.RespondAsync("Pinned message redirection has been disabled in this server!");
        }
    }
}
