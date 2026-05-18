using System;
using System.Collections.Generic;
using Neuracode.Crm.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Neuracode.Crm.Api.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Activity> Activities { get; set; }

    public virtual DbSet<Contact> Contacts { get; set; }

    public virtual DbSet<CrmSetting> CrmSettings { get; set; }

    public virtual DbSet<Deal> Deals { get; set; }

    public virtual DbSet<PipelineStage> PipelineStages { get; set; }

    public virtual DbSet<WhatsAppMessage> WhatsAppMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.ToTable("activities");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.ContactId).HasColumnName("contact_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DealId).HasColumnName("deal_id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Wamid).HasColumnName("wamid");
            entity.Property(e => e.ScheduledAt).HasColumnName("scheduled_at");
            entity.Property(e => e.Type).HasColumnName("type");

            entity.HasIndex(e => e.ContactId);
            entity.HasIndex(e => e.DealId);

            entity.HasOne(d => d.Contact).WithMany(p => p.Activities)
                .HasForeignKey(d => d.ContactId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Deal).WithMany(p => p.Activities).HasForeignKey(d => d.DealId);
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("contacts");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Company).HasColumnName("company");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Phone).HasColumnName("phone");
            entity.Property(e => e.Score).HasColumnName("score");
            entity.Property(e => e.Source)
                .HasDefaultValue("otro")
                .HasColumnName("source");
            entity.Property(e => e.Temperature)
                .HasDefaultValue("cold")
                .HasColumnName("temperature");
            entity.Property(e => e.WaId).HasColumnName("wa_id");
            entity.Property(e => e.OptedOut).HasColumnName("opted_out").HasDefaultValue(false);
            entity.Property(e => e.BotHandling).HasColumnName("bot_handling").HasDefaultValue(false);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(e => e.WaId).IsUnique().HasFilter("wa_id IS NOT NULL");
        });

        modelBuilder.Entity<CrmSetting>(entity =>
        {
            entity.HasKey(e => e.Key);

            entity.ToTable("crm_settings");

            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<Deal>(entity =>
        {
            entity.ToTable("deals");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ContactId).HasColumnName("contact_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpectedClose).HasColumnName("expected_close");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.Probability).HasColumnName("probability");
            entity.Property(e => e.StageId).HasColumnName("stage_id");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.Value).HasColumnName("value");

            entity.HasIndex(e => e.ContactId);
            entity.HasIndex(e => e.StageId);

            entity.HasOne(d => d.Contact).WithMany(p => p.Deals)
                .HasForeignKey(d => d.ContactId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Stage).WithMany(p => p.Deals)
                .HasForeignKey(d => d.StageId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<PipelineStage>(entity =>
        {
            entity.ToTable("pipeline_stages");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Color)
                .HasDefaultValue("#64748b")
                .HasColumnName("color");
            entity.Property(e => e.IsLost).HasColumnName("is_lost");
            entity.Property(e => e.IsWon).HasColumnName("is_won");
            entity.Property(e => e.Name).HasColumnName("name");
            entity.Property(e => e.Order).HasColumnName("order");
        });

        modelBuilder.Entity<WhatsAppMessage>(entity =>
        {
            entity.ToTable("whatsapp_messages");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.WaId).HasColumnName("wa_id");
            entity.Property(e => e.Wamid).HasColumnName("wamid");
            entity.Property(e => e.Direction).HasColumnName("direction");
            entity.Property(e => e.Body).HasColumnName("body");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(e => e.Wamid).IsUnique();
            entity.HasIndex(e => e.WaId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
