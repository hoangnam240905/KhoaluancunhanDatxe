import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../navigation/role_router.dart';
import '../services/api_service.dart';

class VerifyEmailScreen extends StatefulWidget {
  final String email;
  const VerifyEmailScreen({super.key, required this.email});

  @override
  State<VerifyEmailScreen> createState() => _VerifyEmailScreenState();
}

class _VerifyEmailScreenState extends State<VerifyEmailScreen> {
  late final TextEditingController _email;
  final _otp = TextEditingController();
  final _api = ApiService();
  bool _loading = false;
  String? _error;
  String? _info;

  @override
  void initState() {
    super.initState();
    _email = TextEditingController(text: widget.email);
    _info = 'Đã gửi mã OTP đến ${widget.email}. Mã hết hạn sau 5 phút.';
  }

  @override
  void dispose() {
    _email.dispose();
    _otp.dispose();
    super.dispose();
  }

  Future<void> _verify() async {
    final otp = _otp.text.trim();
    if (otp.isEmpty) {
      setState(() => _error = 'Vui lòng nhập mã OTP.');
      return;
    }
    if (otp.length != 6 || int.tryParse(otp) == null) {
      setState(() => _error = 'Mã OTP gồm 6 chữ số.');
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final auth = await _api.verifyEmail(
        email: widget.email,
        otp: _otp.text.trim(),
      );
      final roleError = RoleRouter.mobileRoleError(auth.role);
      if (roleError != null) {
        await _api.authService.logout();
        throw Exception(roleError);
      }
      if (!mounted) return;
      RoleRouter.goHome(context, _api, auth);
    } catch (e) {
      setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _resend() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final message = await _api.resendVerificationOtp(widget.email);
      setState(() => _info = message);
    } catch (e) {
      setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Xác minh email')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_info != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Text(_info!, style: const TextStyle(color: Color(0xFF2563EB))),
              ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Text(_error!, style: const TextStyle(color: Color(0xFFDC2626))),
              ),
            TextField(
              controller: _email,
              readOnly: true,
              enableInteractiveSelection: true,
              decoration: const InputDecoration(
                labelText: 'Email',
                border: OutlineInputBorder(),
                filled: true,
                suffixIcon: Icon(Icons.lock_outline),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _otp,
              keyboardType: TextInputType.number,
              maxLength: 6,
              inputFormatters: [
                FilteringTextInputFormatter.digitsOnly,
                LengthLimitingTextInputFormatter(6),
              ],
              decoration: const InputDecoration(
                labelText: 'Mã OTP',
                hintText: '000000',
                border: OutlineInputBorder(),
                counterText: '',
              ),
            ),
            const SizedBox(height: 12),
            FilledButton(
              onPressed: _loading ? null : _verify,
              child: Text(_loading ? 'Đang xử lý...' : 'Xác minh'),
            ),
            TextButton(
              onPressed: _loading ? null : _resend,
              child: const Text('Gửi lại mã OTP'),
            ),
          ],
        ),
      ),
    );
  }
}
