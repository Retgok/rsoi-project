using Microsoft.EntityFrameworkCore;

namespace StatisticsService;

public sealed class StatisticsDb : DbContext
{
    public StatisticsDb(DbContextOptions<StatisticsDb> options) : base(options) { }

    public DbSet<EventLogEntry> Events => Set<EventLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventLogEntry>(entity =>
        {
            entity.ToTable("event_log");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ServiceName).HasColumnName("service_name");
            entity.Property(e => e.Action).HasColumnName("action");
            entity.Property(e => e.Username).HasColumnName("username");
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}

public sealed class EventLogEntry
{
    public int Id { get; set; }
    public string ServiceName { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Username { get; set; }
    public string? Details { get; set; }
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
}
