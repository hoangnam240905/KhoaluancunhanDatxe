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
    userId: json['userId'] as int,
    email: json['email'] as String,
    fullName: json['fullName'] as String,
    phone: json['phone'] as String?,
    role: json['role'] as String,
  );
}

class VehicleType {
  final int typeId;
  final String typeName;
  final int seatCapacity;
  final double pricePerDay;
  final double pricePerKm;
  final String? description;
  final String? imageUrl;

  VehicleType({
    required this.typeId,
    required this.typeName,
    required this.seatCapacity,
    required this.pricePerDay,
    required this.pricePerKm,
    this.description,
    this.imageUrl,
  });

  factory VehicleType.fromJson(Map<String, dynamic> json) => VehicleType(
    typeId: json['typeId'] as int,
    typeName: json['typeName'] as String,
    seatCapacity: json['seatCapacity'] as int,
    pricePerDay: (json['pricePerDay'] as num).toDouble(),
    pricePerKm: (json['pricePerKm'] as num).toDouble(),
    description: json['description'] as String?,
    imageUrl: json['imageUrl'] as String?,
  );
}

class BookingQuote {
  final int vehicleTypeId;
  final String vehicleTypeName;
  final String rentalMode;
  final DateTime startDate;
  final DateTime endDate;
  final double quotedPricePerDay;
  final double quotedPricePerKm;
  final int quotedDays;
  final double estimatedDistance;
  final double rentalAmount;
  final double distanceAmount;
  final double totalAmount;
  final double driverAmount;
  final double includedKm;
  final double extraKm;
  final double extraKmPrice;
  final double depositAmount;

  BookingQuote({
    required this.vehicleTypeId,
    required this.vehicleTypeName,
    required this.rentalMode,
    required this.startDate,
    required this.endDate,
    required this.quotedPricePerDay,
    required this.quotedPricePerKm,
    required this.quotedDays,
    required this.estimatedDistance,
    required this.rentalAmount,
    required this.distanceAmount,
    required this.totalAmount,
    this.driverAmount = 0,
    this.includedKm = 0,
    this.extraKm = 0,
    this.extraKmPrice = 0,
    this.depositAmount = 0,
  });

  factory BookingQuote.fromJson(Map<String, dynamic> json) => BookingQuote(
    vehicleTypeId: json['vehicleTypeId'] as int,
    vehicleTypeName: json['vehicleTypeName'] as String,
    rentalMode: json['rentalMode'] as String,
    startDate: DateTime.parse(json['startDate'] as String),
    endDate: DateTime.parse(json['endDate'] as String),
    quotedPricePerDay: (json['quotedPricePerDay'] as num).toDouble(),
    quotedPricePerKm: (json['quotedPricePerKm'] as num).toDouble(),
    quotedDays: json['quotedDays'] as int,
    estimatedDistance: (json['estimatedDistance'] as num).toDouble(),
    rentalAmount: (json['rentalAmount'] as num).toDouble(),
    distanceAmount: (json['distanceAmount'] as num).toDouble(),
    totalAmount: (json['totalAmount'] as num).toDouble(),
    driverAmount: (json['driverAmount'] as num?)?.toDouble() ?? 0,
    includedKm: (json['includedKm'] as num?)?.toDouble() ?? 0,
    extraKm: (json['extraKm'] as num?)?.toDouble() ?? 0,
    extraKmPrice: (json['extraKmPrice'] as num?)?.toDouble() ?? 0,
    depositAmount: (json['depositAmount'] as num?)?.toDouble() ?? 0,
  );
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
        vehicleId: json['vehicleId'] as int,
        licensePlate: json['licensePlate'] as String,
        brand: json['brand'] as String,
        model: json['model'] as String,
        status: json['status'] as String,
      );
}

class BookingFee {
  final int feeId;
  final String feeType;
  final String? description;
  final double amount;
  final DateTime createdAt;

  BookingFee({
    required this.feeId,
    required this.feeType,
    required this.amount,
    required this.createdAt,
    this.description,
  });

  bool get isIncludedInBase => feeType == 'ExtraKm';

  factory BookingFee.fromJson(Map<String, dynamic> json) => BookingFee(
    feeId: json['feeId'] as int,
    feeType: json['feeType'] as String? ?? '',
    description: json['description'] as String?,
    amount: (json['amount'] as num?)?.toDouble() ?? 0,
    createdAt: DateTime.parse(json['createdAt'] as String),
  );
}

class VehicleInspection {
  final int inspectionId;
  final int bookingId;
  final int vehicleId;
  final String inspectionType;
  final DateTime actualAt;
  final double? odometerKm;
  final double? fuelLevel;
  final String? condition;
  final String? notes;
  final DateTime createdAt;

  VehicleInspection({
    required this.inspectionId,
    required this.bookingId,
    required this.vehicleId,
    required this.inspectionType,
    required this.actualAt,
    required this.createdAt,
    this.odometerKm,
    this.fuelLevel,
    this.condition,
    this.notes,
  });

  factory VehicleInspection.fromJson(Map<String, dynamic> json) =>
      VehicleInspection(
        inspectionId: json['inspectionId'] as int,
        bookingId: json['bookingId'] as int,
        vehicleId: json['vehicleId'] as int,
        inspectionType: json['inspectionType'] as String? ?? '',
        actualAt: DateTime.parse(json['actualAt'] as String),
        odometerKm: (json['odometerKm'] as num?)?.toDouble(),
        fuelLevel: (json['fuelLevel'] as num?)?.toDouble(),
        condition: json['condition'] as String?,
        notes: json['notes'] as String?,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

class Booking {
  final int bookingId;
  final int? customerId;
  final String customerName;
  final int vehicleTypeId;
  final String vehicleTypeName;
  final String pickupAddress;
  final String dropoffAddress;
  final DateTime startDate;
  final DateTime endDate;
  final double? estimatedDistance;
  final double totalAmount;
  final String status;
  final String? notes;
  final String rentalMode;
  final TripAssignment? assignment;
  final AssignedVehicle? assignedVehicle;
  final double? quotedDepositAmount;
  final double? quotedPricePerDay;
  final double? quotedPricePerKm;
  final int? quotedDays;
  final double? quotedDriverFeePerDay;
  final double? quotedSelfDriveIncludedKmPerDay;
  final double? quotedSelfDriveExtraKmPrice;
  final double? finalAmount;
  final List<BookingFee> fees;
  final double? finalBaseAmount;
  final double? totalFees;
  final List<VehicleInspection> inspections;

  Booking({
    required this.bookingId,
    required this.customerName,
    required this.vehicleTypeId,
    required this.vehicleTypeName,
    required this.pickupAddress,
    required this.dropoffAddress,
    required this.startDate,
    required this.endDate,
    required this.totalAmount,
    required this.status,
    this.customerId,
    this.estimatedDistance,
    this.notes,
    this.rentalMode = 'WithDriver',
    this.assignment,
    this.assignedVehicle,
    this.quotedDepositAmount,
    this.quotedPricePerDay,
    this.quotedPricePerKm,
    this.quotedDays,
    this.quotedDriverFeePerDay,
    this.quotedSelfDriveIncludedKmPerDay,
    this.quotedSelfDriveExtraKmPrice,
    this.finalAmount,
    this.fees = const [],
    this.finalBaseAmount,
    this.totalFees,
    this.inspections = const [],
  });

  bool get isSelfDrive => rentalMode == 'SelfDrive';

  bool get hasPriceSnapshot =>
      quotedPricePerDay != null ||
      quotedPricePerKm != null ||
      quotedDays != null ||
      quotedDriverFeePerDay != null ||
      quotedSelfDriveIncludedKmPerDay != null ||
      quotedSelfDriveExtraKmPrice != null ||
      quotedDepositAmount != null;

  factory Booking.fromJson(Map<String, dynamic> json) => Booking(
    bookingId: json['bookingId'] as int,
    customerId: json['customerId'] as int?,
    customerName: json['customerName'] as String,
    vehicleTypeId: json['vehicleTypeId'] as int? ?? 0,
    vehicleTypeName: json['vehicleTypeName'] as String,
    pickupAddress: json['pickupAddress'] as String,
    dropoffAddress: json['dropoffAddress'] as String,
    startDate: DateTime.parse(json['startDate'] as String),
    endDate: DateTime.parse(json['endDate'] as String),
    estimatedDistance: (json['estimatedDistance'] as num?)?.toDouble(),
    totalAmount: (json['totalAmount'] as num).toDouble(),
    status: json['status'] as String,
    notes: json['notes'] as String?,
    rentalMode: json['rentalMode'] as String? ?? 'WithDriver',
    assignment: json['assignment'] != null
        ? TripAssignment.fromJson(json['assignment'] as Map<String, dynamic>)
        : null,
    assignedVehicle: json['assignedVehicle'] != null
        ? AssignedVehicle.fromJson(
            json['assignedVehicle'] as Map<String, dynamic>,
          )
        : null,
    quotedDepositAmount: (json['quotedDepositAmount'] as num?)?.toDouble(),
    quotedPricePerDay: (json['quotedPricePerDay'] as num?)?.toDouble(),
    quotedPricePerKm: (json['quotedPricePerKm'] as num?)?.toDouble(),
    quotedDays: json['quotedDays'] as int?,
    quotedDriverFeePerDay: (json['quotedDriverFeePerDay'] as num?)?.toDouble(),
    quotedSelfDriveIncludedKmPerDay:
        (json['quotedSelfDriveIncludedKmPerDay'] as num?)?.toDouble(),
    quotedSelfDriveExtraKmPrice: (json['quotedSelfDriveExtraKmPrice'] as num?)
        ?.toDouble(),
    finalAmount: (json['finalAmount'] as num?)?.toDouble(),
    fees: _parseFees(json['fees']),
    finalBaseAmount: (json['finalBaseAmount'] as num?)?.toDouble(),
    totalFees: (json['totalFees'] as num?)?.toDouble(),
    inspections: _parseInspections(json['inspections']),
  );

  static List<BookingFee> _parseFees(dynamic raw) {
    if (raw is! List) return const [];
    return raw
        .map((e) => BookingFee.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  static List<VehicleInspection> _parseInspections(dynamic raw) {
    if (raw is! List) return const [];
    return raw
        .map((e) => VehicleInspection.fromJson(e as Map<String, dynamic>))
        .toList();
  }
}

class Payment {
  final int paymentId;
  final int bookingId;
  final String? paymentType;
  final double amount;
  final String method;
  final String status;
  final String? transactionRef;
  final DateTime? paidAt;
  final DateTime createdAt;

  Payment({
    required this.paymentId,
    required this.bookingId,
    required this.amount,
    required this.method,
    required this.status,
    required this.createdAt,
    this.paymentType,
    this.transactionRef,
    this.paidAt,
  });

  bool get isDeposit => paymentType == 'Deposit';

  bool get blocksNewDeposit =>
      isDeposit && (status == 'Pending' || status == 'Paid');

  factory Payment.fromJson(Map<String, dynamic> json) => Payment(
    paymentId: json['paymentId'] as int,
    bookingId: json['bookingId'] as int,
    paymentType: json['paymentType'] as String?,
    amount: (json['amount'] as num).toDouble(),
    method: json['method'] as String,
    status: json['status'] as String,
    transactionRef: json['transactionRef'] as String?,
    paidAt: json['paidAt'] == null
        ? null
        : DateTime.parse(json['paidAt'] as String),
    createdAt: DateTime.parse(json['createdAt'] as String),
  );

  /// Body for POST /api/payments. Never includes amount.
  static Map<String, dynamic> depositCreateBody({
    required int bookingId,
    required String method,
    String? transactionRef,
  }) {
    final body = <String, dynamic>{
      'bookingId': bookingId,
      'paymentType': 'Deposit',
      'method': method,
    };
    final ref = transactionRef?.trim();
    if (ref != null && ref.isNotEmpty) {
      body['transactionRef'] = ref;
    }
    return body;
  }
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
    required super.vehicleTypeId,
    required super.vehicleTypeName,
    required super.pickupAddress,
    required super.dropoffAddress,
    required super.startDate,
    required super.endDate,
    required super.totalAmount,
    required super.status,
    super.notes,
    super.assignment,
    super.rentalMode,
    super.estimatedDistance,
    super.assignedVehicle,
    super.quotedDepositAmount,
  });

  factory DriverBooking.fromJson(Map<String, dynamic> json) => DriverBooking(
    bookingId: json['bookingId'] as int,
    customerName: json['customerName'] as String,
    vehicleTypeId: json['vehicleTypeId'] as int? ?? 0,
    vehicleTypeName: json['vehicleTypeName'] as String,
    pickupAddress: json['pickupAddress'] as String,
    dropoffAddress: json['dropoffAddress'] as String,
    startDate: DateTime.parse(json['startDate'] as String),
    endDate: DateTime.parse(json['endDate'] as String),
    totalAmount: (json['totalAmount'] as num).toDouble(),
    status: json['status'] as String,
    notes: json['notes'] as String?,
    rentalMode: json['rentalMode'] as String? ?? 'WithDriver',
    estimatedDistance: (json['estimatedDistance'] as num?)?.toDouble(),
    assignment: json['assignment'] != null
        ? TripAssignment.fromJson(json['assignment'] as Map<String, dynamic>)
        : null,
    assignedVehicle: json['assignedVehicle'] != null
        ? AssignedVehicle.fromJson(
            json['assignedVehicle'] as Map<String, dynamic>,
          )
        : null,
    quotedDepositAmount: (json['quotedDepositAmount'] as num?)?.toDouble(),
  );

  int? get assignmentId => assignment?.assignmentId;
}
