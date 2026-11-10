import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'driver_shell.dart';
import 'register_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({
    super.key,
    required this.darkMode,
    required this.onDarkModeChanged,
  });
  final bool darkMode;
  final ValueChanged<bool> onDarkModeChanged;

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
    FocusScope.of(context).unfocus();
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
            darkMode: widget.darkMode,
            onDarkModeChanged: widget.onDarkModeChanged,
          ),
        ),
      );
    } catch (e) {
      if (mounted) {
        setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF2F5FB),
      body: LayoutBuilder(
        builder: (context, constraints) {
          final wide = constraints.maxWidth >= 900;
          if (!wide) return _mobileLayout();
          return SafeArea(
            child: Row(
              children: [
                Expanded(flex: 11, child: _heroPanel()),
                Expanded(
                  flex: 9,
                  child: Center(
                    child: SingleChildScrollView(
                      padding: const EdgeInsets.all(40),
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 520),
                        child: _loginCard(),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _mobileLayout() => Stack(
    fit: StackFit.expand,
    children: [
      Image.asset('assets/images/hero-car.jpg', fit: BoxFit.cover),
      const DecoratedBox(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [Color(0x77334E77), Color(0xE610203B)],
          ),
        ),
      ),
      SafeArea(
        child: SingleChildScrollView(
          padding: const EdgeInsets.fromLTRB(20, 20, 20, 24),
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 520),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _brand(light: true),
                  const SizedBox(height: 28),
                  const Text(
                    'Đồng hành cùng bạn trên mọi hành trình',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 25,
                      height: 1.15,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Những hành trình tuyệt vời bắt đầu từ sự tin cậy.',
                    style: TextStyle(color: Colors.white, fontSize: 14),
                  ),
                  const SizedBox(height: 22),
                  _loginCard(),
                ],
              ),
            ),
          ),
        ),
      ),
    ],
  );
  Widget _heroPanel() => Stack(
    fit: StackFit.expand,
    children: [
      Image.asset('assets/images/hero-car.jpg', fit: BoxFit.cover),
      const DecoratedBox(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [Color(0x44203355), Color(0xE610203B)],
          ),
        ),
      ),
      Padding(
        padding: const EdgeInsets.all(38),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _brand(light: true),
            const Spacer(),
            const Text(
              'Đồng hành cùng bạn trên\nmọi hành trình',
              style: TextStyle(
                color: Colors.white,
                fontSize: 42,
                height: 1.15,
                fontWeight: FontWeight.w800,
              ),
            ),
            const SizedBox(height: 14),
            const Text(
              'DriveX Driver · Chủ động mỗi chuyến đi.',
              style: TextStyle(color: Colors.white, fontSize: 17),
            ),
            const SizedBox(height: 28),
            Wrap(
              spacing: 10,
              runSpacing: 10,
              children: const [
                _HeroTag(icon: Icons.directions_car, text: 'Chuyến được giao'),
                _HeroTag(
                  icon: Icons.verified_user_outlined,
                  text: 'An toàn & tin cậy',
                ),
                _HeroTag(
                  icon: Icons.route_outlined,
                  text: 'Đồng hành mọi hành trình',
                ),
              ],
            ),
          ],
        ),
      ),
    ],
  );

  Widget _loginCard() => Container(
    padding: const EdgeInsets.all(30),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(24),
      boxShadow: const [
        BoxShadow(
          color: Color(0x14223755),
          blurRadius: 38,
          offset: Offset(0, 16),
        ),
      ],
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _brand(),
        const SizedBox(height: 26),
        const Text(
          'Chào mừng tài xế!',
          style: TextStyle(
            fontSize: 28,
            fontWeight: FontWeight.w800,
            color: Color(0xFF10213D),
          ),
        ),
        const SizedBox(height: 7),
        const Text(
          'Đăng nhập để xem và thực hiện chuyến được giao.',
          style: TextStyle(fontSize: 15, color: Color(0xFF60718D)),
        ),
        const SizedBox(height: 26),
        if (_error != null) ...[
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: const Color(0xFFFFF0EF),
              border: Border.all(color: const Color(0xFFF4C7C4)),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Icon(
                  Icons.error_outline,
                  color: Color(0xFFB42318),
                  size: 20,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    _error!,
                    style: const TextStyle(color: Color(0xFFB42318)),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
        ],
        const Text(
          'Email',
          style: TextStyle(
            fontWeight: FontWeight.w700,
            color: Color(0xFF263A58),
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: _email,
          keyboardType: TextInputType.emailAddress,
          textInputAction: TextInputAction.next,
          autocorrect: false,
          style: const TextStyle(
            color: Color(0xFF10213D),
            fontWeight: FontWeight.w600,
          ),
          decoration: _inputDecoration(
            hint: 'Nhập email tài xế',
            icon: Icons.email_outlined,
          ),
          onSubmitted: (_) => FocusScope.of(context).nextFocus(),
        ),
        const SizedBox(height: 18),
        const Text(
          'Mật khẩu',
          style: TextStyle(
            fontWeight: FontWeight.w700,
            color: Color(0xFF263A58),
          ),
        ),
        const SizedBox(height: 8),
        TextField(
          controller: _password,
          obscureText: !_showPassword,
          textInputAction: TextInputAction.done,
          style: const TextStyle(
            color: Color(0xFF10213D),
            fontWeight: FontWeight.w600,
          ),
          decoration: _inputDecoration(
            hint: 'Nhập mật khẩu',
            icon: Icons.lock_outline,
            suffix: IconButton(
              tooltip: _showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu',
              onPressed: () => setState(() => _showPassword = !_showPassword),
              icon: Icon(
                _showPassword
                    ? Icons.visibility_off_outlined
                    : Icons.visibility_outlined,
                color: const Color(0xFF60718D),
              ),
            ),
          ),
          onSubmitted: (_) => _loading ? null : _login(),
        ),
        const SizedBox(height: 24),
        SizedBox(
          height: 54,
          child: FilledButton(
            onPressed: _loading ? null : _login,
            style: FilledButton.styleFrom(
              backgroundColor: const Color(0xFF2864EA),
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(13),
              ),
            ),
            child: _loading
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: Colors.white,
                    ),
                  )
                : const Text(
                    'Đăng nhập',
                    style: TextStyle(fontSize: 16, fontWeight: FontWeight.w800),
                  ),
          ),
        ),
        const SizedBox(height: 8),
        Center(
          child: TextButton(
            key: const Key('open-register'),
            onPressed: _loading
                ? null
                : () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => RegisterScreen(
                        darkMode: widget.darkMode,
                        onDarkModeChanged: widget.onDarkModeChanged,
                      ),
                    ),
                  ),
            child: const Text('Chưa có tài khoản? Đăng ký tài xế'),
          ),
        ),
        const SizedBox(height: 10),
        const Text(
          'Tài khoản chỉ dành cho tài xế DriveX.',
          textAlign: TextAlign.center,
          style: TextStyle(color: Color(0xFF71819A), fontSize: 13),
        ),
      ],
    ),
  );

  InputDecoration _inputDecoration({
    required String hint,
    required IconData icon,
    Widget? suffix,
  }) => InputDecoration(
    hintText: hint,
    hintStyle: const TextStyle(
      color: Color(0xFF8290A6),
      fontWeight: FontWeight.w400,
    ),
    prefixIcon: Icon(icon, color: const Color(0xFF60718D)),
    suffixIcon: suffix,
    filled: true,
    fillColor: const Color(0xFFEAF1FD),
    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 17),
    enabledBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(14),
      borderSide: const BorderSide(color: Color(0xFFDCE5F3)),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: BorderRadius.circular(14),
      borderSide: const BorderSide(color: Color(0xFF2864EA), width: 1.5),
    ),
  );

  Widget _brand({bool light = false}) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Container(
        width: 44,
        height: 44,
        decoration: BoxDecoration(
          color: const Color(0xFF2864EA),
          borderRadius: BorderRadius.circular(13),
        ),
        child: const Icon(
          Icons.directions_car_filled_outlined,
          color: Colors.white,
          size: 24,
        ),
      ),
      const SizedBox(width: 11),
      Text(
        'DriveX',
        style: TextStyle(
          fontSize: 22,
          fontWeight: FontWeight.w800,
          color: light ? Colors.white : const Color(0xFF10213D),
        ),
      ),
    ],
  );
}

class _HeroTag extends StatelessWidget {
  const _HeroTag({required this.icon, required this.text});
  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 9),
    decoration: BoxDecoration(
      color: const Color(0x332F4B73),
      borderRadius: BorderRadius.circular(24),
      border: Border.all(color: const Color(0x667C98BE)),
    ),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, color: Colors.white, size: 16),
        const SizedBox(width: 7),
        Text(
          text,
          style: const TextStyle(
            color: Colors.white,
            fontWeight: FontWeight.w700,
            fontSize: 12,
          ),
        ),
      ],
    ),
  );
}
