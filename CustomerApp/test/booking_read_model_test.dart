import 'package:customer_app/models/models.dart';
import 'package:customer_app/utils/formatters.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Map<String, dynamic> bookingJson({
    int bookingId = 1,
    double totalAmount = 6600000,
    Object? finalAmount,
    Object? quotedDepositAmount,
    Object? quotedPricePerDay,
    List<Map<String, dynamic>>? fees,
    Object? totalFees,
    Object? finalBaseAmount,
    List<Map<String, dynamic>>? inspections,
    String rentalMode = 'WithDriver',
    Map<String, dynamic>? assignment,
  }) => {
    'bookingId': bookingId,
    'customerId': 3,
    'customerName': 'Le Van Khach',
    'vehicleTypeId': 1,
    'vehicleTypeName': '4 cho - Sedan',
    'pickupAddress': 'A',
    'dropoffAddress': 'B',
    'startDate': '2026-08-20T08:00:00',
    'endDate': '2026-08-22T18:00:00',
    'estimatedDistance': 100,
    'totalAmount': totalAmount,
    'status': 'Pending',
    'rentalMode': rentalMode,
    'quotedDepositAmount': quotedDepositAmount,
    'quotedPricePerDay': quotedPricePerDay,
    'finalAmount': finalAmount,
    'fees': fees,
    'totalFees': totalFees,
    'finalBaseAmount': finalBaseAmount,
    'inspections': inspections,
    'assignment': assignment,
  };

  group('legacy bookings #1/#2', () {
    test('null snapshot final inspections still parse', () {
      final booking = Booking.fromJson(bookingJson());
      expect(booking.totalAmount, 6600000);
      expect(booking.finalAmount, isNull);
      expect(booking.quotedDepositAmount, isNull);
      expect(booking.quotedPricePerDay, isNull);
      expect(booking.hasPriceSnapshot, isFalse);
      expect(booking.fees, isEmpty);
      expect(booking.inspections, isEmpty);
      expect(booking.totalFees, isNull);
      expect(booking.assignment, isNull);
    });

    test('missing fees/inspections keys default to empty', () {
      final json = bookingJson()
        ..remove('fees')
        ..remove('inspections');
      final booking = Booking.fromJson(json);
      expect(booking.fees, isEmpty);
      expect(booking.inspections, isEmpty);
    });
  });

  group('final amount and fees', () {
    test('finalAmount is displayed value from server only', () {
      final booking = Booking.fromJson(
        bookingJson(
          totalAmount: 2400000,
          finalAmount: 2800000,
          totalFees: 0,
          finalBaseAmount: 2800000,
          fees: [
            {
              'feeId': 1,
              'feeType': 'ExtraKm',
              'description': 'Km vượt hạn mức 50 km (đã gồm trong giá chốt)',
              'amount': 600000,
              'createdAt': '2026-08-27T10:00:00Z',
            },
          ],
        ),
      );
      expect(booking.totalAmount, 2400000);
      expect(booking.finalAmount, 2800000);
      expect(booking.totalFees, 0);
      expect(booking.fees.single.isIncludedInBase, isTrue);
      expect(booking.fees.single.amount, 600000);
      expect(
        booking.totalAmount + booking.fees.single.amount,
        isNot(booking.finalAmount),
      );
    });

    test('does not treat ExtraKm as additive totalFees', () {
      final booking = Booking.fromJson(
        bookingJson(
          fees: [
            {
              'feeId': 2,
              'feeType': 'ExtraKm',
              'description': 'đã gồm trong giá chốt',
              'amount': 400000,
              'createdAt': '2026-08-27T10:00:00Z',
            },
          ],
          totalFees: 0,
        ),
      );
      final clientSum = booking.fees.fold<double>(0, (s, f) => s + f.amount);
      expect(clientSum, 400000);
      expect(booking.totalFees, 0);
      expect(booking.totalFees, isNot(clientSum));
    });
  });

  group('inspections', () {
    test('SelfDrive handover and return', () {
      final booking = Booking.fromJson(
        bookingJson(
          bookingId: 25,
          rentalMode: 'SelfDrive',
          inspections: [
            {
              'inspectionId': 4,
              'bookingId': 25,
              'vehicleId': 2,
              'inspectionType': 'Handover',
              'actualAt': '2026-08-27T08:00:00Z',
              'odometerKm': 1000,
              'fuelLevel': 80,
              'condition': 'OK',
              'notes': null,
              'createdAt': '2026-08-27T08:00:01Z',
            },
            {
              'inspectionId': 5,
              'bookingId': 25,
              'vehicleId': 2,
              'inspectionType': 'Return',
              'actualAt': '2026-08-27T18:00:00Z',
              'odometerKm': 1100,
              'fuelLevel': 50,
              'condition': 'OK',
              'notes': null,
              'createdAt': '2026-08-27T18:00:01Z',
            },
          ],
        ),
      );
      expect(booking.isSelfDrive, isTrue);
      expect(booking.assignment, isNull);
      expect(booking.inspections.map((e) => e.inspectionType), [
        'Handover',
        'Return',
      ]);
      expect(Formatters.inspectionTypeLabel('Handover'), 'Giao xe');
      expect(Formatters.inspectionTypeLabel('Return'), 'Trả xe');
    });
  });

  group('deposit create body unchanged', () {
    test('still omits amount', () {
      final body = Payment.depositCreateBody(
        bookingId: 22,
        method: 'BankTransfer',
      );
      expect(body.containsKey('amount'), isFalse);
      expect(body['paymentType'], 'Deposit');
    });
  });
}
