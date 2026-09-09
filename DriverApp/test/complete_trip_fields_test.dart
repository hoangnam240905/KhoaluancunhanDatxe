import 'package:driver_app/utils/complete_trip_fields.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('CompleteTripFields.parse', () {
    test('empty odometer is required error', () {
      final parsed = CompleteTripFields.parse('', '80');
      expect(parsed.fields, isNull);
      expect(parsed.error, CompleteTripFields.odometerRequired);
    });

    test('negative odometer is invalid', () {
      final parsed = CompleteTripFields.parse('-1', '80');
      expect(parsed.fields, isNull);
      expect(parsed.error, CompleteTripFields.odometerInvalid);
    });

    test('empty fuel is required error', () {
      final parsed = CompleteTripFields.parse('52300', '');
      expect(parsed.fields, isNull);
      expect(parsed.error, CompleteTripFields.fuelRequired);
    });

    test('fuel above 100 is invalid', () {
      final parsed = CompleteTripFields.parse('52300', '101');
      expect(parsed.fields, isNull);
      expect(parsed.error, CompleteTripFields.fuelInvalid);
    });

    test('valid odometer and fuel', () {
      final parsed = CompleteTripFields.parse('52300', '80');
      expect(parsed.error, isNull);
      expect(parsed.fields!.odometerKm, 52300);
      expect(parsed.fields!.fuelLevel, 80);
    });

    test('comma is treated as decimal separator', () {
      final parsed = CompleteTripFields.parse('52,5', '80');
      expect(parsed.error, isNull);
      expect(parsed.fields!.odometerKm, 52.5);
    });

    test('zero odometer and zero fuel are allowed', () {
      final parsed = CompleteTripFields.parse('0', '0');
      expect(parsed.error, isNull);
      expect(parsed.fields!.odometerKm, 0);
      expect(parsed.fields!.fuelLevel, 0);
    });
  });
}
