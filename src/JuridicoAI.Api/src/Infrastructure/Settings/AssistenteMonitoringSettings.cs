namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class AssistenteMonitoringSettings
{
    public bool Enabled { get; set; }
    public string OtlpEndpoint { get; set; } = "http://localhost:4318/v1/metrics";
    public int CollectionIntervalSeconds { get; set; } = 60;
    public int QueryTimeoutSeconds { get; set; } = 5;
    public int StalledJobSeconds { get; set; } = 600;
    public int PendingDeletionSeconds { get; set; } = 900;
    public int OldReservationSeconds { get; set; } = 900;
}
