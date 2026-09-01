using System.Text.Json;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JuridicoAI.Api.Infrastructure.Database;

internal sealed class CasoConfiguration : IEntityTypeConfiguration<Caso>
{
    public void Configure(EntityTypeBuilder<Caso> builder)
    {
        builder.ToTable("casos", table =>
        {
            table.HasCheckConstraint("ck_casos_titulo", "length(btrim(titulo)) > 0");
            table.HasCheckConstraint("ck_casos_status", "status IN ('Ativo', 'Arquivado')");
        });
        builder.HasKey(x => x.Id).HasName("casos_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id");
        builder.Property(x => x.CreatedById).HasColumnName("created_by_id");
        builder.Property(x => x.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(4000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao");
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_casos_status");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OrganizationId, x.Status }).HasDatabaseName("ix_casos_organization_status");
    }
}

internal sealed class EnvolvidoCasoConfiguration : IEntityTypeConfiguration<EnvolvidoCaso>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<EnvolvidoCaso> builder)
    {
        builder.ToTable("envolvidos_caso", table =>
        {
            table.HasCheckConstraint("ck_envolvidos_caso_nome", "length(btrim(nome)) > 0");
            table.HasCheckConstraint("ck_envolvidos_caso_tipo", "tipo IN ('Cliente', 'ParteContraria', 'Advogado', 'Testemunha', 'TerceiroInteressado', 'Outro')");
        });
        builder.HasKey(x => x.Id).HasName("envolvidos_caso_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CasoId).HasColumnName("caso_id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(40).HasConversion<string>();
        builder.Property(x => x.Documento).HasColumnName("documento").HasMaxLength(50);

        var converter = new ValueConverter<IReadOnlyDictionary<string, string?>?, string?>(
            value => value == null ? null : JsonSerializer.Serialize(value, JsonOptions),
            value => value == null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string?>>(value, JsonOptions));
        var comparer = new ValueComparer<IReadOnlyDictionary<string, string?>?>(
            (left, right) => Json(left) == Json(right),
            value => Json(value).GetHashCode(),
            value => value == null ? null : new Dictionary<string, string?>(value));
        builder.Property(x => x.DadosAdicionais)
            .HasColumnName("dados_adicionais")
            .HasColumnType("jsonb")
            .HasConversion(converter, comparer);

        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao");
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.HasOne<Caso>().WithMany().HasForeignKey(x => x.CasoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("envolvidos_caso_caso_id_fkey");
        builder.HasIndex(x => x.CasoId).HasDatabaseName("ix_envolvidos_caso_caso_id");
    }

    private static string Json(IReadOnlyDictionary<string, string?>? value) =>
        value == null ? string.Empty : JsonSerializer.Serialize(value, JsonOptions);
}

internal sealed class ContextoCasoConfiguration : IEntityTypeConfiguration<ContextoCaso>
{
    public void Configure(EntityTypeBuilder<ContextoCaso> builder)
    {
        builder.ToTable("contextos_caso", table =>
        {
            table.HasCheckConstraint("ck_contextos_caso_conteudo", "length(btrim(conteudo)) > 0");
            table.HasCheckConstraint("ck_contextos_caso_tipo", "tipo IN ('RelatoInicial', 'ResumoDosFatos', 'Cronologia', 'Observacao', 'Estrategia', 'Outro')");
        });
        builder.HasKey(x => x.Id).HasName("contextos_caso_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CasoId).HasColumnName("caso_id");
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(40).HasConversion<string>();
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200);
        builder.Property(x => x.Conteudo).HasColumnName("conteudo").HasMaxLength(50000).IsRequired();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao");
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.HasOne<Caso>().WithMany().HasForeignKey(x => x.CasoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("contextos_caso_caso_id_fkey");
        builder.HasIndex(x => x.CasoId).HasDatabaseName("ix_contextos_caso_caso_id");
    }
}

internal sealed class DocumentoCasoConfiguration : IEntityTypeConfiguration<DocumentoCaso>
{
    public void Configure(EntityTypeBuilder<DocumentoCaso> builder)
    {
        builder.ToTable("documentos_caso", table =>
            table.HasCheckConstraint("ck_documentos_caso_tamanho", "tamanho_bytes > 0"));
        builder.HasKey(x => x.Id).HasName("documentos_caso_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.CasoId).HasColumnName("caso_id");
        builder.Property(x => x.NomeOriginal).HasColumnName("nome_original").HasMaxLength(255).IsRequired();
        builder.Property(x => x.NomeArmazenado).HasColumnName("nome_armazenado").HasMaxLength(255).IsRequired();
        builder.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(100).IsRequired();
        builder.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(150).IsRequired();
        builder.Property(x => x.TamanhoBytes).HasColumnName("tamanho_bytes");
        builder.Property(x => x.StorageId).HasColumnName("storage_id").HasMaxLength(500).IsRequired();
        builder.Property(x => x.DataUpload).HasColumnName("data_upload");
        builder.HasOne<Caso>().WithMany().HasForeignKey(x => x.CasoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("documentos_caso_caso_id_fkey");
        builder.HasIndex(x => x.CasoId).HasDatabaseName("ix_documentos_caso_caso_id");
        builder.HasIndex(x => x.StorageId).IsUnique().HasDatabaseName("uq_documentos_caso_storage_id");
    }
}

internal sealed class PecaConfiguration : IEntityTypeConfiguration<Peca>
{
    public void Configure(EntityTypeBuilder<Peca> builder)
    {
        builder.ToTable("pecas", table =>
        {
            table.HasCheckConstraint("ck_pecas_titulo", "length(btrim(titulo)) > 0");
            table.HasCheckConstraint("ck_pecas_status", "status IN ('Rascunho', 'Finalizada')");
            table.HasCheckConstraint("ck_pecas_versao", "versao > 0");
        });
        builder.HasKey(x => x.Id).HasName("pecas_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id");
        builder.Property(x => x.CreatedById).HasColumnName("created_by_id");
        builder.Property(x => x.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(x => x.CasoId).HasColumnName("caso_id");
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ConteudoJson).HasColumnName("conteudo_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.TemplateId).HasColumnName("template_id");
        builder.Property(x => x.PageConfig).HasColumnName("page_config").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.HeaderJson).HasColumnName("header_json").HasColumnType("jsonb");
        builder.Property(x => x.FooterJson).HasColumnName("footer_json").HasColumnType("jsonb");
        builder.Property(x => x.Styles).HasColumnName("styles").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(x => x.Versao).HasColumnName("versao");
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao");
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.Property(x => x.DataExclusao).HasColumnName("data_exclusao");
        builder.HasOne<Caso>().WithMany().HasForeignKey(x => x.CasoId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pecas_caso_id_fkey");
        builder.HasOne<TemplatePeca>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("pecas_template_id_fkey");
        builder.HasIndex(x => new { x.OrganizationId, x.CasoId, x.DataExclusao, x.DataAtualizacao }).HasDatabaseName("ix_pecas_caso_listagem");
        builder.HasIndex(x => x.TemplateId).HasDatabaseName("ix_pecas_template_id");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TemplatePecaConfiguration : IEntityTypeConfiguration<TemplatePeca>
{
    public void Configure(EntityTypeBuilder<TemplatePeca> builder)
    {
        builder.ToTable("templates_peca", table =>
            table.HasCheckConstraint("ck_templates_peca_nome", "length(btrim(nome)) > 0"));
        builder.HasKey(x => x.Id).HasName("templates_peca_pkey");
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id");
        builder.Property(x => x.CreatedById).HasColumnName("created_by_id");
        builder.Property(x => x.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ConteudoJson).HasColumnName("conteudo_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.PageConfig).HasColumnName("page_config").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.HeaderJson).HasColumnName("header_json").HasColumnType("jsonb");
        builder.Property(x => x.FooterJson).HasColumnName("footer_json").HasColumnType("jsonb");
        builder.Property(x => x.Styles).HasColumnName("styles").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao");
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.Property(x => x.DataExclusao).HasColumnName("data_exclusao");
        builder.HasIndex(x => new { x.OrganizationId, x.DataExclusao, x.Nome }).HasDatabaseName("ix_templates_peca_listagem");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations", t => t.HasCheckConstraint("ck_organizations_nome", "length(btrim(nome)) > 0"));
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao"); builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao"); builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_users_email");
    }
}

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id"); builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Role).HasColumnName("role").HasMaxLength(20).HasConversion<string>();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao"); builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique().HasDatabaseName("uq_memberships_organization_user");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_memberships_user_id");
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever(); builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.OrganizationId).HasColumnName("organization_id");
        builder.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExpiraEm).HasColumnName("expira_em"); builder.Property(x => x.RevogadaEm).HasColumnName("revogada_em");
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao"); builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.Property(x => x.UltimoUsoEm).HasColumnName("ultimo_uso_em");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RefreshTokenHash).IsUnique().HasDatabaseName("uq_sessions_refresh_token_hash");
        builder.HasIndex(x => new { x.UserId, x.OrganizationId }).HasDatabaseName("ix_sessions_user_organization");
    }
}
