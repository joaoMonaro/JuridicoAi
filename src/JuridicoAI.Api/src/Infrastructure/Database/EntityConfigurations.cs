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
        builder.Property(x => x.Titulo).HasColumnName("titulo").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Descricao).HasColumnName("descricao").HasMaxLength(4000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(x => x.DataCriacao).HasColumnName("data_criacao");
        builder.Property(x => x.DataAtualizacao).HasColumnName("data_atualizacao");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_casos_status");
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
