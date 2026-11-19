using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace JuridicoAI.Api.Infrastructure.Database;
internal sealed class AssistenteRestoreStateConfiguration : IEntityTypeConfiguration<AssistenteRestoreState>
{
    public void Configure(EntityTypeBuilder<AssistenteRestoreState> b)
    {
        b.ToTable("ai_restore_state", t => t.HasCheckConstraint("ck_ai_restore_state_singleton", "id = 1"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.RunId).HasColumnName("run_id"); b.Property(x => x.InProgress).HasColumnName("in_progress");
        b.Property(x => x.JournalSha256).HasColumnName("journal_sha256").HasMaxLength(64);
        b.Property(x => x.StartedAt).HasColumnName("started_at"); b.Property(x => x.VerifiedAt).HasColumnName("verified_at");
    }
}
