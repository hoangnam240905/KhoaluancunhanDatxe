import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'screens/driver_shell.dart';
import 'screens/login_screen.dart';
import 'services/api_service.dart';
import 'theme/app_theme.dart';

void main() => runApp(const DriverApp());

class DriverApp extends StatefulWidget {
  const DriverApp({super.key});

  @override
  State<DriverApp> createState() => _DriverAppState();
}

class _DriverAppState extends State<DriverApp> {
  static const _darkModeKey = 'driver_app_dark_mode';
  bool _darkMode = false;
  bool _themeLoaded = false;

  @override
  void initState() {
    super.initState();
    _loadTheme();
  }

  Future<void> _loadTheme() async {
    final prefs = await SharedPreferences.getInstance();
    if (mounted) {
      setState(() {
        _darkMode = prefs.getBool(_darkModeKey) ?? false;
        _themeLoaded = true;
      });
    }
  }

  Future<void> _setDarkMode(bool value) async {
    setState(() => _darkMode = value);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_darkModeKey, value);
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
    title: 'DriveX Tài xế',
    debugShowCheckedModeBanner: false,
    theme: AppTheme.light,
    darkTheme: AppTheme.dark,
    themeMode: _darkMode ? ThemeMode.dark : ThemeMode.light,
    home: _themeLoaded
        ? SplashScreen(darkMode: _darkMode, onDarkModeChanged: _setDarkMode)
        : const _ThemeLoadingScreen(),
  );
}

class SplashScreen extends StatefulWidget {
  const SplashScreen({
    super.key,
    required this.darkMode,
    required this.onDarkModeChanged,
  });
  final bool darkMode;
  final ValueChanged<bool> onDarkModeChanged;

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
    final valid =
        auth != null &&
        auth.role == 'Driver' &&
        auth.expiresAt.isAfter(DateTime.now());
    if (valid) {
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
          builder: (_) => DriverShell(
            api: _api,
            userName: auth.fullName,
            darkMode: widget.darkMode,
            onDarkModeChanged: widget.onDarkModeChanged,
          ),
        ),
      );
      return;
    }
    if (auth != null) await _api.authService.logout();
    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(
        builder: (_) => LoginScreen(
          darkMode: widget.darkMode,
          onDarkModeChanged: widget.onDarkModeChanged,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: Theme.of(context).scaffoldBackgroundColor,
    body: Center(
      child: CircularProgressIndicator(
        color: Theme.of(context).colorScheme.primary,
      ),
    ),
  );
}

class _ThemeLoadingScreen extends StatelessWidget {
  const _ThemeLoadingScreen();

  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: Theme.of(context).scaffoldBackgroundColor,
    body: Center(
      child: CircularProgressIndicator(
        color: Theme.of(context).colorScheme.primary,
      ),
    ),
  );
}
