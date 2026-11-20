using Microsoft.Extensions.Options;

namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class AuthSettingsValidator(IHostEnvironment environment) : IValidateOptions<AuthSettings>
{
    public ValidateOptionsResult Validate(string? name, AuthSettings settings)
    {
        var errors = new List<string>();
        if (settings.JwtSecret is null || settings.JwtSecret.Length < 32)
            errors.Add("Auth:JwtSecret deve possuir ao menos 32 caracteres.");
        if (settings.AccessTokenTtlMinutes <= 0 || settings.RefreshTokenTtlDays <= 0)
            errors.Add("TTLs de autenticacao invalidos.");
        if (environment.IsProduction() && !settings.SecureRefreshCookie)
            errors.Add("Auth:SecureRefreshCookie deve ser true em produção.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
