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
    token: json['token'] as String? ?? '',
    userId: json['userId'] as int? ?? 0,
    email: json['email'] as String? ?? '',
    fullName: json['fullName'] as String? ?? '',
    role: json['role'] as String? ?? '',
    expiresAt:
        DateTime.tryParse(json['expiresAt']?.toString() ?? '') ??
        DateTime.now(),
  );
}

class UserProfile {
  final int userId;
  final String email;
  final String fullName;
  final String? phone;
  final String role;

  UserProfile({
    required this.userId,
    required this.email,
    required this.fullName,
    required this.role,
    this.phone,
  });

  factory UserProfile.fromJson(Map<String, dynamic> json) => UserProfile(
    userId: json['userId'] as int? ?? 0,
    email: json['email'] as String? ?? '',
    fullName: json['fullName'] as String? ?? '',
    phone: json['phone'] as String?,
    role: json['role'] as String? ?? '',
  );
}

class DriverProfile {
  final int driverId;
  final String fullName;
  final String email;
  final String? phone;
  final String status;
  final double averageRating;
  final int totalTrips;

  DriverProfile({
    required this.driverId,
    required this.fullName,
    required this.email,
    required this.status,
    required this.averageRating,
    required this.totalTrips,
    this.phone,
  });

  factory DriverProfile.fromJson(Map<String, dynamic> json) => DriverProfile(
    driverId: json['driverId'] as int? ?? 0,
    fullName: json['fullName'] as String? ?? '',
    email: json['email'] as String? ?? '',
    phone: json['phone'] as String?,
    status: json['status'] as String? ?? 'Available',
    averageRating: (json['averageRating'] as num?)?.toDouble() ?? 0,
    totalTrips: json['totalTrips'] as int? ?? 0,
  );
}

class TripAssignment {
  final int assignmentId;
  final int? driverId;
  final String driverName;
  final String? driverPhone;
  final int? vehicleId;
  final String licensePlate;
  final String status;

  TripAssignment({
    required this.assignmentId,
    required this.driverName,
    required this.licensePlate,
    required this.status,
    this.driverId,
    this.driverPhone,
    this.vehicleId,
  });

  factory TripAssignment.fromJson(Map<String, dynamic> json) {
    final id = json['assignmentId'] as int?;
    if (id == null) {
      throw const FormatException('assignmentId missing');
    }
    return TripAssignment(
      assignmentId: id,
      driverId: json['driverId'] as int?,
      driverName: json['driverName'] as String? ?? '',
      driverPhone: json['driverPhone'] as String?,
      vehicleId: json['vehicleId'] as int?,
      licensePlate: json['licensePlate'] as String? ?? '—',
      status: json['status'] as String? ?? '',
    );
  }
}

class VehicleCondition {
  /// JSON body for VehicleConditionRequest. Omits null/blank fields.
  /// Returns null when nothing to send (empty complete, same as before).
  static Map<String, dynamic>? toJson({
    double? odometerKm,
    double? fuelLevel,
    String? condition,
    String? notes,
  }) {
    final body = <String, dynamic>{};
    if (odometerKm != null) body['odometerKm'] = odometerKm;
    if (fuelLevel != null) body['fuelLevel'] = fuelLevel;
    final c = condition?.trim();
    if (c != null && c.isNotEmpty) body['condition'] = c;
    final n = notes?.trim();
    if (n != null && n.isNotEmpty) body['notes'] = n;
    return body.isEmpty ? null : body;
  }
}

class AssignedVehicle {
  final int vehicleId;
  final String licensePlate;
  final String brand;
  final String model;
  final String status;

  AssignedVehicle({
    required this.vehicleId,
    required this.licensePlate,
    required this.brand,
    required this.model,
    required this.status,
  });

  factory AssignedVehicle.fromJson(Map<String, dynamic> json) =>
      AssignedVehicle(
        vehicleId: json['vehicleId'] as int? ?? 0,
        licensePlate: json['licensePlate'] as String? ?? '—',
        brand: json['brand'] as String? ?? '',
        model: json['model'] as String? ?? '',
        status: json['status'] as String? ?? '',
      );
}

class DriverBooking {
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
  final String rentalMode;
  final TripAssignment? assignment;
  final AssignedVehicle? assignedVehicle;

  DriverBooking({
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
    this.rentalMode = 'WithDriver',
    this.assignment,
    this.assignedVehicle,
  });

  bool get isSelfDrive => rentalMode == 'SelfDrive';

  bool get isDriverTrip => !isSelfDrive && assignment != null;

  int? get assignmentId => assignment?.assignmentId;

  factory DriverBooking.fromJson(Map<String, dynamic> json) {
    TripAssignment? assignment;
    final rawAssignment = json['assignment'];
    if (rawAssignment is Map<String, dynamic>) {
      try {
        assignment = TripAssignment.fromJson(rawAssignment);
      } catch (_) {
        assignment = null;
      }
    }

    AssignedVehicle? vehicle;
    final rawVehicle = json['assignedVehicle'];
    if (rawVehicle is Map<String, dynamic>) {
      try {
        vehicle = AssignedVehicle.fromJson(rawVehicle);
      } catch (_) {
        vehicle = null;
      }
    }

    return DriverBooking(
      bookingId: json['bookingId'] as int? ?? 0,
      customerName: json['customerName'] as String? ?? '—',
      vehicleTypeName: json['vehicleTypeName'] as String? ?? '—',
      pickupAddress: json['pickupAddress'] as String? ?? '—',
      dropoffAddress: json['dropoffAddress'] as String? ?? '—',
      startDate:
          DateTime.tryParse(json['startDate']?.toString() ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0),
      endDate:
          DateTime.tryParse(json['endDate']?.toString() ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0),
      totalAmount: (json['totalAmount'] as num?)?.toDouble() ?? 0,
      status: json['status'] as String? ?? '',
      notes: json['notes'] as String?,
      rentalMode: json['rentalMode'] as String? ?? 'WithDriver',
      assignment: assignment,
      assignedVehicle: vehicle,
    );
  }
}

class TripFilters {
  /// DriverApp only shows WithDriver trips that have a real assignment.
  static List<DriverBooking> forDriver(List<DriverBooking> items) {
    return items.where((t) => t.isDriverTrip).toList();
  }
}
