namespace JuridicoAI.Api.Application.Services;

internal static class AssistenteTokenCost
{
    public static bool IsCacheUsageValid(int input, int? cached, int? written, decimal writeRate)
    {
        if (writeRate > 0m && (cached is null || written is null)) return false;
        var read = cached ?? 0;
        var write = written ?? 0;
        return read >= 0 && read <= input && write >= 0 && write <= input - read && (write == 0 || writeRate > 0m);
    }

    public static decimal Calculate(int input, int cached, int written, int output,
        decimal inputRate, decimal cachedRate, decimal writeRate, decimal outputRate)
    {
        var ordinaryInput = input - cached - written;
        return Math.Round((ordinaryInput * inputRate + cached * cachedRate + written * writeRate + output * outputRate)
            / 1_000_000m, 8, MidpointRounding.AwayFromZero);
    }
}
