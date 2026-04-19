using IncidentApp.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<IncidentRoom> IncidentRooms => Set<IncidentRoom>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(255).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(u => u.GlobalRole).HasMaxLength(20).IsRequired();
        });

        // IncidentRoom
        modelBuilder.Entity<IncidentRoom>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Title).HasMaxLength(255).IsRequired();
            e.Property(r => r.Severity).HasMaxLength(20).IsRequired();
            e.Property(r => r.Status).HasMaxLength(30).IsRequired();
            e.HasOne(r => r.CreatedBy)
             .WithMany()
             .HasForeignKey(r => r.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // RoomMember — composite PK
        modelBuilder.Entity<RoomMember>(e =>
        {
            e.HasKey(rm => new { rm.RoomId, rm.UserId });
            e.Property(rm => rm.Role).HasMaxLength(20).IsRequired();
            e.HasOne(rm => rm.Room)
             .WithMany(r => r.Members)
             .HasForeignKey(rm => rm.RoomId);
            e.HasOne(rm => rm.User)
             .WithMany(u => u.RoomMemberships)
             .HasForeignKey(rm => rm.UserId);
        });

        // Message
        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.MessageType).HasMaxLength(20).IsRequired();
            e.HasOne(m => m.Room)
             .WithMany(r => r.Messages)
             .HasForeignKey(m => m.RoomId);
            e.HasOne(m => m.Sender)
             .WithMany(u => u.Messages)
             .HasForeignKey(m => m.SenderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // TaskItem
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("Tasks");
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).HasMaxLength(255).IsRequired();
            e.Property(t => t.Status).HasMaxLength(20).IsRequired();
            e.Property(t => t.Priority).HasMaxLength(20).IsRequired();
            e.HasOne(t => t.Room)
             .WithMany(r => r.Tasks)
             .HasForeignKey(t => t.RoomId);
            e.HasOne(t => t.Assignee)
             .WithMany(u => u.AssignedTasks)
             .HasForeignKey(t => t.AssigneeId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.CreatedBy)
             .WithMany()
             .HasForeignKey(t => t.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ActivityLog
        modelBuilder.Entity<ActivityLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).HasMaxLength(100).IsRequired();
            e.Property(a => a.TargetType).HasMaxLength(50);
            e.HasOne(a => a.Room)
             .WithMany(r => r.ActivityLogs)
             .HasForeignKey(a => a.RoomId);
            e.HasOne(a => a.Actor)
             .WithMany(u => u.ActivityLogs)
             .HasForeignKey(a => a.ActorId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // RefreshToken
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);
            e.HasIndex(rt => rt.Token).IsUnique();
            e.HasOne(rt => rt.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(rt => rt.UserId);
        });
    }
}
