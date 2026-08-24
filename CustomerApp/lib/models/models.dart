class AuthResponse {
  final String token;
  final int userId;
  final String email;
  final String fullName;
  final String role;
  final DateTime expiresAt;

  AuthResponse({
    required this.token,
    required this.userId,
    required this.email,
    required this.fullName,
    required this.role,
    required this.expiresAt,
  });

  factory AuthResponse.fromJson(Map<String, dynamic> json) => AuthResponse(
        token: json['token'] as String,
        userId: json['userId'] as int,
        email: json['email'] as String,
        fullName: json['fullName'] as String,
        role: json['role'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String),
      );
}

class VehicleType {
  final int typeId;
  final String typeName;
  final int seatCapacity;
  final double pricePerDay;
  final double pricePerKm;
  final String? description;

  VehicleType({
    required this.typeId,
    required this.typeName,
    required this.seatCapacity,
    required this.pricePerDay,
    required this.pricePerKm,
    this.description,
  });

  factory VehicleType.fromJson(Map<String, dynamic> json) => VehicleType(
        typeId: json['typeId'] as int,
        typeName: json['typeName'] as String,
        seatCapacity: json['seatCapacity'] as int,
        pricePerDay: (json['pricePerDay'] as num).toDouble(),
        pricePerKm: (json['pricePerKm'] as num).toDouble(),
        description: json['description'] as String?,
      );
}

class Booking {
  final int bookingId;
  final String customerName;
  final String vehicleTypeName;
  final String pickupAddress;
  final String dropoffAddress;
  final DateTime startDate;
  final DateTime endDate;
  final double totalAmount;
  final String status;
  final String? notes;
  final TripAssignment? assignment;

  Booking({
    required this.bookingId,
    required this.customerName,
    required this.vehicleTypeName,
    required this.pickupAddress,
    required this.dropoffAddress,
    required this.startDate,
    required this.endDate,
    required this.totalAmount,
    required this.status,
    this.notes,
    this.assignment,
  });

  factory Booking.fromJson(Map<String, dynamic> json) => Booking(
        bookingId: json['bookingId'] as int,
        customerName: json['customerName'] as String,
        vehicleTypeName: json['vehicleTypeName'] as String,
        pickupAddress: json['pickupAddress'] as String,
        dropoffAddress: json['dropoffAddress'] as String,
        startDate: DateTime.parse(json['startDate'] as String),
        endDate: DateTime.parse(json['endDate'] as String),
        totalAmount: (json['totalAmount'] as num).toDouble(),
        status: json['status'] as String,
        notes: json['notes'] as String?,
        assignment: json['assignment'] != null
            ? TripAssignment.fromJson(json['assignment'] as Map<String, dynamic>)
            : null,
      );
}

class TripAssignment {
  final int assignmentId;
  final String driverName;
  final String? driverPhone;
  final String licensePlate;
  final String status;

  TripAssignment({
    required this.assignmentId,
    required this.driverName,
    this.driverPhone,
    required this.licensePlate,
    required this.status,
  });

  factory TripAssignment.fromJson(Map<String, dynamic> json) => TripAssignment(
        assignmentId: json['assignmentId'] as int,
        driverName: json['driverName'] as String,
        driverPhone: json['driverPhone'] as String?,
        licensePlate: json['licensePlate'] as String,
        status: json['status'] as String,
      );
}

class DriverBooking extends Booking {
  DriverBooking({
    required super.bookingId,
    required super.customerName,
    required super.vehicleTypeName,
    required super.pickupAddress,
    required super.dropoffAddress,
    required super.startDate,
    required super.endDate,
    required super.totalAmount,
    required super.status,
    super.notes,
    super.assignment,
  });

  factory DriverBooking.fromJson(Map<String, dynamic> json) => DriverBooking(
        bookingId: json['bookingId'] as int,
        customerName: json['customerName'] as String,
        vehicleTypeName: json['vehicleTypeName'] as String,
        pickupAddress: json['pickupAddress'] as String,
        dropoffAddress: json['dropoffAddress'] as String,
        startDate: DateTime.parse(json['startDate'] as String),
        endDate: DateTime.parse(json['endDate'] as String),
        totalAmount: (json['totalAmount'] as num).toDouble(),
        status: json['status'] as String,
        notes: json['notes'] as String?,
        assignment: json['assignment'] != null
            ? TripAssignment.fromJson(json['assignment'] as Map<String, dynamic>)
            : null,
      );

  int? get assignmentId => assignment?.assignmentId;
}
