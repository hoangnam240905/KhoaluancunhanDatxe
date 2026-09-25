using Backend.Validation;
using Xunit;

namespace Backend.Tests;

public class ReviewRulesTests
{
    [Theory]
    [InlineData((byte)1, null)]
    [InlineData((byte)2, "")]
    [InlineData((byte)3, "   ")]
    public void Low_rating_empty_or_whitespace_comment_fails(byte rating, string? comment)
    {
        var error = ReviewRules.ValidateComment(rating, comment, out var normalized);
        Assert.Equal(ReviewRules.CommentRequired, error);
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData((byte)1, "Tài xế đến muộn")]
    [InlineData((byte)2, "  Cần cải thiện  ")]
    [InlineData((byte)3, "Xe ồn")]
    public void Low_rating_with_comment_passes(byte rating, string comment)
    {
        var error = ReviewRules.ValidateComment(rating, comment, out var normalized);
        Assert.Null(error);
        Assert.Equal(comment.Trim(), normalized);
    }

    [Theory]
    [InlineData((byte)4, null)]
    [InlineData((byte)5, "")]
    [InlineData((byte)5, "   ")]
    public void High_rating_without_comment_passes(byte rating, string? comment)
    {
        var error = ReviewRules.ValidateComment(rating, comment, out var normalized);
        Assert.Null(error);
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData((byte)4, "Tốt")]
    [InlineData((byte)5, "  Rất tốt  ")]
    public void High_rating_with_comment_passes(byte rating, string comment)
    {
        var error = ReviewRules.ValidateComment(rating, comment, out var normalized);
        Assert.Null(error);
        Assert.Equal(comment.Trim(), normalized);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)5)]
    public void Comment_over_500_fails(byte rating)
    {
        var error = ReviewRules.ValidateComment(rating, new string('x', 501), out _);
        Assert.Equal(ReviewRules.CommentTooLong, error);
    }

    [Fact]
    public void Comment_of_500_passes()
    {
        var comment = new string('y', 500);
        var error = ReviewRules.ValidateComment(3, comment, out var normalized);
        Assert.Null(error);
        Assert.Equal(500, normalized!.Length);
    }
}
