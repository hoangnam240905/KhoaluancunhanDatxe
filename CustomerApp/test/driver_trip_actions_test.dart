import 'package:customer_app/utils/driver_trip_actions.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('Assigned shows accept only, not start', () {
    final action = DriverTripActions.forAssignmentStatus('Assigned');
    expect(action, DriverTripAction.accept);
    expect(action!.label, 'Nhận chuyến');
    expect(action, isNot(DriverTripAction.start));
  });

  test('Accepted shows start only', () {
    final action = DriverTripActions.forAssignmentStatus('Accepted');
    expect(action, DriverTripAction.start);
    expect(action!.label, 'Bắt đầu chuyến');
  });

  test('InProgress shows complete only', () {
    final action = DriverTripActions.forAssignmentStatus('InProgress');
    expect(action, DriverTripAction.complete);
    expect(action!.label, 'Hoàn thành chuyến');
  });

  test('Completed has no start', () {
    expect(DriverTripActions.forAssignmentStatus('Completed'), isNull);
  });

  test('Cancelled has no start', () {
    expect(DriverTripActions.forAssignmentStatus('Cancelled'), isNull);
  });

  test('booking Assigned with assignment Accepted uses assignment status', () {
    const bookingStatus = 'Assigned';
    const assignmentStatus = 'Accepted';
    expect(bookingStatus, 'Assigned');
    expect(
      DriverTripActions.forAssignmentStatus(assignmentStatus),
      DriverTripAction.start,
    );
    expect(
      DriverTripActions.forAssignmentStatus(bookingStatus),
      isNot(DriverTripAction.start),
    );
  });
}
