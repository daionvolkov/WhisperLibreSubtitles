
namespace WhisperApi.Utils;

public static class TimeFormat
{
    public static string ToHhMmSsMmm(double seconds)
    {
        if (seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalMinutes:00}:{ts.Seconds:00}.{ts.Milliseconds:000}";
    }
}