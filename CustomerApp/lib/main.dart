import 'package:flutter/material.dart';
import 'navigation/role_router.dart';
import 'screens/customer_shell.dart';
import 'services/api_service.dart';
import 'theme/app_theme.dart';

void main() {
  runApp(const CarRentalApp());
}

class CarRentalApp extends StatelessWidget {
  const CarRentalApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Car Rental',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      home: const SplashScreen(),
    );
  }
}

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  final _api = ApiService();

  @override
  void initState() {
    super.initState();
    _checkAuth();
  }

  Future<void> _checkAuth() async {
    final auth = await _api.authService.getAuth();
    if (!mounted) return;

    final valid = auth != null && auth.expiresAt.isAfter(DateTime.now());
    if (valid && (auth.role == 'Customer' || auth.role == 'Driver')) {
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => RoleRouter.homeFor(_api, auth)),
      );
      return;
    }

    if (valid) {
      await _api.authService.logout();
    }

    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => CustomerShell(api: _api)),
    );
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: AppColors.primaryDark,
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.directions_car_filled, size: 56, color: Colors.white),
            SizedBox(height: 16),
            Text(
              'Car Rental',
              style: TextStyle(
                color: Colors.white,
                fontSize: 24,
                fontWeight: FontWeight.w800,
              ),
            ),
            SizedBox(height: 24),
            CircularProgressIndicator(color: Colors.white),
          ],
        ),
      ),
    );
  }
}
