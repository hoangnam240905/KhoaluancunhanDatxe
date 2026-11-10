import 'package:driver_app/models/models.dart';
import 'package:driver_app/screens/forgot_password_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('DriverNotification parses UTC time and booking link', () {
    final n = DriverNotification.fromJson({
      'notificationId': 7,
      'title': 'Chuyến #12 đổi sang xe 51A-12345',
      'message': 'Xe mới: Toyota Vios.',
      'type': 'driver',
      'isRead': false,
      'createdAt': '2026-11-10T08:00:00',
      'bookingId': 12,
    });
    expect(n.bookingId, 12);
    expect(n.isRead, isFalse);
    expect(n.createdAt.isUtc, isTrue);
    expect(n.createdAt.hour, 8);
  });

  test('DriverNotification without booking still parses', () {
    final n = DriverNotification.fromJson({
      'notificationId': 1,
      'title': 'Bằng lái đã được xác minh',
      'isRead': true,
      'createdAt': '2026-11-10T08:00:00Z',
    });
    expect(n.bookingId, isNull);
    expect(n.message, '');
  });

  testWidgets('Forgot password asks for a valid email before calling the API', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: ForgotPasswordScreen()));
    await tester.tap(find.text('Gửi mã OTP'));
    await tester.pump();
    expect(find.text('Vui lòng nhập email hợp lệ.'), findsOneWidget);
  });

  testWidgets('Reset password checks OTP length and matching passwords', (tester) async {
    await tester.pumpWidget(const MaterialApp(home: ResetPasswordScreen(email: 'driver2@carrental.vn')));
    await tester.tap(find.widgetWithText(FilledButton, 'Đặt lại mật khẩu'));
    await tester.pump();
    expect(find.text('Mã OTP gồm 6 chữ số.'), findsOneWidget);

    await tester.enterText(find.byType(TextField).at(0), '123456');
    await tester.enterText(find.byType(TextField).at(1), 'Matkhau@1');
    await tester.enterText(find.byType(TextField).at(2), 'Khac@1234');
    await tester.tap(find.widgetWithText(FilledButton, 'Đặt lại mật khẩu'));
    await tester.pump();
    expect(find.text('Xác nhận mật khẩu không khớp.'), findsOneWidget);
  });
}
