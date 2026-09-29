import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'screens/driver_shell.dart';
import 'screens/login_screen.dart';
import 'services/api_service.dart';
import 'theme/app_theme.dart';

void main() {
  runApp(const DriverApp());
}

class DriverApp extends StatefulWidget {
  const DriverApp({super.key});

  @override
  State<DriverApp> createState() => _DriverAppState();
}

class _DriverAppState extends State<DriverApp> {
  static const _themePreferenceKey = 'driver_app_theme_mode';
  ThemeMode _themeMode = ThemeMode.dark;

  @override
  void initState() {
    super.initState();
    _loadThemeMode();
  }

  Future<void> _loadThemeMode() async {
    final preferences = await SharedPreferences.getInstance();
    final saved = preferences.getString(_themePreferenceKey);
    if (!mounted || saved == null) return;
    setState(() {
      _themeMode = saved == 'light' ? ThemeMode.light : ThemeMode.dark;
    });
  }

  Future<void> _setThemeMode(ThemeMode mode) async {
    setState(() => _themeMode = mode);
    final preferences = await SharedPreferences.getInstance();
    await preferences.setString(
      _themePreferenceKey,
      mode == ThemeMode.light ? 'light' : 'dark',
    );
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'DriveX Tài xế',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.light,
      darkTheme: AppTheme.dark,
      themeMode: _themeMode,
      home: SplashScreen(onThemeModeChanged: _setThemeMode),
    );
  }
}

class SplashScreen extends StatefulWidget {
  final ValueChanged<ThemeMode> onThemeModeChanged;

  const SplashScreen({super.key, required this.onThemeModeChanged});

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
            onThemeModeChanged: widget.onThemeModeChanged,
          ),
        ),
      );
      return;
    }
    if (auth != null) {
      await _api.authService.logout();
    }
    if (!mounted) return;
    Navigator.of(
      context,
    ).pushReplacement(
      MaterialPageRoute(
        builder: (_) => LoginScreen(onThemeModeChanged: widget.onThemeModeChanged),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    return Scaffold(
      backgroundColor: palette.surface,
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.local_taxi, size: 56, color: palette.primary),
            SizedBox(height: 16),
            Text(
              'DriverApp',
              style: TextStyle(
                color: palette.text,
                fontSize: 24,
                fontWeight: FontWeight.w800,
              ),
            ),
            SizedBox(height: 24),
            CircularProgressIndicator(color: palette.primary),
          ],
        ),
      ),
    );
  }
}
