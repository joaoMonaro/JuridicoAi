using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace JuridicoAI.Api.Infrastructure.Database;
internal sealed class AssistenteGeracaoConfiguration : IEntityTypeConfiguration<AssistenteGeracao>
{
    public void Configure(EntityTypeBuilder<AssistenteGeracao> b)
    {
        WorkspaceConfiguration.Base(b, "ai_generations");
        b.Property(x => x.FontesIds).HasColumnType("jsonb"); b.Property(x => x.Contexto).HasColumnType("jsonb"); b.Property(x => x.Plano).HasColumnType("jsonb");
        b.Property(x => x.Estado).HasMaxLength(30); b.Property(x => x.Modelo).HasMaxLength(100); b.Property(x => x.PoliticaVersao).HasMaxLength(100); b.Property(x => x.CodigoErro).HasMaxLength(100);
        b.HasIndex(x => x.RespostaId).IsUnique(); b.HasIndex(x => new { x.Estado, x.LeaseAte });
        b.HasOne<AssistenteConversa>().WithMany().HasForeignKey(x => new { x.ConversaId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteMensagemPersistida>().WithMany().HasForeignKey(x => new { x.PedidoId, x.ConversaId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AssistenteMensagemPersistida>().WithMany().HasForeignKey(x => new { x.RespostaId, x.ConversaId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.ConversaId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class AssistenteGeracaoEtapaConfiguration : IEntityTypeConfiguration<AssistenteGeracaoEtapa>
{
    public void Configure(EntityTypeBuilder<AssistenteGeracaoEtapa> b)
    {
        WorkspaceConfiguration.Base(b, "ai_generation_steps"); b.Property(x => x.Resultado).HasColumnType("jsonb");
        b.Property(x => x.Tipo).HasMaxLength(60); b.Property(x => x.Estado).HasMaxLength(30);
        b.HasIndex(x => new { x.GeracaoId, x.SolicitacaoId, x.Chamada }).IsUnique();
        b.HasOne<AssistenteGeracao>().WithMany().HasForeignKey(x => new { x.GeracaoId, x.OrganizationId }).HasPrincipalKey(x => new { x.Id, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict);
    }
}
