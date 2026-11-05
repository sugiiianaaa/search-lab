namespace SearchLab.Ingest;

public sealed class HiringThreadFilter
{
    public static bool IsHiringThread(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        string lowerTitle = title.ToLowerInvariant();

        string hiringTitlePattern = "who is hiring?";

        if (!lowerTitle.Contains(hiringTitlePattern))
        {
            return false;
        }

        return true;
    }
}
