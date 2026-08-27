import 'package:customer_app/utils/booking_pricing.dart';
import 'package:customer_app/utils/formatters.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('BookingPricing', () {
    test('ceil partial day to at least 1', () {
      final start = DateTime(2026, 8, 28, 8);
      final end = DateTime(2026, 8, 28, 16);
      expect(BookingPricing.rentalDays(start, end), 1);
    });

    test('exact 2 days stays 2', () {
      final start = DateTime(2026, 8, 28, 8);
      final end = DateTime(2026, 8, 30, 8);
      expect(BookingPricing.rentalDays(start, end), 2);
    });

    test('legacy undifferentiated preview still ceils days', () {
      final start = DateTime(2026, 9, 1, 8);
      final end = DateTime(2026, 9, 3, 18);
      final total = BookingPricing.estimate(
        pricePerDay: 1200000,
        pricePerKm: 15000,
        start: start,
        end: end,
        estimatedDistance: 300,
      );
      // 3 days * 1_200_000 + 300 * 15_000 = 3_600_000 + 4_500_000 = 8_100_000
      expect(total, 8100000);
    });
  });

  group('Formatters', () {
    test('rental mode labels', () {
      expect(Formatters.rentalModeLabel('WithDriver'), 'Có tài xế');
      expect(Formatters.rentalModeLabel('SelfDrive'), 'Tự lái');
      expect(Formatters.isSelfDrive('SelfDrive'), isTrue);
      expect(Formatters.isSelfDrive('WithDriver'), isFalse);
    });
  });
}
