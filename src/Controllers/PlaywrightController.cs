using JuridicoAI.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;

namespace JuridicoAI.Controllers;

[ApiController]
[Route("[controller]")]
public class PlaywrightController : ControllerBase
{
    private readonly PlaywrightService _playwright;

    public PlaywrightController(PlaywrightService playwright)
    {
        _playwright = playwright;
    }

    [HttpPost]
    public async Task<string> PlaywrightCheck([FromBody] string descricao, CancellationToken cancellationToken)
    {
        var result = await _playwright.PlaywrightCheck(cancellationToken);
        return result;
    }
}
