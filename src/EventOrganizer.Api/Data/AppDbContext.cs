using EventOrganizer.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventOrganizer.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Participation> Participations => Set<Participation>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseShare> ExpenseShares => Set<ExpenseShare>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.Name).IsRequired();
        });

        // Event
        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Date).IsRequired();
            entity.Property(e => e.Place).IsRequired();
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.InviteCode).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().IsRequired();
        });

        // Participation
        modelBuilder.Entity<Participation>(entity =>
        {
            entity.ToTable("Participations");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Role).HasConversion<string>().IsRequired();
            entity.Property(p => p.JoinedAt).IsRequired();

            entity.HasOne<Event>()
                .WithMany()
                .HasForeignKey(p => p.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => new { p.EventId, p.UserId }).IsUnique();
        });

        // Expense
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.ToTable("Expenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasColumnType("TEXT").IsRequired();
            entity.Property(x => x.Description).IsRequired();
            entity.Property(x => x.CreatedAt).IsRequired();

            entity.HasOne<Event>()
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Participation>()
                .WithMany()
                .HasForeignKey(x => x.PayerParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ExpenseShare
        modelBuilder.Entity<ExpenseShare>(entity =>
        {
            entity.ToTable("ExpenseShares");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Amount).HasColumnType("TEXT").IsRequired();

            entity.HasOne<Expense>()
                .WithMany()
                .HasForeignKey(s => s.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<Participation>()
                .WithMany()
                .HasForeignKey(s => s.ParticipationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
