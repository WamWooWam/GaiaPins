using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace GaiaPins.Data
{
    public class PinsDbContext : DbContext
    {
        public PinsDbContext() : base()
        {

        }

        public PinsDbContext(DbContextOptions options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=pins.dev.db;");
        }

        public DbSet<GuildInfo> Guilds { get; set; }
    }

    public class GuildInfo
    {
        public GuildInfo()
        {
            PinnedMessages = new List<PinnedMessage>();
            IncludeNSFW = false;
        }

        public long Id { get; set; }
        public long PinsChannelId { get; set; }
        public long WebhookId { get; set; }
        public string WebhookToken { get; set; }

        public bool IncludeNSFW { get; set; }

        public List<PinnedMessage> PinnedMessages { get; set; }
    }

    public class PinnedMessage
    {
        public long Id { get; set; }
        public long GuildId { get; set; }
        public long NewMessageId { get;set; }
    }
}
