using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;

namespace BonusService;

public class BonusDb : DbContext
{
    public BonusDb(DbContextOptions<BonusDb> options) : base(options) { }

    public DbSet<Privilege> Privileges => Set<Privilege>();
    public DbSet<PrivilegeHistory> PrivilegeHistories => Set<PrivilegeHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Privilege>(entity =>
        {
            entity.ToTable("privilege");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");

            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username)
                  .HasColumnName("username")
                  .HasMaxLength(80)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasColumnName("status")
                  .HasMaxLength(80)
                  .HasDefaultValue("BRONZE")
                  .IsRequired();

            entity.Property(e => e.Balance)
                  .HasColumnName("balance")
                  .IsRequired();

            entity.HasCheckConstraint("CK_Privilege_Status", "status IN ('BRONZE', 'SILVER', 'GOLD')");

            entity.HasMany(e => e.History)
                  .WithOne(h => h.Privilege)
                  .HasForeignKey(h => h.PrivilegeId)
                  .HasConstraintName("fk_privilege_history_privilege");
        });

        modelBuilder.Entity<PrivilegeHistory>(entity =>
        {
            entity.ToTable("privilege_history");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");

            entity.Property(e => e.PrivilegeId)
                  .HasColumnName("privilege_id")
                  .IsRequired();

            entity.Property(e => e.TicketUid)
                  .HasColumnName("ticket_uid")
                  .IsRequired();

            entity.Property(e => e.DateTime)
                  .HasColumnName("datetime")
                  .IsRequired();

            entity.Property(e => e.BalanceDiff)
                  .HasColumnName("balance_diff")
                  .IsRequired();

            entity.Property(e => e.OperationType)
                  .HasColumnName("operation_type")
                  .HasMaxLength(20)
                  .IsRequired();

            entity.HasCheckConstraint("CK_PrivilegeHistory_OperationType",
                "operation_type IN ('FILL_IN_BALANCE', 'DEBIT_THE_ACCOUNT')");
        });
    }
}

