namespace Backend.Validation;

public static class ReviewRules
{
    public const int CommentMaxLength = 500;
    public const string CommentRequired = "Vui lòng nhập nhận xét khi đánh giá từ 1 đến 3 sao.";
    public const string CommentTooLong = "Nhận xét không được vượt quá 500 ký tự.";

    public static string? NormalizeComment(string? comment)
    {
        var trimmed = comment?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static string? ValidateComment(byte rating, string? comment, out string? normalizedComment)
    {
        normalizedComment = NormalizeComment(comment);

        if (rating is >= 1 and <= 3 && normalizedComment is null)
            return CommentRequired;

        if (normalizedComment is { Length: > CommentMaxLength })
            return CommentTooLong;

        return null;
    }
}
