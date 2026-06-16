using JuridicoAI.Api.Application.Peticoes.DTOs;
using JuridicoAI.Application.Services;

namespace JuridicoAI.Api.Application.Peticoes.Services;

public class PeticaoService
{
    private IJurisprudenciaProvider _jurisprudenciaProvider;
    
    public PeticaoService()
    {
        
    }

    public async Task<GerarPeticaoRequest> GerarAsync(GerarPeticaoRequest request,  CancellationToken cancellationToken)
    {
        var testeDB = _jurisprudenciaProvider.PesquisarAsync()
        return new GerarPeticaoRequest();
    }
}