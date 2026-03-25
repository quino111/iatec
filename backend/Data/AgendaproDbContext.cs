// ============================================================
// AgendaproDbContext.cs  —  agenda2
// Versión corregida: sin connection string hardcodeado.
// La conexión viene de appsettings.json via Program.cs.
// ============================================================
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using agenda2.Models;

namespace agenda2.Data;

public partial class AgendaproDbContext : DbContext
{
    public AgendaproDbContext()
    {
    }

    public AgendaproDbContext(DbContextOptions<AgendaproDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Efmigrationshistory> Efmigrationshistories { get; set; }
    public virtual DbSet<Event> Events { get; set; }
    public virtual DbSet<Eventparticipant> Eventparticipants { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<Userevent> Userevents { get; set; }

    // ── OnConfiguring eliminado ────────────────────────────
    // La conexión se configura en Program.cs mediante
    // builder.Services.AddDbContext<AgendaproDbContext>(...)
    // apuntando a appsettings.json → ConnectionStrings:DefaultConnection

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Efmigrationshistory>(entity =>
        {
            entity.HasKey(e => e.MigrationId).HasName("PRIMARY");
            entity
                .ToTable("__efmigrationshistory")
                .UseCollation("utf8mb4_general_ci");
            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ProductVersion).HasMaxLength(32);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("events");

            entity.HasIndex(e => e.Date, "IX_Events_Date");
            entity.HasIndex(e => e.IsDeleted, "IX_Events_Deleted");
            entity.HasIndex(e => e.OwnerId, "IX_Events_Owner");
            entity.HasIndex(e => e.Type, "IX_Events_Type");

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Date).HasColumnType("datetime");
            entity.Property(e => e.EndDate).HasColumnType("datetime");   // ← nuevo
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.OwnerId).HasColumnType("int(11)");
            entity.Property(e => e.UpdatedAt)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Owner)
                .WithMany(p => p.Events)
                .HasForeignKey(d => d.OwnerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Events_Owner");
        });

        modelBuilder.Entity<Eventparticipant>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("eventparticipants");
            entity.HasIndex(e => e.EventId, "IX_Participants_Event");
            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.EventId).HasColumnType("int(11)");
            entity.Property(e => e.ParticipantName).HasMaxLength(200);

            entity.HasOne(d => d.Event)
                .WithMany(p => p.Eventparticipants)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("FK_Participants_Event");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("users");
            entity.HasIndex(e => e.Email, "UQ_Users_Email").IsUnique();
            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.IsActive)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.Name).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(500);
        });

        modelBuilder.Entity<Userevent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");
            entity.ToTable("userevents");

            entity.HasIndex(e => e.EventId, "IX_UserEvents_Event");
            entity.HasIndex(e => e.UserId, "IX_UserEvents_User");
            entity.HasIndex(e => new { e.UserId, e.EventId }, "UQ_UserEvent").IsUnique();

            entity.Property(e => e.Id).HasColumnType("int(11)");
            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("current_timestamp()")
                .HasColumnType("datetime");
            entity.Property(e => e.EventId).HasColumnType("int(11)");
            entity.Property(e => e.UserId).HasColumnType("int(11)");

            entity.HasOne(d => d.Event)
                .WithMany(p => p.Userevents)
                .HasForeignKey(d => d.EventId)
                .HasConstraintName("FK_UserEvents_Event");

            entity.HasOne(d => d.User)
                .WithMany(p => p.Userevents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserEvents_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
