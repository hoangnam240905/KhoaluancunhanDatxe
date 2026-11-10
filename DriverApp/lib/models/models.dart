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
    String? exteriorCondition,
    String? technicalCondition,
  }) {
    final body = <String, dynamic>{};
    if (odometerKm != null) body['odometerKm'] = odometerKm;
    if (fuelLevel != null) body['fuelLevel'] = fuelLevel;
    final c = condition?.trim();
    if (c != null && c.isNotEmpty) body['condition'] = c;
    final n = notes?.trim();
    if (n != null && n.isNotEmpty) body['notes'] = n;
    final exterior = exteriorCondition?.trim();
    if (exterior != null && exterior.isNotEmpty) {
      body['exteriorCondition'] = exterior;
    }
    final technical = technicalCondition?.trim();
    if (technical != null && technical.isNotEmpty) {
      body['technicalCondition'] = technical;
    }
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
  final String? exteriorCondition;
  final String? technicalCondition;
  final String? createdByName;
  final String? createdByRole;
  final String? updatedByName;
  final DateTime? updatedAt;
  final String? verificationStatus;
  final String? verificationNote;
  final String? verifiedByName;

  bool get isPreHandover => inspectionType == 'PreHandover';
  bool get isHandover => inspectionType == 'Handover';
  bool get isReturn => inspectionType == 'Return';

  const VehicleInspection({
    required this.inspectionId,
    required this.bookingId,
    required this.vehicleId,
    required this.inspectionType,
    required this.actualAt,
    this.odometerKm,
    this.fuelLevel,
    this.condition,
    this.notes,
    this.exteriorCondition,
    this.technicalCondition,
    this.createdByName,
    this.createdByRole,
    this.updatedByName,
    this.updatedAt,
    this.verificationStatus,
    this.verificationNote,
    this.verifiedByName,
  });

  factory VehicleInspection.fromJson(Map<String, dynamic> json) =>
      VehicleInspection(
        inspectionId: json['inspectionId'] as int? ?? 0,
        bookingId: json['bookingId'] as int? ?? 0,
        vehicleId: json['vehicleId'] as int? ?? 0,
        inspectionType: json['inspectionType'] as String? ?? '',
        actualAt:
            DateTime.tryParse(json['actualAt']?.toString() ?? '') ??
            DateTime.fromMillisecondsSinceEpoch(0),
        odometerKm: (json['odometerKm'] as num?)?.toDouble(),
        fuelLevel: (json['fuelLevel'] as num?)?.toDouble(),
        condition: json['condition'] as String?,
        notes: json['notes'] as String?,
        exteriorCondition: json['exteriorCondition'] as String?,
        technicalCondition: json['technicalCondition'] as String?,
        createdByName: json['createdByName'] as String?,
        createdByRole: json['createdByRole'] as String?,
        updatedByName: json['updatedByName'] as String?,
        updatedAt: DateTime.tryParse(json['updatedAt']?.toString() ?? ''),
        verificationStatus: json['verificationStatus'] as String?,
        verificationNote: json['verificationNote'] as String?,
        verifiedByName: json['verifiedByName'] as String?,
      );
}

/// Driver's report that the dispatcher's pre-handover record does not match the car (plan1.md Phần A).
class InspectionDispute {
  final int inspectionDisputeId;
  final String note;
  final String status;
  final String? resolvedByName;
  final String? resolutionNote;
  final DateTime createdAt;

  const InspectionDispute({
    required this.inspectionDisputeId,
    required this.note,
    required this.status,
    required this.createdAt,
    this.resolvedByName,
    this.resolutionNote,
  });

  bool get isOpen => status == 'Open';

  factory InspectionDispute.fromJson(Map<String, dynamic> json) =>
      InspectionDispute(
        inspectionDisputeId: json['inspectionDisputeId'] as int? ?? 0,
        note: json['note'] as String? ?? '',
        status: json['status'] as String? ?? '',
        resolvedByName: json['resolvedByName'] as String?,
        resolutionNote: json['resolutionNote'] as String?,
        createdAt:
            DateTime.tryParse(json['createdAt']?.toString() ?? '') ??
            DateTime.fromMillisecondsSinceEpoch(0),
      );
}

/// One photo of the licence / ID card and its review (plan1.md Phần F).
class UserDocument {
  final int userDocumentId;
  final String documentType;
  final String status;
  final String? rejectReason;
  final String? reviewedByName;

  const UserDocument({
    required this.userDocumentId,
    required this.documentType,
    required this.status,
    this.rejectReason,
    this.reviewedByName,
  });

  factory UserDocument.fromJson(Map<String, dynamic> json) => UserDocument(
    userDocumentId: json['userDocumentId'] as int? ?? 0,
    documentType: json['documentType'] as String? ?? '',
    status: json['status'] as String? ?? '',
    rejectReason: json['rejectReason'] as String?,
    reviewedByName: json['reviewedByName'] as String?,
  );
}

class LicenseClassOption {
  final String code;
  final String label;
  final bool allowedForDrivers;

  const LicenseClassOption({
    required this.code,
    required this.label,
    required this.allowedForDrivers,
  });

  factory LicenseClassOption.fromJson(Map<String, dynamic> json) =>
      LicenseClassOption(
        code: json['code'] as String? ?? '',
        label: json['label'] as String? ?? '',
        allowedForDrivers: json['allowedForDrivers'] as bool? ?? false,
      );
}

/// The driver's licence verification file (plan1.md Phần D).
/// photoStatus: Missing / Pending / Accepted / Rejected; verificationStatus: Pending / Verified / Rejected.
class DriverVerification {
  final String? licenseNumber;
  final String? licenseClass;
  final String? licenseClassLabel;
  final DateTime? licenseIssueDate;
  final DateTime? licenseExpiry;
  final String? idNumber;
  final String verificationStatus;
  final String photoStatus;
  final List<String> missingDocuments;
  final List<UserDocument> documents;
  final String? verifiedByName;
  final String? verificationNote;
  final bool licenseExpiringSoon;
  final bool licenseExpired;

  const DriverVerification({
    required this.verificationStatus,
    required this.photoStatus,
    required this.missingDocuments,
    required this.documents,
    this.licenseNumber,
    this.licenseClass,
    this.licenseClassLabel,
    this.licenseIssueDate,
    this.licenseExpiry,
    this.idNumber,
    this.verifiedByName,
    this.verificationNote,
    this.licenseExpiringSoon = false,
    this.licenseExpired = false,
  });

  bool get isVerified => verificationStatus == 'Verified';

  UserDocument? documentOf(String type) {
    for (final d in documents) {
      if (d.documentType == type) return d;
    }
    return null;
  }

  factory DriverVerification.fromJson(Map<String, dynamic> json) =>
      DriverVerification(
        licenseNumber: json['licenseNumber'] as String?,
        licenseClass: json['licenseClass'] as String?,
        licenseClassLabel: json['licenseClassLabel'] as String?,
        licenseIssueDate: DateTime.tryParse(
          json['licenseIssueDate']?.toString() ?? '',
        ),
        licenseExpiry: DateTime.tryParse(json['licenseExpiry']?.toString() ?? ''),
        idNumber: json['idNumber'] as String?,
        verificationStatus: json['verificationStatus'] as String? ?? 'Pending',
        photoStatus: json['photoStatus'] as String? ?? 'Missing',
        missingDocuments: (json['missingDocuments'] as List? ?? const [])
            .map((e) => e.toString())
            .toList(),
        documents: (json['documents'] as List? ?? const [])
            .whereType<Map<String, dynamic>>()
            .map(UserDocument.fromJson)
            .toList(),
        verifiedByName: json['verifiedByName'] as String?,
        verificationNote: json['verificationNote'] as String?,
        licenseExpiringSoon: json['licenseExpiringSoon'] as bool? ?? false,
        licenseExpired: json['licenseExpired'] as bool? ?? false,
      );
}

class IncidentReport {
  final int incidentId;
  final int bookingId;
  final int assignmentId;
  final int driverId;
  final String driverName;
  final String incidentType;
  final String description;
  final DateTime occurredAt;
  final String status;
  final DateTime createdAt;

  IncidentReport({
    required this.incidentId,
    required this.bookingId,
    required this.assignmentId,
    required this.driverId,
    required this.driverName,
    required this.incidentType,
    required this.description,
    required this.occurredAt,
    required this.status,
    required this.createdAt,
  });

  factory IncidentReport.fromJson(Map<String, dynamic> json) => IncidentReport(
    incidentId: json['incidentId'] as int,
    bookingId: json['bookingId'] as int,
    assignmentId: json['assignmentId'] as int,
    driverId: json['driverId'] as int,
    driverName: json['driverName'] as String? ?? '',
    incidentType: json['incidentType'] as String,
    description: json['description'] as String,
    occurredAt: DateTime.parse(json['occurredAt'] as String),
    status: json['status'] as String,
    createdAt: DateTime.parse(json['createdAt'] as String),
  );
}

class TripFilters {
  /// DriverApp only shows WithDriver trips that have a real assignment.
  static List<DriverBooking> forDriver(List<DriverBooking> items) {
    return items.where((t) => t.isDriverTrip).toList();
  }
}

/// One "Phụ lục đổi xe" entry (plan1.md Phần I): the dispatcher moved this trip to another car.
class VehicleChange {
  final int vehicleChangeId;
  final String oldVehicle;
  final String newVehicle;
  final String oldTypeName;
  final String newTypeName;
  final String reason;
  final String? note;
  final bool duringTrip;
  final double? oldOdometerKm;
  final double? oldFuelLevel;
  final double? newOdometerKm;
  final double? newFuelLevel;
  final String? approvedByName;
  final DateTime createdAt;

  const VehicleChange({
    required this.vehicleChangeId,
    required this.oldVehicle,
    required this.newVehicle,
    required this.oldTypeName,
    required this.newTypeName,
    required this.reason,
    required this.duringTrip,
    required this.createdAt,
    this.note,
    this.oldOdometerKm,
    this.oldFuelLevel,
    this.newOdometerKm,
    this.newFuelLevel,
    this.approvedByName,
  });

  String get reasonLabel => switch (reason) {
        'Breakdown' => 'Xe gặp sự cố kỹ thuật',
        'NeedChange' => 'Khách hàng thay đổi nhu cầu',
        _ => reason,
      };

  static double? _num(dynamic v) => v is num ? v.toDouble() : null;

  factory VehicleChange.fromJson(Map<String, dynamic> json) {
    final created = json['createdAt'] as String?;
    return VehicleChange(
      vehicleChangeId: json['vehicleChangeId'] as int? ?? 0,
      oldVehicle: json['oldVehicle'] as String? ?? '',
      newVehicle: json['newVehicle'] as String? ?? '',
      oldTypeName: json['oldTypeName'] as String? ?? '',
      newTypeName: json['newTypeName'] as String? ?? '',
      reason: json['reason'] as String? ?? '',
      note: json['note'] as String?,
      duringTrip: json['duringTrip'] as bool? ?? false,
      oldOdometerKm: _num(json['oldOdometerKm']),
      oldFuelLevel: _num(json['oldFuelLevel']),
      newOdometerKm: _num(json['newOdometerKm']),
      newFuelLevel: _num(json['newFuelLevel']),
      approvedByName: json['approvedByName'] as String?,
      // Backend stores UTC without a zone suffix.
      createdAt: created == null
          ? DateTime.now()
          : DateTime.parse(created.endsWith('Z') ? created : '${created}Z'),
    );
  }
}

/// One row of the driver's notification history (GET /api/notifications).
class DriverNotification {
  final int notificationId;
  final String title;
  final String message;
  final String? type;
  final bool isRead;
  final DateTime createdAt;
  final int? bookingId;

  const DriverNotification({
    required this.notificationId,
    required this.title,
    required this.message,
    required this.isRead,
    required this.createdAt,
    this.type,
    this.bookingId,
  });

  factory DriverNotification.fromJson(Map<String, dynamic> json) {
    final created = json['createdAt'] as String?;
    return DriverNotification(
      notificationId: json['notificationId'] as int? ?? 0,
      title: json['title'] as String? ?? 'Thông báo',
      message: json['message'] as String? ?? '',
      type: json['type'] as String?,
      isRead: json['isRead'] as bool? ?? false,
      // Backend stores UTC without a zone suffix.
      createdAt: created == null
          ? DateTime.now()
          : DateTime.parse(created.endsWith('Z') ? created : '${created}Z'),
      bookingId: json['bookingId'] as int?,
    );
  }
}
