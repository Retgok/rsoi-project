using Microsoft.EntityFrameworkCore;

namespace FlightService;

public class FlightDb : DbContext
{
    public FlightDb(DbContextOptions<FlightDb> options) : base(options) { }

    public DbSet<Flight> Flights => Set<Flight>();
    public DbSet<Airport> Airports => Set<Airport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Airport>(entity =>
        {
            entity.ToTable("airport");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.Id).HasColumnName("id");
            entity.Property(a => a.Name).HasColumnName("name").HasMaxLength(255);
            entity.Property(a => a.City).HasColumnName("city").HasMaxLength(255);
            entity.Property(a => a.Country).HasColumnName("country").HasMaxLength(255);
        });

        modelBuilder.Entity<Flight>(entity =>
        {
            entity.ToTable("flight");
            entity.HasKey(f => f.Id);

            entity.Property(f => f.Id).HasColumnName("id");
            entity.Property(f => f.FlightNumber).HasColumnName("flight_number").HasMaxLength(20).IsRequired();
            entity.Property(f => f.DateTime).HasColumnName("datetime").IsRequired();
            entity.Property(f => f.FromAirportId).HasColumnName("from_airport_id");
            entity.Property(f => f.ToAirportId).HasColumnName("to_airport_id");
            entity.Property(f => f.Price).HasColumnName("price").IsRequired();
            entity.Property(f => f.Capacity).HasColumnName("capacity");

            entity.HasOne(f => f.FromAirport)
                .WithMany()
                .HasForeignKey(f => f.FromAirportId);

            entity.HasOne(f => f.ToAirport)
                .WithMany()
                .HasForeignKey(f => f.ToAirportId);
        });

        base.OnModelCreating(modelBuilder);
    }
}