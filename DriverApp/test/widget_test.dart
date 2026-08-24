import 'package:flutter_test/flutter_test.dart';
import 'package:driver_app/main.dart';

void main() {
  testWidgets('App loads login screen', (WidgetTester tester) async {
    await tester.pumpWidget(const DriverApp());
    await tester.pumpAndSettle();
    expect(find.text('Dang nhap'), findsOneWidget);
  });
}
