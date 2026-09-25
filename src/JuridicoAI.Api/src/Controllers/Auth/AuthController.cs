using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Services;
using JuridicoAI.Api.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace JuridicoAI.Api.Controllers.Auth;

[ApiController]
[Route("auth")]
public sealed class AuthController(AuthApplicationService service, IOptions<AuthSettings> options) : ControllerBase
{
    private const string RefreshCookie = "juridicoai_refresh";
    private readonly AuthSettings _settings = options.Value;

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var response = ComCookie(await service.RegisterAsync(request, ct));
        return StatusCode(StatusCodes.Status201Created, response);
    }

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(ComCookie(await service.LoginAsync(request, ct)));

    [AllowAnonymous, EnableRateLimiting("auth"), HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct) =>
        Ok(ComCookie(await service.RefreshAsync(Request.Cookies[RefreshCookie], ct)));

    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    { await service.LogoutAsync(Request.Cookies[RefreshCookie], ct); Response.Cookies.Delete(RefreshCookie, CookieOptions()); return NoContent(); }

    [Authorize, HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken ct) => Ok(await service.MeAsync(ct));

    private AuthResponse ComCookie(AuthResponse response)
    {
        Response.Cookies.Append(RefreshCookie, response.RefreshToken, CookieOptions(response.RefreshTokenExpiresAt));
        return response with { RefreshToken = string.Empty };
    }

    private CookieOptions CookieOptions(DateTime? expiresAt = null) => new()
    {
        HttpOnly = true,
        Secure = _settings.SecureRefreshCookie,
        SameSite = SameSiteMode.Strict,
        Expires = expiresAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)),
        Path = "/auth"
    };
}
