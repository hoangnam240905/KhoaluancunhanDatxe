import 'package:driver_app/widgets/complete_trip_dialog.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  Future<String?> openDialog(
    WidgetTester tester, {
    required CompleteTripSubmit onSubmit,
  }) async {
    String? result;
    await tester.pumpWidget(
      MaterialApp(
        home: Builder(
          builder: (context) {
            return TextButton(
              onPressed: () async {
                result = await showDialog<String>(
                  context: context,
                  builder: (_) => CompleteTripDialog(onSubmit: onSubmit),
                );
              },
              child: const Text('open'),
            );
          },
        ),
      ),
    );
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    return result;
  }

  testWidgets('valid submit pops dialog and does not throw', (tester) async {
    var called = false;
    double? odo;
    double? fuel;
    String? exterior;
    String? technical;
    String? capturedNotes;
    await openDialog(
      tester,
      onSubmit:
          ({
            required odometerKm,
            required fuelLevel,
            exteriorCondition,
            technicalCondition,
            notes,
          }) async {
            called = true;
            odo = odometerKm;
            fuel = fuelLevel;
            exterior = exteriorCondition;
            technical = technicalCondition;
            capturedNotes = notes;
            return 'Đã hoàn thành chuyến.';
          },
    );

    await tester.enterText(find.byKey(const Key('complete-odometer')), '52300');
    await tester.enterText(find.byKey(const Key('complete-fuel')), '80');
    await tester.enterText(find.byKey(const Key('complete-exterior')), 'Tốt');
    await tester.enterText(find.byKey(const Key('complete-technical')), 'Tốt');
    await tester.enterText(find.byKey(const Key('complete-notes')), 'Test');
    await tester.tap(find.text('Hoàn thành'));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(called, isTrue);
    expect(odo, 52300);
    expect(fuel, 80);
    expect(exterior, 'Tốt');
    expect(technical, 'Tốt');
    expect(capturedNotes, 'Test');
    expect(find.text('Hoàn thành chuyến'), findsNothing);
  });

  testWidgets('empty odometer stays open and does not call API', (tester) async {
    var called = false;
    await openDialog(
      tester,
      onSubmit:
          ({
            required odometerKm,
            required fuelLevel,
            exteriorCondition,
            technicalCondition,
            notes,
          }) async {
            called = true;
            return 'ok';
          },
    );

    await tester.enterText(find.byKey(const Key('complete-fuel')), '80');
    await tester.tap(find.text('Hoàn thành'));
    await tester.pumpAndSettle();

    expect(called, isFalse);
    expect(find.text('Vui lòng nhập số km hợp lệ.'), findsOneWidget);
    expect(find.text('Hoàn thành chuyến'), findsOneWidget);
  });

  testWidgets('invalid fuel stays open and does not call API', (tester) async {
    var called = false;
    await openDialog(
      tester,
      onSubmit:
          ({
            required odometerKm,
            required fuelLevel,
            exteriorCondition,
            technicalCondition,
            notes,
          }) async {
            called = true;
            return 'ok';
          },
    );

    await tester.enterText(find.byKey(const Key('complete-odometer')), '52300');
    await tester.enterText(find.byKey(const Key('complete-fuel')), '180');
    await tester.tap(find.text('Hoàn thành'));
    await tester.pumpAndSettle();

    expect(called, isFalse);
    expect(find.text('Nhiên liệu phải từ 0 đến 100.'), findsOneWidget);
  });

  testWidgets('API 400 shows backend message and does not pop', (tester) async {
    await openDialog(
      tester,
      onSubmit:
          ({
            required odometerKm,
            required fuelLevel,
            exteriorCondition,
            technicalCondition,
            notes,
          }) async {
            throw Exception('Số km trả xe không được nhỏ hơn số km lúc giao xe.');
          },
    );

    await tester.enterText(find.byKey(const Key('complete-odometer')), '100');
    await tester.enterText(find.byKey(const Key('complete-fuel')), '80');
    await tester.tap(find.text('Hoàn thành'));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(
      find.text('Số km trả xe không được nhỏ hơn số km lúc giao xe.'),
      findsOneWidget,
    );
    expect(find.text('Hoàn thành chuyến'), findsOneWidget);
  });

  testWidgets('network failure shows error and does not crash', (tester) async {
    await openDialog(
      tester,
      onSubmit:
          ({
            required odometerKm,
            required fuelLevel,
            exteriorCondition,
            technicalCondition,
            notes,
          }) async {
            throw Exception('Loi mang');
          },
    );

    await tester.enterText(find.byKey(const Key('complete-odometer')), '52300');
    await tester.enterText(find.byKey(const Key('complete-fuel')), '80');
    await tester.tap(find.text('Hoàn thành'));
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.text('Loi mang'), findsOneWidget);
    expect(find.text('Hoàn thành chuyến'), findsOneWidget);
  });
}
