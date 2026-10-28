namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class PecaExportSettings
{
    public string LibreOfficePath { get; set; } = "soffice";
    public string TempPath { get; set; } = Path.Combine(Path.GetTempPath(), "juridicoai-export");
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxConcurrentConversions { get; set; } = 2;
}
