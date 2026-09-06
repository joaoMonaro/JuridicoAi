using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace JuridicoAI.Api.Controllers.Pecas;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize]
[Route("templates-peca")]
public sealed class TemplatesPecaController(TemplatePecaApplicationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TemplatePeca>> Criar(CriarTemplatePecaRequest request, CancellationToken ct)
    { var item = await service.CriarAsync(request, ct); return CreatedAtAction(nameof(Obter), new { id = item.Id }, item); }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplatePeca>>> Listar(CancellationToken ct) => Ok(await service.ListarAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplatePeca>> Obter(Guid id, CancellationToken ct) => Ok(await service.ObterAsync(id, ct));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TemplatePeca>> Atualizar(Guid id, AtualizarTemplatePecaRequest request, CancellationToken ct) =>
        Ok(await service.AtualizarAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Excluir(Guid id, CancellationToken ct)
    { await service.ExcluirAsync(id, ct); return NoContent(); }
}
