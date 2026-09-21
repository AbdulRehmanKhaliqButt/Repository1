using Microsoft.EntityFrameworkCore;

namespace IncidentAgent.Persistence;

public sealed class InvestigationDbContext(
    DbContextOptions<InvestigationDbContext> options) : DbContext(options)
{
    public DbSet<InvestigationEntity> Investigations => Set<InvestigationEntity>();
    public DbSet<EvidenceEntity> Evidence => Set<EvidenceEntity>();
    public DbSet<HypothesisEntity> Hypotheses => Set<HypothesisEntity>();
    public DbSet<RecommendedActionEntity> RecommendedActions => Set<RecommendedActionEntity>();
    public DbSet<SourceExecutionEntity> SourceExecutions => Set<SourceExecutionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var investigation = modelBuilder.Entity<InvestigationEntity>();
        investigation.ToTable("investigations");
        investigation.HasKey(item => item.Id);
        investigation.Property(item => item.Id).HasMaxLength(64);
        investigation.Property(item => item.Title).HasMaxLength(500);
        investigation.Property(item => item.ServiceName).HasMaxLength(300);
        investigation.Property(item => item.ReasoningMode).HasMaxLength(64);
        investigation.Property(item => item.ReasoningProvider).HasMaxLength(128);
        investigation.Property(item => item.ReasoningModel).HasMaxLength(256);
        investigation.Property(item => item.ReasoningEstimatedCostUsd)
            .HasPrecision(18, 8);
        investigation.HasIndex(item => item.GeneratedAtUtc);
        investigation.HasIndex(item => new { item.ServiceName, item.GeneratedAtUtc });

        var evidence = modelBuilder.Entity<EvidenceEntity>();
        evidence.ToTable("investigation_evidence");
        evidence.HasKey(item => item.Id);
        evidence.Property(item => item.EvidenceId).HasMaxLength(300);
        evidence.Property(item => item.Type).HasMaxLength(64);
        evidence.Property(item => item.Source).HasMaxLength(128);
        evidence.Property(item => item.Service).HasMaxLength(300);
        evidence.Property(item => item.AttributesJson).HasColumnType("jsonb");
        evidence.HasIndex(item => new { item.InvestigationId, item.EvidenceId });
        evidence.HasOne(item => item.Investigation)
            .WithMany(item => item.Evidence)
            .HasForeignKey(item => item.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);

        var hypothesis = modelBuilder.Entity<HypothesisEntity>();
        hypothesis.ToTable("investigation_hypotheses");
        hypothesis.HasKey(item => item.Id);
        hypothesis.Property(item => item.EvidenceIdsJson).HasColumnType("jsonb");
        hypothesis.HasIndex(item => new { item.InvestigationId, item.Rank });
        hypothesis.HasOne(item => item.Investigation)
            .WithMany(item => item.Hypotheses)
            .HasForeignKey(item => item.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);

        var action = modelBuilder.Entity<RecommendedActionEntity>();
        action.ToTable("investigation_actions");
        action.HasKey(item => item.Id);
        action.HasIndex(item => new { item.InvestigationId, item.SortOrder });
        action.HasOne(item => item.Investigation)
            .WithMany(item => item.Actions)
            .HasForeignKey(item => item.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);

        var execution = modelBuilder.Entity<SourceExecutionEntity>();
        execution.ToTable("source_executions");
        execution.HasKey(item => item.Id);
        execution.Property(item => item.Source).HasMaxLength(128);
        execution.Property(item => item.Status).HasMaxLength(32);
        execution.Property(item => item.ErrorType).HasMaxLength(300);
        execution.HasIndex(item => new { item.InvestigationId, item.Source });
        execution.HasOne(item => item.Investigation)
            .WithMany(item => item.SourceExecutions)
            .HasForeignKey(item => item.InvestigationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
