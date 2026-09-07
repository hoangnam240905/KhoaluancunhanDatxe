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
    DateOnly? DateOfBirth = null);

public record AdminCustomerListResponse(
    List<AdminCustomerResponse> Items,
    int Total,
    int Page,
    int PageSize);

public record LockCustomerRequest(bool IsLocked);

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
