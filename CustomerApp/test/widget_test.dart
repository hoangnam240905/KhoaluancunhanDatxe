import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:customer_app/main.dart';
import 'package:customer_app/screens/customer_shell.dart';
import 'package:customer_app/services/api_service.dart';
import 'package:customer_app/theme/app_theme.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:customer_app/models/models.dart';
import 'package:customer_app/screens/booking_detail_screen.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  testWidgets('App starts at splash', (WidgetTester tester) async {
    SharedPreferences.setMockInitialValues({});
    await tester.pumpWidget(const CarRentalApp());
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
    expect(find.text('Car Rental'), findsOneWidget);
  });
  testWidgets('CustomerShell renders all tabs without errors', (WidgetTester tester) async {
    final api = ApiService();
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.light,
      home: CustomerShell(api: api),
    ));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));

    // Tap bookings tab
    await tester.tap(find.text('Đơn thuê'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));

    // Tap account tab
    await tester.tap(find.text('Tài khoản'));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  });

  testWidgets('BookingDetailScreen renders without errors', (WidgetTester tester) async {
    final api = ApiService();
    final booking = Booking(
      bookingId: 4,
      customerName: 'Lê Văn Khách',
      vehicleTypeId: 1,
      vehicleTypeName: '4 chỗ - Sedan',
      pickupAddress: 'TP.HCM',
      dropoffAddress: 'Vũng Tàu',
      startDate: DateTime.now().add(const Duration(days: 1)),
      endDate: DateTime.now().add(const Duration(days: 2)),
      totalAmount: 1200000,
      status: 'Pending',
      rentalMode: 'WithDriver',
      quotedDepositAmount: 600000,
    );
    await tester.pumpWidget(MaterialApp(
      theme: AppTheme.light,
      home: BookingDetailScreen(api: api, booking: booking),
    ));
    await tester.pump();
    await tester.pump(const Duration(milliseconds: 500));
  });
}
