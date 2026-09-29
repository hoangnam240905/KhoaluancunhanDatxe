import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import 'driver_shell.dart';

class LoginScreen extends StatefulWidget {
  final ValueChanged<ThemeMode> onThemeModeChanged;

  const LoginScreen({super.key, required this.onThemeModeChanged});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _api = ApiService();
  bool _loading = false;
  bool _showPassword = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _login() async {
    if (_email.text.trim().isEmpty || _password.text.isEmpty) {
      setState(() => _error = 'Vui lòng nhập email và mật khẩu.');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final auth = await _api.login(_email.text.trim(), _password.text);
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute(
          builder: (_) => DriverShell(
            api: _api,
            userName: auth.fullName,
            onThemeModeChanged: widget.onThemeModeChanged,
          ),
        ),
      );
    } catch (e) {
      setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final palette = AppPalette.of(context);
    final colorScheme = Theme.of(context).colorScheme;
    return Scaffold(
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            padding: EdgeInsets.symmetric(
              horizontal: 24,
              vertical: constraints.maxHeight < 700 ? 20 : 32,
            ),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 440),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Container(
                          width: 48,
                          height: 48,
                          decoration: BoxDecoration(
                            color: colorScheme.primary,
                            borderRadius: BorderRadius.circular(15),
                          ),
                          child: Icon(
                            Icons.directions_car_filled_rounded,
                            color: colorScheme.onPrimary,
                            size: 25,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Text(
                          'DriveX',
                          style: TextStyle(
                            color: palette.text,
                            fontSize: 21,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.5,
                          ),
                        ),
                        const Spacer(),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 11,
                            vertical: 7,
                          ),
                          decoration: BoxDecoration(
                            color: colorScheme.primaryContainer.withValues(
                              alpha: 0.55,
                            ),
                            borderRadius: BorderRadius.circular(999),
                          ),
                          child: Text(
                            'TÀI XẾ',
                            style: TextStyle(
                              color: colorScheme.onPrimaryContainer,
                              fontSize: 11,
                              fontWeight: FontWeight.w800,
                              letterSpacing: 0.6,
                            ),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 24),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(22),
                      child: SizedBox(
                        height: constraints.maxHeight < 700 ? 156 : 184,
                        child: Stack(
                          fit: StackFit.expand,
                          children: [
                            Image.asset(
                              'assets/images/driver_login_hero.jpg',
                              fit: BoxFit.cover,
                              semanticLabel: 'Xe DriveX trên đường',
                            ),
                            const DecoratedBox(
                              decoration: BoxDecoration(
                                gradient: LinearGradient(
                                  begin: Alignment.bottomLeft,
                                  end: Alignment.topRight,
                                  colors: [
                                    Color(0xCC10213D),
                                    Color(0x55263B5C),
                                    Color(0x10263B5C),
                                  ],
                                ),
                              ),
                            ),
                            Padding(
                              padding: const EdgeInsets.all(20),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Spacer(),
                                  const Text(
                                    'DRIVEX  ·  TÀI XẾ',
                                    style: TextStyle(
                                      color: Colors.white70,
                                      fontSize: 10,
                                      fontWeight: FontWeight.w800,
                                      letterSpacing: 1.2,
                                    ),
                                  ),
                                  const SizedBox(height: 7),
                                  const Text(
                                    'Đồng hành cùng bạn\ntrên mọi hành trình',
                                    style: TextStyle(
                                      color: Colors.white,
                                      fontSize: 20,
                                      height: 1.18,
                                      fontWeight: FontWeight.w800,
                                      letterSpacing: -0.35,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: 25),
                    Text(
                      'Chào mừng trở lại!',
                      style: TextStyle(
                        color: palette.text,
                        fontSize: 29,
                        height: 1.15,
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.8,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Đăng nhập để tiếp tục hành trình cùng DriveX.',
                      style: TextStyle(
                        color: palette.muted,
                        fontSize: 15,
                        height: 1.45,
                      ),
                    ),
                    const SizedBox(height: 30),
                    if (_error != null) ...[
                      Container(
                        padding: const EdgeInsets.all(13),
                        decoration: BoxDecoration(
                          color: colorScheme.errorContainer,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Icon(
                              Icons.error_outline_rounded,
                              size: 19,
                              color: colorScheme.onErrorContainer,
                            ),
                            const SizedBox(width: 9),
                            Expanded(
                              child: Text(
                                _error!,
                                style: TextStyle(
                                  color: colorScheme.onErrorContainer,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 18),
                    ],
                    Text(
                      'Email',
                      style: TextStyle(
                        color: palette.text,
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 8),
                    TextField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      textInputAction: TextInputAction.next,
                      autofillHints: const [AutofillHints.username],
                      decoration: const InputDecoration(
                        hintText: 'Nhập email của bạn',
                        prefixIcon: Icon(Icons.email_outlined),
                      ),
                    ),
                    const SizedBox(height: 20),
                    Text(
                      'Mật khẩu',
                      style: TextStyle(
                        color: palette.text,
                        fontSize: 13,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 8),
                    TextField(
                      controller: _password,
                      obscureText: !_showPassword,
                      textInputAction: TextInputAction.done,
                      autofillHints: const [AutofillHints.password],
                      onSubmitted: (_) => _loading ? null : _login(),
                      decoration: InputDecoration(
                        hintText: 'Nhập mật khẩu',
                        prefixIcon: const Icon(Icons.lock_outline_rounded),
                        suffixIcon: IconButton(
                          tooltip: _showPassword
                              ? 'Ẩn mật khẩu'
                              : 'Hiện mật khẩu',
                          icon: Icon(
                            _showPassword
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                          ),
                          onPressed: () => setState(
                            () => _showPassword = !_showPassword,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(height: 26),
                    SizedBox(
                      height: 54,
                      child: FilledButton(
                        onPressed: _loading ? null : _login,
                        child: _loading
                            ? SizedBox(
                                height: 22,
                                width: 22,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: colorScheme.onPrimary,
                                ),
                              )
                            : const Text('Đăng nhập'),
                      ),
                    ),
                    const SizedBox(height: 22),
                    Row(
                      children: [
                        Expanded(
                          child: Divider(color: palette.border, height: 1),
                        ),
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 12),
                          child: Text(
                            'DÀNH CHO TÀI XẾ',
                            style: TextStyle(
                              color: palette.muted,
                              fontSize: 10,
                              fontWeight: FontWeight.w700,
                              letterSpacing: 0.8,
                            ),
                          ),
                        ),
                        Expanded(
                          child: Divider(color: palette.border, height: 1),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    Text(
                      'Sử dụng tài khoản tài xế được cấp để xem lịch và cập nhật báo cáo chuyến đi.',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: palette.muted,
                        fontSize: 12,
                        height: 1.5,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
