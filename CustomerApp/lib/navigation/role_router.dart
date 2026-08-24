import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../screens/driver_trips_screen.dart';
import '../screens/home_screen.dart';

/// Dieu huong sau dang nhap/dang ky theo role (giong PortalWeb).
class RoleRouter {
  static Widget homeFor(ApiService api, AuthResponse auth) {
    switch (auth.role) {
      case 'Customer':
        return HomeScreen(api: api, userName: auth.fullName);
      case 'Driver':
        return DriverTripsScreen(api: api, userName: auth.fullName);
      default:
        return HomeScreen(api: api);
    }
  }

  static void goHome(BuildContext context, ApiService api, AuthResponse auth) {
    if (auth.role != 'Customer' && auth.role != 'Driver') {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Role ${auth.role} chi dung tren Web Portal.')),
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
    return 'Tai khoan $role chi dang nhap tren Web. App chi ho tro Khach hang / Tai xe.';
  }
}
