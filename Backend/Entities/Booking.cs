namespace Backend.Entities;

public class Booking
{
    public int BookingId { get; set; }
    public int CustomerId { get; set; }
    public int VehicleTypeId { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public string DropoffAddress { get; set; } = string.Empty;
    public decimal? PickupLat { get; set; }
    public decimal? PickupLng { get; set; }
    public decimal? DropoffLat { get; set; }
    public decimal? DropoffLng { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal? EstimatedDistance { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? QuotedPricePerDay { get; set; }
    public decimal? QuotedPricePerKm { get; set; }
    public int? QuotedDays { get; set; }
    public decimal? QuotedDriverFeePerDay { get; set; }
    public decimal? QuotedSelfDriveIncludedKmPerDay { get; set; }
    public decimal? QuotedSelfDriveExtraKmPrice { get; set; }
    public decimal? QuotedDepositAmount { get; set; }
    public decimal? FinalAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string RentalMode { get; set; } = "WithDriver";
    public int? AssignedVehicleId { get; set; }
    public bool SourceRecommended { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public VehicleType VehicleType { get; set; } = null!;
    public Vehicle? AssignedVehicle { get; set; }
    public TripAssignment? TripAssignment { get; set; }
    public ICollection<Payment> Payments { get; set; } = [];
    public Contract? Contract { get; set; }
    public Review? Review { get; set; }
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<VehicleInspection> Inspections { get; set; } = [];
    public ICollection<BookingFee> Fees { get; set; } = [];
}
