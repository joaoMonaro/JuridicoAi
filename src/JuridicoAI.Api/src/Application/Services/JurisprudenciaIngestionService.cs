using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Domain.Entities;
using JuridicoAI.Api.Infrastructure.Repositories;

namespace JuridicoAI.Api.Application.Services;

public sealed class JurisprudenciaIngestionService(
    ScraperService scraper,
    JurisprudenciaNormalizerService normalizer,
    JurisprudenciaClassifierService classifier,
    JurisprudenciaRawRepository rawRepository,
    JurisprudenciaRepository repository,
    ILogger<JurisprudenciaIngestionService> logger)
{
    public async Task<IngestionSummary> IngerirAsync(string termo, int? maxResultados, CancellationToken cancellationToken)
    {
        await GarantirIndicesAsync(cancellationToken);
        var coleta = await scraper.ColetarAsync(termo, maxResultados, cancellationToken);
        var novos = 0;
        var duplicados = 0;
        var classificados = 0;
        var falhas = 0;

        foreach (var item in coleta.Resultados)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JurisprudenciaRaw? raw = null;
            try
            {
                var salvo = await rawRepository.SalvarSeNovoAsync(item, cancellationToken);
                raw = salvo.Raw;
                if (!salvo.Criado) { duplicados++; continue; }
                novos++;
                await ProcessarRawAsync(raw, cancellationToken);
                classificados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                logger.LogError(ex, "Falha individual ao ingerir processo {Processo} do TJSP", item.NumeroProcesso);
                if (!string.IsNullOrWhiteSpace(raw?.Id))
                    await rawRepository.AtualizarStatusAsync(raw.Id, "erro_processamento", ex.Message, cancellationToken);
            }
        }

        return new IngestionSummary(coleta.TermoBuscado, coleta.Resultados.Count, novos, duplicados, classificados, falhas);
    }

    public async Task<ReprocessSummary> ReprocessarAsync(string? termo, int limite, CancellationToken cancellationToken)
    {
        await GarantirIndicesAsync(cancellationToken);
        var raws = await rawRepository.ListarParaReprocessarAsync(termo, limite, cancellationToken);
        var processados = 0;
        var falhas = 0;
        foreach (var raw in raws)
        {
            try { await ProcessarRawAsync(raw, cancellationToken); processados++; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                falhas++;
                logger.LogError(ex, "Falha ao reprocessar raw {RawId}", raw.Id);
            }
        }
        return new ReprocessSummary(raws.Count, processados, falhas);
    }

    private async Task ProcessarRawAsync(JurisprudenciaRaw raw, CancellationToken cancellationToken)
    {
        var normalizada = normalizer.Normalizar(raw);
        normalizada.Classificacao = classifier.Classificar(normalizada);
        normalizada.DataProcessamento = DateTime.UtcNow;
        await repository.SalvarAsync(normalizada, cancellationToken);
        await rawRepository.AtualizarStatusAsync(raw.Id!, "processado", null, cancellationToken);
    }

    private async Task GarantirIndicesAsync(CancellationToken cancellationToken)
    {
        await rawRepository.EnsureIndexesAsync(cancellationToken);
        await repository.EnsureIndexesAsync(cancellationToken);
    }
}
