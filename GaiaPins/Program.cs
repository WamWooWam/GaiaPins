using System;
using System.IO;
using DSharpPlus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using GaiaPins.Data;
using LogLevel = DSharpPlus.LogLevel;
using MSLogLevel = Microsoft.Extensions.Logging.LogLevel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GaiaPins
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureHostConfiguration(config =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("hostsettings.json", optional: true)
                          .AddEnvironmentVariables(prefix: "PINS_")
                          .AddCommandLine(args);
                })
                .ConfigureAppConfiguration((host, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory())
                          .AddJsonFile("appsettings.json")
                          .AddJsonFile($"appsettings.{host.HostingEnvironment.EnvironmentName}.json", true)
                          .AddEnvironmentVariables("PINS_")
                          .AddCommandLine(args);
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureLogging((host, logging) =>
                {
                    logging.AddConfiguration(host.Configuration)
                           .AddConsole();
                })
                .ConfigureServices(ConfigureServices)
                .UseConsoleLifetime()
                .Build();

            var startup = host.Services.GetService<Startup>();
            await startup.Configure(host);
            await host.RunAsync();
        }

        public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            services.AddSingleton<Startup>();
            services.AddHostedService<PinsService>();
            services.AddDbContext<PinsDbContext>(builder =>
            {
                builder.UseSqlite(context.Configuration["Database:ConnectionString"])
                       .ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuting, MSLogLevel.Debug)));
            });

            var discordConfig = new DiscordConfiguration() { Token = context.Configuration["Discord:Token"], LogLevel = LogLevel.Debug };
            var discord = new DiscordClient(discordConfig);
            services.AddSingleton(discord);

            var webhookClient = new DiscordWebhookClient();
            services.AddSingleton(webhookClient);

            var prefixes = context.Configuration["Discord:Prefixes"].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commandsConfig = new CommandsNextConfiguration() { Services = services.BuildServiceProvider(), StringPrefixes = prefixes };
            var cnext = discord.UseCommandsNext(commandsConfig);
            services.AddSingleton(cnext);
        }
    }
}
