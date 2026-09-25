/// Maps TripAssignment.status (not Booking.status) to the single P0.4 driver action.
class DriverTripActions {
  static const acceptLabel = 'Nhận chuyến';
  static const startLabel = 'Bắt đầu chuyến';
  static const completeLabel = 'Hoàn thành chuyến';

  static DriverTripAction? forAssignmentStatus(String? assignmentStatus) {
    switch (assignmentStatus) {
      case 'Assigned':
        return DriverTripAction.accept;
      case 'Accepted':
        return DriverTripAction.start;
      case 'InProgress':
        return DriverTripAction.complete;
      default:
        return null;
    }
  }
}

enum DriverTripAction { accept, start, complete }

extension DriverTripActionLabel on DriverTripAction {
  String get label => switch (this) {
        DriverTripAction.accept => DriverTripActions.acceptLabel,
        DriverTripAction.start => DriverTripActions.startLabel,
        DriverTripAction.complete => DriverTripActions.completeLabel,
      };
}
