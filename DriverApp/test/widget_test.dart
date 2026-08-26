import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:driver_app/main.dart';

void main() {
  testWidgets('App starts', (WidgetTester tester) async {
    await tester.pumpWidget(const DriverApp());
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });
}
