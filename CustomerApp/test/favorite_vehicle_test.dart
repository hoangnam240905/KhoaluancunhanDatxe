import 'package:customer_app/models/models.dart';
import 'package:customer_app/navigation/role_router.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('FavoriteVehicle parses a VehicleResponse row from /api/favorites', () {
    final v = FavoriteVehicle.fromJson({
      'vehicleId': 2,
      'typeId': 1,
      'typeName': '4 chỗ - Sedan',
      'licensePlate': '51B-67890',
      'brand': 'Hyundai',
      'model': 'Accent',
      'year': 2023,
      'color': 'Bạc',
      'status': 'Available',
      'currentKm': 22000,
      'pricePerDay': 800000,
    });
    expect(v.title, 'Hyundai Accent');
    expect(v.typeId, 1);
    expect(v.pricePerDay, 800000);
    expect(v.color, 'Bạc');
  });

  test('Customer app only lets customers in; drivers are sent to DriverApp', () {
    expect(RoleRouter.mobileRoleError('Customer'), isNull);
    expect(RoleRouter.mobileRoleError('Driver'), contains('DriverApp'));
    expect(RoleRouter.mobileRoleError('Admin'), contains('Web'));
  });
}
