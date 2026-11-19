namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class DeletionProtectionSettings
{
    public string JournalDirectory { get; set; } = "";
    public string IntegrityKeyBase64 { get; set; } = "";
    public Guid ScopeId { get; set; }
    public bool MaintenanceMode { get; set; }
    public bool Configured => ScopeId != Guid.Empty && Path.IsPathFullyQualified(JournalDirectory) && ValidKey(IntegrityKeyBase64);
    public static bool ValidKey(string key)
    {
        try { return Convert.FromBase64String(key).Length >= 32; } catch (FormatException) { return false; }
    }
}
