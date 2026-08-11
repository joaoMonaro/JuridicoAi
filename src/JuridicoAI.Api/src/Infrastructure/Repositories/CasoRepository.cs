using System.Text.Json;
using JuridicoAI.Api.Application.Abstractions;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Database;
using Npgsql;
using NpgsqlTypes;

namespace JuridicoAI.Api.Infrastructure.Repositories;

public sealed class CasoRepository(PostgresConnectionFactory connectionFactory) : ICasoRepository
{
    private const string CasoColumns = "id, titulo, descricao, status, data_criacao, data_atualizacao";
    private const string EnvolvidoColumns = "id, caso_id, nome, tipo, documento, dados_adicionais, data_criacao, data_atualizacao";
    private const string ContextoColumns = "id, caso_id, tipo, titulo, conteudo, data_criacao, data_atualizacao";
    private const string DocumentoColumns = "id, caso_id, nome_original, nome_armazenado, tipo, mime_type, tamanho_bytes, storage_id, data_upload";

    public async Task<Caso> CriarCasoAsync(Caso caso, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO casos (id, titulo, descricao, status, data_criacao, data_atualizacao)
            VALUES (@id, @titulo, @descricao, @status, @data_criacao, @data_atualizacao)
            """;
        command.Parameters.AddWithValue("id", caso.Id);
        command.Parameters.AddWithValue("titulo", caso.Titulo);
        AddNullable(command, "descricao", caso.Descricao);
        command.Parameters.AddWithValue("status", caso.Status.ToString());
        command.Parameters.AddWithValue("data_criacao", caso.DataCriacao);
        command.Parameters.AddWithValue("data_atualizacao", caso.DataAtualizacao);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return caso;
    }

    public async Task<IReadOnlyList<Caso>> ListarCasosAsync(
        CasoStatus? status, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = status.HasValue
            ? $"SELECT {CasoColumns} FROM casos WHERE status = @status ORDER BY data_atualizacao DESC"
            : $"SELECT {CasoColumns} FROM casos ORDER BY data_atualizacao DESC";
        if (status.HasValue) command.Parameters.AddWithValue("status", status.Value.ToString());
        return await ReadManyAsync(command, MapCaso, cancellationToken);
    }

    public async Task<Caso?> ObterCasoAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {CasoColumns} FROM casos WHERE id = @id";
        command.Parameters.AddWithValue("id", id);
        return await ReadOneAsync(command, MapCaso, cancellationToken);
    }

    public async Task<bool> AtualizarCasoAsync(Caso caso, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE casos
               SET titulo = @titulo, descricao = @descricao, status = @status,
                   data_atualizacao = @data_atualizacao
             WHERE id = @id
            """;
        command.Parameters.AddWithValue("id", caso.Id);
        command.Parameters.AddWithValue("titulo", caso.Titulo);
        AddNullable(command, "descricao", caso.Descricao);
        command.Parameters.AddWithValue("status", caso.Status.ToString());
        command.Parameters.AddWithValue("data_atualizacao", caso.DataAtualizacao);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<bool> ArquivarCasoAsync(
        Guid id, DateTime dataAtualizacao, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE casos SET status = 'Arquivado', data_atualizacao = @data_atualizacao
             WHERE id = @id
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("data_atualizacao", dataAtualizacao);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<EnvolvidoCaso> CriarEnvolvidoAsync(
        EnvolvidoCaso envolvido, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO envolvidos_caso
                (id, caso_id, nome, tipo, documento, dados_adicionais, data_criacao, data_atualizacao)
            VALUES
                (@id, @caso_id, @nome, @tipo, @documento, @dados_adicionais, @data_criacao, @data_atualizacao)
            """;
        AddEnvolvidoParameters(command, envolvido);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return envolvido;
    }

    public async Task<IReadOnlyList<EnvolvidoCaso>> ListarEnvolvidosAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {EnvolvidoColumns} FROM envolvidos_caso WHERE caso_id = @caso_id ORDER BY data_criacao";
        command.Parameters.AddWithValue("caso_id", casoId);
        return await ReadManyAsync(command, MapEnvolvido, cancellationToken);
    }

    public async Task<EnvolvidoCaso?> ObterEnvolvidoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {EnvolvidoColumns} FROM envolvidos_caso WHERE caso_id = @caso_id AND id = @id";
        command.Parameters.AddWithValue("caso_id", casoId);
        command.Parameters.AddWithValue("id", id);
        return await ReadOneAsync(command, MapEnvolvido, cancellationToken);
    }

    public async Task<bool> AtualizarEnvolvidoAsync(
        EnvolvidoCaso envolvido, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE envolvidos_caso
               SET nome = @nome, tipo = @tipo, documento = @documento,
                   dados_adicionais = @dados_adicionais, data_atualizacao = @data_atualizacao
             WHERE caso_id = @caso_id AND id = @id
            """;
        AddEnvolvidoParameters(command, envolvido, includeCreationDate: false);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public Task<bool> RemoverEnvolvidoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken) =>
        DeleteChildAsync("envolvidos_caso", casoId, id, cancellationToken);

    public async Task<ContextoCaso> CriarContextoAsync(
        ContextoCaso contexto, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO contextos_caso
                (id, caso_id, tipo, titulo, conteudo, data_criacao, data_atualizacao)
            VALUES
                (@id, @caso_id, @tipo, @titulo, @conteudo, @data_criacao, @data_atualizacao)
            """;
        AddContextoParameters(command, contexto);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return contexto;
    }

    public async Task<IReadOnlyList<ContextoCaso>> ListarContextosAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ContextoColumns} FROM contextos_caso WHERE caso_id = @caso_id ORDER BY data_criacao";
        command.Parameters.AddWithValue("caso_id", casoId);
        return await ReadManyAsync(command, MapContexto, cancellationToken);
    }

    public async Task<ContextoCaso?> ObterContextoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ContextoColumns} FROM contextos_caso WHERE caso_id = @caso_id AND id = @id";
        command.Parameters.AddWithValue("caso_id", casoId);
        command.Parameters.AddWithValue("id", id);
        return await ReadOneAsync(command, MapContexto, cancellationToken);
    }

    public async Task<bool> AtualizarContextoAsync(
        ContextoCaso contexto, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE contextos_caso
               SET tipo = @tipo, titulo = @titulo, conteudo = @conteudo,
                   data_atualizacao = @data_atualizacao
             WHERE caso_id = @caso_id AND id = @id
            """;
        AddContextoParameters(command, contexto, includeCreationDate: false);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public Task<bool> RemoverContextoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken) =>
        DeleteChildAsync("contextos_caso", casoId, id, cancellationToken);

    public async Task<DocumentoCaso> CriarDocumentoAsync(
        DocumentoCaso documento, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO documentos_caso
                (id, caso_id, nome_original, nome_armazenado, tipo, mime_type,
                 tamanho_bytes, storage_id, data_upload)
            VALUES
                (@id, @caso_id, @nome_original, @nome_armazenado, @tipo, @mime_type,
                 @tamanho_bytes, @storage_id, @data_upload)
            """;
        command.Parameters.AddWithValue("id", documento.Id);
        command.Parameters.AddWithValue("caso_id", documento.CasoId);
        command.Parameters.AddWithValue("nome_original", documento.NomeOriginal);
        command.Parameters.AddWithValue("nome_armazenado", documento.NomeArmazenado);
        command.Parameters.AddWithValue("tipo", documento.Tipo);
        command.Parameters.AddWithValue("mime_type", documento.MimeType);
        command.Parameters.AddWithValue("tamanho_bytes", documento.TamanhoBytes);
        command.Parameters.AddWithValue("storage_id", documento.StorageId);
        command.Parameters.AddWithValue("data_upload", documento.DataUpload);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return documento;
    }

    public async Task<IReadOnlyList<DocumentoCaso>> ListarDocumentosAsync(
        Guid casoId, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {DocumentoColumns} FROM documentos_caso WHERE caso_id = @caso_id ORDER BY data_upload";
        command.Parameters.AddWithValue("caso_id", casoId);
        return await ReadManyAsync(command, MapDocumento, cancellationToken);
    }

    public async Task<DocumentoCaso?> ObterDocumentoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {DocumentoColumns} FROM documentos_caso WHERE caso_id = @caso_id AND id = @id";
        command.Parameters.AddWithValue("caso_id", casoId);
        command.Parameters.AddWithValue("id", id);
        return await ReadOneAsync(command, MapDocumento, cancellationToken);
    }

    public Task<bool> RemoverDocumentoAsync(
        Guid casoId, Guid id, CancellationToken cancellationToken) =>
        DeleteChildAsync("documentos_caso", casoId, id, cancellationToken);

    private async Task<bool> DeleteChildAsync(
        string table, Guid casoId, Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {table} WHERE caso_id = @caso_id AND id = @id";
        command.Parameters.AddWithValue("caso_id", casoId);
        command.Parameters.AddWithValue("id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static void AddEnvolvidoParameters(
        NpgsqlCommand command, EnvolvidoCaso envolvido, bool includeCreationDate = true)
    {
        command.Parameters.AddWithValue("id", envolvido.Id);
        command.Parameters.AddWithValue("caso_id", envolvido.CasoId);
        command.Parameters.AddWithValue("nome", envolvido.Nome);
        command.Parameters.AddWithValue("tipo", envolvido.Tipo.ToString());
        AddNullable(command, "documento", envolvido.Documento);
        var json = envolvido.DadosAdicionais is null
            ? null
            : JsonSerializer.Serialize(envolvido.DadosAdicionais);
        var jsonParameter = command.Parameters.Add("dados_adicionais", NpgsqlDbType.Jsonb);
        jsonParameter.Value = json is null ? DBNull.Value : json;
        if (includeCreationDate)
            command.Parameters.AddWithValue("data_criacao", envolvido.DataCriacao);
        command.Parameters.AddWithValue("data_atualizacao", envolvido.DataAtualizacao);
    }

    private static void AddContextoParameters(
        NpgsqlCommand command, ContextoCaso contexto, bool includeCreationDate = true)
    {
        command.Parameters.AddWithValue("id", contexto.Id);
        command.Parameters.AddWithValue("caso_id", contexto.CasoId);
        command.Parameters.AddWithValue("tipo", contexto.Tipo.ToString());
        AddNullable(command, "titulo", contexto.Titulo);
        command.Parameters.AddWithValue("conteudo", contexto.Conteudo);
        if (includeCreationDate)
            command.Parameters.AddWithValue("data_criacao", contexto.DataCriacao);
        command.Parameters.AddWithValue("data_atualizacao", contexto.DataAtualizacao);
    }

    private static void AddNullable(NpgsqlCommand command, string name, string? value)
    {
        var parameter = command.Parameters.Add(name, NpgsqlDbType.Text);
        parameter.Value = value is null ? DBNull.Value : value;
    }

    private static async Task<T?> ReadOneAsync<T>(
        NpgsqlCommand command, Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken)
        where T : class
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? map(reader) : null;
    }

    private static async Task<IReadOnlyList<T>> ReadManyAsync<T>(
        NpgsqlCommand command, Func<NpgsqlDataReader, T> map, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(map(reader));
        return items;
    }

    private static Caso MapCaso(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Titulo = reader.GetString(1),
        Descricao = reader.IsDBNull(2) ? null : reader.GetString(2),
        Status = Enum.Parse<CasoStatus>(reader.GetString(3)),
        DataCriacao = reader.GetDateTime(4),
        DataAtualizacao = reader.GetDateTime(5)
    };

    private static EnvolvidoCaso MapEnvolvido(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        CasoId = reader.GetGuid(1),
        Nome = reader.GetString(2),
        Tipo = Enum.Parse<TipoEnvolvidoCaso>(reader.GetString(3)),
        Documento = reader.IsDBNull(4) ? null : reader.GetString(4),
        DadosAdicionais = reader.IsDBNull(5)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(reader.GetString(5)),
        DataCriacao = reader.GetDateTime(6),
        DataAtualizacao = reader.GetDateTime(7)
    };

    private static ContextoCaso MapContexto(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        CasoId = reader.GetGuid(1),
        Tipo = Enum.Parse<TipoContextoCaso>(reader.GetString(2)),
        Titulo = reader.IsDBNull(3) ? null : reader.GetString(3),
        Conteudo = reader.GetString(4),
        DataCriacao = reader.GetDateTime(5),
        DataAtualizacao = reader.GetDateTime(6)
    };

    private static DocumentoCaso MapDocumento(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        CasoId = reader.GetGuid(1),
        NomeOriginal = reader.GetString(2),
        NomeArmazenado = reader.GetString(3),
        Tipo = reader.GetString(4),
        MimeType = reader.GetString(5),
        TamanhoBytes = reader.GetInt64(6),
        StorageId = reader.GetString(7),
        DataUpload = reader.GetDateTime(8)
    };
}
