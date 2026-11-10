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

    public static bool IsWhoIsHiringAuthor(string? author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return false;
        }

        string lowerAuthor = author.ToLowerInvariant();

        string hiringAuthorPattern = "whoishiring";

        if (!lowerAuthor.Contains(hiringAuthorPattern))
        {
            return false;
        }

        return true;
    }
}
