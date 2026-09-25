namespace Backend.Entities;

public class EmailOtp
{
    public int EmailOtpId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
