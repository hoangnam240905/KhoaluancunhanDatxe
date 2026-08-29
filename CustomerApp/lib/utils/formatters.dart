import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../theme/app_theme.dart';

class Formatters {
  static final money = NumberFormat('#,###', 'vi_VN');
  static final dateTime = DateFormat('dd/MM/yyyy HH:mm');

  static String vnd(double value) => '${money.format(value)} VND';

  static String dt(DateTime value) => dateTime.format(value.toLocal());

  static String rentalModeLabel(String? mode) {
    switch (mode) {
      case 'SelfDrive':
        return 'Tự lái';
      case 'WithDriver':
      default:
        return 'Có tài xế';
    }
  }

  static bool isSelfDrive(String? mode) => mode == 'SelfDrive';

  static String statusLabel(String status) {
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

  static String paymentMethodLabel(String method) {
    switch (method) {
      case 'Cash':
        return 'Tiền mặt';
      case 'BankTransfer':
        return 'Chuyển khoản';
      case 'MoMo':
        return 'MoMo';
      case 'VNPay':
        return 'VNPay';
      default:
        return method;
    }
  }

  static String paymentStatusLabel(String status) {
    switch (status) {
      case 'Pending':
        return 'Chờ thanh toán';
      case 'Paid':
        return 'Đã thanh toán';
      case 'Failed':
        return 'Thất bại';
      case 'Refunded':
        return 'Đã hoàn tiền';
      default:
        return status;
    }
  }

  static Color paymentStatusColor(String status) {
    switch (status) {
      case 'Paid':
        return AppColors.success;
      case 'Failed':
        return AppColors.danger;
      case 'Refunded':
        return AppColors.warning;
      case 'Pending':
        return AppColors.warning;
      default:
        return AppColors.muted;
    }
  }

  static String feeTypeLabel(String type) {
    switch (type) {
      case 'LateFee':
        return 'Phí trễ hạn';
      case 'ExtraKm':
        return 'Km vượt';
      case 'Fuel':
        return 'Nhiên liệu';
      case 'Damage':
        return 'Hư hỏng';
      case 'Other':
        return 'Khác';
      default:
        return type;
    }
  }

  static String inspectionTypeLabel(String type) {
    switch (type) {
      case 'Handover':
        return 'Giao xe';
      case 'Return':
        return 'Trả xe';
      default:
        return type;
    }
  }

  static String paymentTypeLabel(String? type) {
    switch (type) {
      case 'Deposit':
        return 'Tiền cọc';
      case 'Balance':
        return 'Phần còn lại';
      case 'Refund':
        return 'Hoàn tiền';
      default:
        return 'Thanh toán cũ';
    }
  }

  static Color statusColor(String status) {
    switch (status) {
      case 'Completed':
        return AppColors.success;
      case 'Cancelled':
        return AppColors.danger;
      case 'InProgress':
        return AppColors.primary;
      case 'Assigned':
      case 'Confirmed':
        return AppColors.warning;
      default:
        return AppColors.muted;
    }
  }
}
