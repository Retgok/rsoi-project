using Microsoft.EntityFrameworkCore;

namespace TicketService;

public class TicketDb : DbContext
{
    public TicketDb(DbContextOptions<TicketDb> options) : base(options) { }
    public DbSet<Ticket> Tickets => Set<Ticket>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("ticket");
            entity.HasKey(e => e.Id);


            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.TicketUid).HasColumnName("ticket_uid").IsRequired();
            entity.HasIndex(e => e.TicketUid).IsUnique();


            entity.Property(e => e.Username).HasColumnName("username").HasMaxLength(80).IsRequired();
            entity.Property(e => e.FlightNumber).HasColumnName("flight_number").HasMaxLength(20).IsRequired();
            entity.Property(e => e.Price).HasColumnName("price").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();

            entity.HasCheckConstraint("CK_Ticket_Status", "status IN ('PAID', 'CANCELED')");
        });


        base.OnModelCreating(modelBuilder);
    }
}