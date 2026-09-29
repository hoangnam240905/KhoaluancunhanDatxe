import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../theme/app_theme.dart';

class Formatters {
  static final dateTime = DateFormat('dd/MM/yyyy HH:mm');

  static String dt(DateTime value) => dateTime.format(value.toLocal());

  static String rentalDt(DateTime value) {
    final wall = DateTime(
      value.year,
      value.month,
      value.day,
      value.hour,
      value.minute,
    );
    return dateTime.format(wall);
  }

  static String bookingStatus(String status) {
    switch (status) {
      case 'Pending':
        return 'Chờ xác nhận';
      case 'Confirmed':
        return 'Đã xác nhận';
      case 'Assigned':
        return 'Đã phân công';
      case 'InProgress':
        return 'Đang thực hiện';
      case 'Completed':
        return 'Hoàn thành';
      case 'Cancelled':
        return 'Đã hủy';
      default:
        return status;
    }
  }

  static String assignmentStatus(String status) {
    switch (status) {
      case 'Assigned':
        return 'Chờ nhận';
      case 'Accepted':
        return 'Đã nhận';
      case 'InProgress':
        return 'Đang chạy';
      case 'Completed':
        return 'Hoàn thành';
      case 'Cancelled':
        return 'Đã hủy';
      default:
        return status;
    }
  }

  static String driverStatus(String status) {
    switch (status) {
      case 'Available':
        return 'Sẵn sàng';
      case 'Busy':
        return 'Bận';
      case 'Offline':
        return 'Offline';
      default:
        return status;
    }
  }

  static Color assignmentColor(BuildContext context, String status) {
    final palette = AppPalette.of(context);
    switch (status) {
      case 'Completed':
        return palette.success;
      case 'InProgress':
        return palette.primary;
      case 'Accepted':
        return palette.warning;
      case 'Cancelled':
        return palette.danger;
      default:
        return palette.muted;
    }
  }
}
