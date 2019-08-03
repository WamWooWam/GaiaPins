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
        public Task Configure(IHost host) // task in case i need to do asynchronous work here
        {
            return Task.CompletedTask;
        }
    }
}
