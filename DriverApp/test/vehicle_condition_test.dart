import 'package:driver_app/models/models.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('VehicleCondition.toJson', () {
    test('all empty returns null so complete has no body', () {
      expect(VehicleCondition.toJson(), isNull);
      expect(
        VehicleCondition.toJson(
          odometerKm: null,
          fuelLevel: null,
          condition: '  ',
          notes: '',
        ),
        isNull,
      );
    });

    test('omits null and blank fields, camelCase keys only', () {
      final body = VehicleCondition.toJson(
        odometerKm: 12345,
        fuelLevel: 80,
        condition: ' OK ',
        notes: '  nứt gương  ',
      );
      expect(body, {
        'odometerKm': 12345,
        'fuelLevel': 80,
        'condition': 'OK',
        'notes': 'nứt gương',
      });
      expect(body!.containsKey('actualAt'), isFalse);
      expect(body.containsKey('inspectionType'), isFalse);
      expect(body.containsKey('amount'), isFalse);
    });

    test('partial body omits unused fields', () {
      final body = VehicleCondition.toJson(odometerKm: 1000);
      expect(body, {'odometerKm': 1000});
    });
  });
}
