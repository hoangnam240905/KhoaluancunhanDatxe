import 'package:flutter/material.dart';

import '../services/api_service.dart';
import 'driver_shell.dart';

/// Driver sign-up (plan1.md Phần D). The account works at once but stays "Chờ xác minh":
/// the driver is taken to "Bằng lái & giấy tờ" to declare the licence and send photos.
class RegisterScreen extends StatefulWidget {
  const RegisterScreen({
    super.key,
    required this.darkMode,
    required this.onDarkModeChanged,
  });

  final bool darkMode;
  final ValueChanged<bool> onDarkModeChanged;

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _api = ApiService();
  final _name = TextEditingController();
  final _email = TextEditingController();
  final _phone = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  bool _loading = false;
  bool _showPassword = false;
  String? _error;

  @override
  void dispose() {
    _name.dispose();
    _email.dispose();
    _phone.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _register() async {
    FocusScope.of(context).unfocus();
    if (_name.text.trim().isEmpty ||
        _email.text.trim().isEmpty ||
        _phone.text.trim().isEmpty ||
        _password.text.isEmpty) {
      setState(() => _error = 'Vui lòng nhập đủ họ tên, email, số điện thoại và mật khẩu.');
      return;
    }
    if (_password.text != _confirm.text) {
      setState(() => _error = 'Mật khẩu nhập lại không khớp.');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final auth = await _api.registerDriver(
        fullName: _name.text.trim(),
        email: _email.text.trim(),
        phone: _phone.text.trim(),
        password: _password.text,
        confirmPassword: _confirm.text,
      );
      if (!mounted) return;
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(
          builder: (_) => DriverShell(
            api: _api,
            userName: auth.fullName,
            darkMode: widget.darkMode,
            onDarkModeChanged: widget.onDarkModeChanged,
            openVerificationOnStart: true,
          ),
        ),
        (_) => false,
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
    final muted = TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant);
    return Scaffold(
      appBar: AppBar(title: const Text('Đăng ký tài xế')),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          Text(
            'Sau khi đăng ký, bạn nhập thông tin bằng lái và gửi ảnh bằng lái, CCCD. '
            'Điều phối viên duyệt ảnh, rồi đối chiếu bằng gốc tại văn phòng trước khi bạn được phân công chuyến.',
            style: muted,
          ),
          const SizedBox(height: 16),
          TextField(
            key: const Key('register-name'),
            controller: _name,
            textCapitalization: TextCapitalization.words,
            decoration: const InputDecoration(labelText: 'Họ và tên'),
          ),
          const SizedBox(height: 10),
          TextField(
            key: const Key('register-email'),
            controller: _email,
            keyboardType: TextInputType.emailAddress,
            decoration: const InputDecoration(labelText: 'Email'),
          ),
          const SizedBox(height: 10),
          TextField(
            key: const Key('register-phone'),
            controller: _phone,
            keyboardType: TextInputType.phone,
            decoration: const InputDecoration(labelText: 'Số điện thoại'),
          ),
          const SizedBox(height: 10),
          TextField(
            key: const Key('register-password'),
            controller: _password,
            obscureText: !_showPassword,
            decoration: InputDecoration(
              labelText: 'Mật khẩu',
              helperText: 'Ít nhất 8 ký tự, 1 chữ hoa và 1 ký tự đặc biệt.',
              suffixIcon: IconButton(
                icon: Icon(_showPassword ? Icons.visibility_off : Icons.visibility),
                onPressed: () => setState(() => _showPassword = !_showPassword),
              ),
            ),
          ),
          const SizedBox(height: 10),
          TextField(
            key: const Key('register-confirm'),
            controller: _confirm,
            obscureText: !_showPassword,
            decoration: const InputDecoration(labelText: 'Nhập lại mật khẩu'),
            onSubmitted: (_) => _loading ? null : _register(),
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          ],
          const SizedBox(height: 20),
          SizedBox(
            height: 50,
            child: FilledButton(
              onPressed: _loading ? null : _register,
              child: _loading
                  ? const SizedBox(
                      width: 22,
                      height: 22,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Đăng ký'),
            ),
          ),
        ],
      ),
    );
  }
}
