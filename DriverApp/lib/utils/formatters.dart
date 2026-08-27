import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../theme/app_theme.dart';

class Formatters {
  static final dateTime = DateFormat('dd/MM/yyyy HH:mm');

  static String dt(DateTime value) => dateTime.format(value.toLocal());

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

  static Color assignmentColor(String status) {
    switch (status) {
      case 'Completed':
        return AppColors.success;
      case 'InProgress':
        return AppColors.primary;
      case 'Accepted':
        return AppColors.accent;
      case 'Cancelled':
        return AppColors.danger;
      default:
        return AppColors.muted;
    }
  }
}
