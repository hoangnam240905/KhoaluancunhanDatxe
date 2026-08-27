import 'package:customer_app/models/models.dart';
import 'package:customer_app/utils/formatters.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Payment.fromJson', () {
    test('legacy paymentType null does not crash', () {
      final payment = Payment.fromJson({
        'paymentId': 1,
        'bookingId': 2,
        'paymentType': null,
        'amount': 2240000,
        'method': 'BankTransfer',
        'status': 'Paid',
        'transactionRef': 'TXN-20260825-001',
        'paidAt': '2026-08-24T15:30:00',
        'createdAt': '2026-08-23T13:01:20.4287286',
      });
      expect(payment.paymentType, isNull);
      expect(payment.amount, 2240000);
      expect(payment.blocksNewDeposit, isFalse);
      expect(Formatters.paymentTypeLabel(payment.paymentType), 'Thanh toán cũ');
      expect(Formatters.paymentStatusLabel(payment.status), 'Đã thanh toán');
      expect(Formatters.paymentMethodLabel(payment.method), 'Chuyển khoản');
    });

    test('deposit pending blocks a second deposit', () {
      final payment = Payment.fromJson({
        'paymentId': 2,
        'bookingId': 22,
        'paymentType': 'Deposit',
        'amount': 800000,
        'method': 'BankTransfer',
        'status': 'Pending',
        'transactionRef': 'TEST-P351-001',
        'paidAt': null,
        'createdAt': '2026-08-27T13:14:00.5073502Z',
      });
      expect(payment.isDeposit, isTrue);
      expect(payment.blocksNewDeposit, isTrue);
      expect(Formatters.paymentTypeLabel(payment.paymentType), 'Tiền cọc');
      expect(Formatters.paymentStatusLabel(payment.status), 'Chờ thanh toán');
    });
  });

  group('Payment.depositCreateBody', () {
    test('never includes amount', () {
      final body = Payment.depositCreateBody(
        bookingId: 22,
        method: 'BankTransfer',
        transactionRef: 'ABC',
      );
      expect(body.containsKey('amount'), isFalse);
      expect(body['bookingId'], 22);
      expect(body['paymentType'], 'Deposit');
      expect(body['method'], 'BankTransfer');
      expect(body['transactionRef'], 'ABC');
    });

    test('omits empty transactionRef', () {
      final body = Payment.depositCreateBody(
        bookingId: 22,
        method: 'Cash',
        transactionRef: '  ',
      );
      expect(body.containsKey('transactionRef'), isFalse);
      expect(body.containsKey('amount'), isFalse);
    });
  });

  group('Booking quotedDepositAmount', () {
    Map<String, dynamic> bookingJson({Object? deposit}) => {
      'bookingId': 20,
      'customerId': 3,
      'customerName': 'Le Van Khach',
      'vehicleTypeId': 1,
      'vehicleTypeName': '4 cho - Sedan',
      'pickupAddress': 'A',
      'dropoffAddress': 'B',
      'startDate': '2026-10-01T08:00:00',
      'endDate': '2026-10-01T18:00:00',
      'estimatedDistance': 50,
      'totalAmount': 1400000,
      'status': 'Pending',
      'rentalMode': 'WithDriver',
      'quotedDepositAmount': deposit,
    };

    test('null snapshot stays null', () {
      final booking = Booking.fromJson(bookingJson(deposit: null));
      expect(booking.quotedDepositAmount, isNull);
    });

    test('reads snapshot amount', () {
      final booking = Booking.fromJson(bookingJson(deposit: 800000));
      expect(booking.quotedDepositAmount, 800000);
    });
  });

  group('payment labels', () {
    test('methods', () {
      expect(Formatters.paymentMethodLabel('Cash'), 'Tiền mặt');
      expect(Formatters.paymentMethodLabel('BankTransfer'), 'Chuyển khoản');
      expect(Formatters.paymentMethodLabel('MoMo'), 'MoMo');
      expect(Formatters.paymentMethodLabel('VNPay'), 'VNPay');
    });
  });
}
