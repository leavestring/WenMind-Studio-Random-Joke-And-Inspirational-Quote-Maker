namespace WenMind.Daily;

internal sealed record AppSettings(bool ShowStrip = true, int? RefreshMinutes = null)
{
    public const int MinimumMinutes = 1;
    public const int MaximumMinutes = 1440;
}
