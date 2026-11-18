using JuridicoAI.Api.Application.DTOs;
using JuridicoAI.Api.Application.Exceptions;
using JuridicoAI.Api.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace JuridicoAI.Api.Controllers.Auth;
[ApiController, AllowAnonymous, Route("auth/pessoal"), EnableRateLimiting("auth")]
[RequestSizeLimit(16_384)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PersonalHistoryAuthController(PersonalHistoryAuthService service, IConfiguration config) : ControllerBase
{
    private const string Cookie = "juridicoai_history_refresh";
    [HttpPost("login")]
    public async Task<PersonalHistoryAuthResponse> Login(LoginRequest request, CancellationToken ct)
    { Origin(); return SetCookie(await service.LoginAsync(request, ct)); }
    [HttpPost("refresh")]
    public async Task<PersonalHistoryAuthResponse> Refresh(CancellationToken ct)
    { Origin(); return SetCookie(await service.RefreshAsync(Request.Cookies[Cookie], ct)); }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    { Origin(); await service.LogoutAsync(Request.Cookies[Cookie], ct); Response.Cookies.Delete(Cookie, CookieOptions()); return NoContent(); }
    private PersonalHistoryAuthResponse SetCookie(PersonalHistoryAuthResponse response)
    { Response.Cookies.Append(Cookie, response.RefreshToken, CookieOptions(response.RefreshTokenExpiresAt)); return response; }
    private static CookieOptions CookieOptions(DateTime? expires = null) => new() { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/auth/pessoal", Expires = expires.HasValue ? new DateTimeOffset(expires.Value) : null };
    private void Origin()
    {
        var origins = config.GetSection("Auth:PersonalHistoryAllowedOrigins").Get<string[]>() ?? [];
        var value = Request.Headers.Origin.ToString();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var origin) || origin.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment) ||
            !(origins.Contains(value, StringComparer.Ordinal) || origin.Host is "localhost" or "127.0.0.1"))
            throw new AssistenteIaException(403, "PERSONAL_HISTORY_ORIGIN_DENIED", "Origem não autorizada para a sessão pessoal.");
    }
}
