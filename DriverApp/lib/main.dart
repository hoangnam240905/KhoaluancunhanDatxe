import 'package:flutter/material.dart';
import 'screens/login_screen.dart';
import 'screens/trips_screen.dart';
import 'services/api_service.dart';

void main() {
  runApp(const DriverApp());
}

class DriverApp extends StatelessWidget {
  const DriverApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Car Rental - Tài xế',
      theme: ThemeData(colorScheme: ColorScheme.fromSeed(seedColor: Colors.orange), useMaterial3: true),
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
    if (auth != null && auth.role == 'Driver' && auth.expiresAt.isAfter(DateTime.now())) {
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(builder: (_) => TripsScreen(api: _api, userName: auth.fullName)),
      );
    } else {
      Navigator.of(context).pushReplacement(MaterialPageRoute(builder: (_) => const LoginScreen()));
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(body: Center(child: CircularProgressIndicator()));
  }
}
