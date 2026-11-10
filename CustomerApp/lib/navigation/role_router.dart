import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../screens/customer_shell.dart';

/// Điều hướng sau đăng nhập/đăng ký. App này chỉ dành cho khách hàng:
/// tài xế dùng DriverApp, điều phối / admin dùng web.
class RoleRouter {
  static Widget homeFor(ApiService api, AuthResponse auth) =>
      CustomerShell(api: api, userName: auth.fullName);

  static void goHome(BuildContext context, ApiService api, AuthResponse auth) {
    final roleError = mobileRoleError(auth.role);
    if (roleError != null) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(roleError)),
      );
      return;
    }
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => homeFor(api, auth)),
      (_) => false,
    );
  }

  static String? mobileRoleError(String role) {
    if (role == 'Customer') return null;
    if (role == 'Driver') {
      return 'Tài khoản tài xế dùng ứng dụng DriveX Tài xế (DriverApp), không dùng app khách hàng.';
    }
    return 'Tài khoản $role chỉ đăng nhập trên Web. App này dành cho khách hàng.';
  }
}
