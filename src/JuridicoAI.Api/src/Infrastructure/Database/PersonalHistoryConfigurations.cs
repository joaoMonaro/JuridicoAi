using System.Text.RegularExpressions;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace JuridicoAI.Api.Infrastructure.Database;
internal static class PersonalConfiguration
{
    public static void Base<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.ToTable(table); b.HasKey("Id"); b.Property<Guid>("Id").ValueGeneratedNever();
        b.HasOne<User>().WithMany().HasForeignKey("UserId").OnDelete(DeleteBehavior.Restrict);
        foreach (var p in b.Metadata.GetProperties()) p.SetColumnName(Regex.Replace(p.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }
}
internal sealed class PersonalHistorySessionConfiguration : IEntityTypeConfiguration<PersonalHistorySession>
{
    public void Configure(EntityTypeBuilder<PersonalHistorySession> b)
    {
        PersonalConfiguration.Base(b, "personal_history_sessions");
        b.Property(s => s.RevocationTokenHash).HasMaxLength(64); b.HasIndex(s => s.RevocationTokenHash).IsUnique();
        b.Property(s => s.RefreshTokenHash).HasMaxLength(64); b.HasIndex(s => s.RefreshTokenHash).IsUnique(); b.HasIndex(s => s.UserId);
    }
}
internal sealed class AssistenteExclusaoConfiguration : IEntityTypeConfiguration<AssistenteExclusao>
{
    public void Configure(EntityTypeBuilder<AssistenteExclusao> b)
    {
        PersonalConfiguration.Base(b, "ai_deletion_requests");
        b.Property(s => s.Arquivos).HasColumnType("jsonb"); b.Property(s => s.Estado).HasMaxLength(40); b.Property(s => s.CodigoErro).HasMaxLength(80);
        b.HasIndex(s => s.ConversaId).IsUnique(); b.HasIndex(s => new { s.UserId, s.AutorizadaEm, s.Id }); b.HasIndex(s => new { s.Estado, s.DisponivelEm });
    }
}
