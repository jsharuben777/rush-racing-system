using Microsoft.EntityFrameworkCore;
using RushRacing.Core.Entities;

namespace RushRacing.Data;

public class RushRacingDbContext : DbContext
{
    public RushRacingDbContext(DbContextOptions<RushRacingDbContext> options)
        : base(options) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<MachineHeartbeat> MachineHeartbeats => Set<MachineHeartbeat>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // LOCATION
        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.TimeZone).HasMaxLength(50).IsRequired();
        });

        // MACHINE
        modelBuilder.Entity<Machine>(entity =>
        {
            entity.HasKey(e => e.MachineId);
            entity.Property(e => e.MachineCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ApiKeyHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasIndex(e => e.MachineCode).IsUnique();
            entity.HasIndex(e => e.Slug).IsUnique();

            entity.HasOne(e => e.Location)
                  .WithMany(l => l.Machines)
                  .HasForeignKey(e => e.LocationId);
        });

        // GAME
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.GameId);
            entity.Property(e => e.GameId).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ImageFileName).HasMaxLength(200);
        });

        // PRICING PLAN
        modelBuilder.Entity<PricingPlan>(entity =>
        {
            entity.HasKey(e => e.PricingPlanId);
            entity.Property(e => e.PriceAmount).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);

            entity.HasOne(e => e.Location)
                  .WithMany(l => l.PricingPlans)
                  .HasForeignKey(e => e.LocationId)
                  .IsRequired(false);
        });

        // SESSION
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.Property(e => e.SessionCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.PriceAmount).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status)
                  .HasConversion<string>()
                  .HasMaxLength(30);
            entity.Property(e => e.CustomerPhone).HasMaxLength(20);
            entity.Property(e => e.CustomerEmail).HasMaxLength(200);
            entity.Property(e => e.SelectedGameId).HasMaxLength(50);

            entity.HasIndex(e => e.SessionCode).IsUnique();

            entity.HasOne(e => e.Machine)
                  .WithMany(m => m.Sessions)
                  .HasForeignKey(e => e.MachineId);

            entity.HasOne(e => e.PricingPlan)
                  .WithMany()
                  .HasForeignKey(e => e.PricingPlanId);

            entity.HasOne(e => e.SelectedGame)
                  .WithMany()
                  .HasForeignKey(e => e.SelectedGameId)
                  .IsRequired(false);
        });

        // PAYMENT
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId);
            entity.Property(e => e.ExternalPaymentId).HasMaxLength(200);
            entity.Property(e => e.PaymentProvider).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasOne(e => e.Session)
                  .WithMany(s => s.Payments)
                  .HasForeignKey(e => e.SessionId);
        });

        // REFUND
        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasKey(e => e.RefundId);
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.Reason).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.InitiatedBy).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExternalRefundId).HasMaxLength(200);

            entity.HasOne(e => e.Payment)
                  .WithMany(p => p.Refunds)
                  .HasForeignKey(e => e.PaymentId);

            entity.HasOne(e => e.Session)
                  .WithMany()
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // MACHINE HEARTBEAT
        modelBuilder.Entity<MachineHeartbeat>(entity =>
        {
            entity.HasKey(e => e.HeartbeatId);
            entity.Property(e => e.InternetStatus).HasMaxLength(10);
            entity.Property(e => e.AgentVersion).HasMaxLength(20);
            entity.Property(e => e.ActiveSessionCode).HasMaxLength(20);
            entity.Property(e => e.ActiveGameId).HasMaxLength(50);

            entity.HasOne(e => e.Machine)
                  .WithMany(m => m.Heartbeats)
                  .HasForeignKey(e => e.MachineId);
        });

        // SESSION EVENT
        modelBuilder.Entity<SessionEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.Property(e => e.Source).HasMaxLength(20).IsRequired();

            entity.HasOne(e => e.Session)
                  .WithMany(s => s.Events)
                  .HasForeignKey(e => e.SessionId)
                  .IsRequired(false);

            entity.HasOne(e => e.Machine)
                  .WithMany()
                  .HasForeignKey(e => e.MachineId)
                  .IsRequired(false);
        });

        // ADMIN USER
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.AdminUserId);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Role)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.HasIndex(e => e.Username).IsUnique();
        });

        // AUDIT LOG
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.Action).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(50);
            entity.Property(e => e.TargetId).HasMaxLength(50);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.Property(e => e.IpAddress).HasMaxLength(45);

            entity.HasOne(e => e.AdminUser)
                  .WithMany()
                  .HasForeignKey(e => e.AdminUserId)
                  .IsRequired(false);
        });
    }
}