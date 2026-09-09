namespace Backend.Entities;

public class User
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public string? LockReason { get; set; }
    public DateTime? LockedAt { get; set; }
    public int? LockedByUserId { get; set; }
    public string? InactiveReason { get; set; }
    public DateTime? InactivatedAt { get; set; }
    public int? InactivatedByUserId { get; set; }
    public bool IsEmailVerified { get; set; } = true;
    public DateTime? EmailVerifiedAt { get; set; }
    public string? GoogleSubject { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public Customer? Customer { get; set; }
    public Driver? Driver { get; set; }
}
