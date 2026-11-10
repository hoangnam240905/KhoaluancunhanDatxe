import 'package:driver_app/models/models.dart';
import 'package:driver_app/widgets/dispute_dialog.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('DriverVerification parses status, documents and missing sides', () {
    final v = DriverVerification.fromJson({
      'licenseNumber': '790111222333',
      'licenseClass': 'B',
      'licenseExpiry': '2033-05-10',
      'verificationStatus': 'Pending',
      'photoStatus': 'Pending',
      'missingDocuments': ['IdCardBack'],
      'documents': [
        {'userDocumentId': 3, 'documentType': 'LicenseFront', 'status': 'Rejected', 'rejectReason': 'Mờ'},
      ],
    });
    expect(v.isVerified, isFalse);
    expect(v.missingDocuments, ['IdCardBack']);
    expect(v.documentOf('LicenseFront')!.rejectReason, 'Mờ');
    expect(v.licenseExpiry, DateTime(2033, 5, 10));
  });

  test('VehicleInspection and InspectionDispute read Phần A fields', () {
    final pre = VehicleInspection.fromJson({
      'inspectionType': 'PreHandover',
      'actualAt': '2026-11-10T01:00:00',
      'createdByName': 'Trần Thị Điều Phối',
      'verificationStatus': null,
    });
    expect(pre.isPreHandover, isTrue);
    expect(pre.createdByName, 'Trần Thị Điều Phối');
    final d = InspectionDispute.fromJson({'inspectionDisputeId': 1, 'note': 'Trầy cửa', 'status': 'Open', 'createdAt': '2026-11-10T01:00:00'});
    expect(d.isOpen, isTrue);
  });

  testWidgets('Dispute dialog requires a note', (tester) async {
    String? result = 'unset';
    await tester.pumpWidget(MaterialApp(
      home: Builder(
        builder: (context) => TextButton(
          onPressed: () async => result = await showDialog<String>(context: context, builder: (_) => const DisputeDialog()),
          child: const Text('open'),
        ),
      ),
    ));
    await tester.tap(find.text('open'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Gửi báo sai lệch'));
    await tester.pump();
    expect(find.text('Vui lòng ghi rõ điểm sai lệch.'), findsOneWidget);
    await tester.enterText(find.byKey(const Key('dispute-note')), 'Lốp trước mòn');
    await tester.tap(find.text('Gửi báo sai lệch'));
    await tester.pumpAndSettle();
    expect(result, 'Lốp trước mòn');
  });
}
