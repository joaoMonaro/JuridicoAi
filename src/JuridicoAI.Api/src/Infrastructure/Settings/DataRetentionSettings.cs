namespace JuridicoAI.Api.Infrastructure.Settings;

public sealed class DataRetentionSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 6;
    public int SoftDeleteRetentionDays { get; set; } = 30;
    public int ExpiredSessionRetentionDays { get; set; } = 30;
    public int AuditLogRetentionDays { get; set; } = 365;
}
