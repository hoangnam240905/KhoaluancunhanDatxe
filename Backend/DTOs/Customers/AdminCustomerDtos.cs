namespace Backend.DTOs.Customers;

public record AdminCustomerResponse(
    int CustomerId,
    string FullName,
    string Email,
    string? Phone,
    bool IsLocked,
    DateTime CreatedAt,
    bool IsActive = true,
    string? Address = null,
    string? IdNumber = null,
    DateOnly? DateOfBirth = null,
    string? LockReason = null,
    DateTime? LockedAt = null,
    int? LockedByUserId = null,
    string? LockedByName = null,
    string? InactiveReason = null,
    DateTime? InactivatedAt = null,
    int? InactivatedByUserId = null,
    string? InactivatedByName = null);

public record AdminCustomerListResponse(
    List<AdminCustomerResponse> Items,
    int Total,
    int Page,
    int PageSize);

public record LockCustomerRequest(bool IsLocked, string? Reason = null);

public record DeactivateCustomerRequest(string? Reason);

public record CreateAdminCustomerRequest(
    string FullName,
    string Email,
    string Phone,
    string Password,
    string? Address,
    string? IdNumber,
    DateOnly? DateOfBirth);

public record UpdateAdminCustomerRequest(
    string FullName,
    string Email,
    string Phone,
    string? Address,
    string? IdNumber,
    DateOnly? DateOfBirth);
