import 'package:driver_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('VehicleChange parses a mid-trip change from the API', () {
    final c = VehicleChange.fromJson({
      'vehicleChangeId': 3,
      'oldVehicle': 'Hyundai Accent · 51B-67890',
      'newVehicle': 'Toyota Vios · 51A-12345',
      'oldTypeName': '4 chỗ - Sedan',
      'newTypeName': '4 chỗ - Sedan',
      'reason': 'Breakdown',
      'note': 'Đèn động cơ sáng',
      'duringTrip': true,
      'oldOdometerKm': 22020.0,
      'oldFuelLevel': 40,
      'newOdometerKm': 45100,
      'newFuelLevel': 90.0,
      'approvedByName': 'Trần Thị Điều Phối',
      'createdAt': '2026-11-10T08:42:57.0369481',
    });

    expect(c.reasonLabel, 'Xe gặp sự cố kỹ thuật');
    expect(c.duringTrip, isTrue);
    expect(c.oldOdometerKm, 22020);
    expect(c.newFuelLevel, 90);
    expect(c.approvedByName, 'Trần Thị Điều Phối');
    expect(c.createdAt.isUtc, isTrue);
    expect(c.createdAt.hour, 8);
  });

  test('VehicleChange before pickup has no readings', () {
    final c = VehicleChange.fromJson({
      'vehicleChangeId': 1,
      'reason': 'NeedChange',
      'duringTrip': false,
      'createdAt': '2026-11-10T08:00:00Z',
    });
    expect(c.reasonLabel, 'Khách hàng thay đổi nhu cầu');
    expect(c.oldOdometerKm, isNull);
    expect(c.approvedByName, isNull);
  });
}
