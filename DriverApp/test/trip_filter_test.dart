import 'package:driver_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  DriverBooking booking({
    required String mode,
    Map<String, dynamic>? assignment,
  }) {
    return DriverBooking.fromJson({
      'bookingId': 1,
      'customerName': 'Khach',
      'vehicleTypeName': 'Sedan',
      'pickupAddress': 'A',
      'dropoffAddress': 'B',
      'startDate': '2026-09-01T08:00:00',
      'endDate': '2026-09-01T18:00:00',
      'totalAmount': 1000,
      'status': 'Assigned',
      'rentalMode': mode,
      'assignment': assignment,
    });
  }

  test('SelfDrive is filtered out even if assignment sneaks in', () {
    final trips = TripFilters.forDriver([
      booking(mode: 'SelfDrive', assignment: null),
      booking(
        mode: 'SelfDrive',
        assignment: {
          'assignmentId': 9,
          'driverName': 'Dummy',
          'licensePlate': '51A-000',
          'status': 'Assigned',
        },
      ),
    ]);
    expect(trips, isEmpty);
  });

  test('WithDriver without assignment is skipped', () {
    final trips = TripFilters.forDriver([
      booking(mode: 'WithDriver', assignment: null),
    ]);
    expect(trips, isEmpty);
  });

  test('WithDriver with assignment is kept', () {
    final trips = TripFilters.forDriver([
      booking(
        mode: 'WithDriver',
        assignment: {
          'assignmentId': 3,
          'driverName': 'Tai xe',
          'licensePlate': '51A-12345',
          'status': 'Assigned',
        },
      ),
    ]);
    expect(trips.length, 1);
    expect(trips.first.assignmentId, 3);
  });

  test('null assignment json does not crash', () {
    final parsed = DriverBooking.fromJson({
      'bookingId': 2,
      'customerName': 'Khach',
      'vehicleTypeName': 'SUV',
      'pickupAddress': 'A',
      'dropoffAddress': 'B',
      'startDate': 'not-a-date',
      'endDate': null,
      'status': 'Pending',
      'assignment': 'bad',
      'assignedVehicle': null,
    });
    expect(parsed.assignment, isNull);
    expect(parsed.isDriverTrip, isFalse);
  });

  test('DriverProfile parses server status', () {
    final profile = DriverProfile.fromJson({
      'driverId': 5,
      'fullName': 'Tai xe',
      'email': 'driver1@carrental.vn',
      'status': 'Busy',
      'averageRating': 4.8,
      'totalTrips': 120,
    });
    expect(profile.status, 'Busy');
    expect(profile.driverId, 5);
  });
}
