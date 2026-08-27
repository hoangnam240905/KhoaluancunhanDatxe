import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../screens/customer_shell.dart';
import '../screens/driver_trips_screen.dart';

/// Điều hướng sau đăng nhập/đăng ký theo vai trò (giống PortalWeb).
class RoleRouter {
  static Widget homeFor(ApiService api, AuthResponse auth) {
    switch (auth.role) {
      case 'Customer':
        return CustomerShell(api: api, userName: auth.fullName);
      case 'Driver':
        return DriverTripsScreen(api: api, userName: auth.fullName);
      default:
        return CustomerShell(api: api);
    }
  }

  static void goHome(BuildContext context, ApiService api, AuthResponse auth) {
    if (auth.role != 'Customer' && auth.role != 'Driver') {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Vai trò ${auth.role} chỉ dùng trên Web Portal.'),
        ),
      );
      return;
    }
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => homeFor(api, auth)),
      (_) => false,
    );
  }

  static String? mobileRoleError(String role) {
    if (role == 'Customer' || role == 'Driver') return null;
    return 'Tài khoản $role chỉ đăng nhập trên Web. App chỉ hỗ trợ Khách hàng / Tài xế.';
  }
}
