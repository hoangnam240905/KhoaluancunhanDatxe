import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'verify_email_screen.dart';

class RegisterScreen extends StatefulWidget {
  const RegisterScreen({super.key});

  @override
  State<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends State<RegisterScreen> {
  final _fullName = TextEditingController();
  final _email = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  final _phone = TextEditingController();
  final _api = ApiService();
  bool _loading = false;
  bool _showPassword = false;
  bool _showConfirm = false;
  bool _agreedTerms = false;
  String? _error;

  bool get _lenOk => _password.text.length >= 8;
  bool get _mixOk =>
      RegExp(r'[A-Z]').hasMatch(_password.text) &&
      RegExp(r'[a-z]').hasMatch(_password.text) &&
      RegExp(r'[0-9]').hasMatch(_password.text);
  bool get _specialOk => RegExp(r'[^a-zA-Z0-9]').hasMatch(_password.text);
  bool get _passwordOk => _lenOk && _mixOk && _specialOk;

  @override
  void dispose() {
    _fullName.dispose();
    _email.dispose();
    _password.dispose();
    _confirm.dispose();
    _phone.dispose();
    super.dispose();
  }

  bool get _emailOk => RegExp(
        r'^[^@\s]+@[^@\s]+\.[^@\s]+$',
        caseSensitive: false,
      ).hasMatch(_email.text.trim());
  bool get _phoneOk => RegExp(
        r'^0\d{9}$',
      ).hasMatch(_phone.text.trim().replaceAll(RegExp(r'[\s.\-]'), ''));

  Future<void> _register() async {
    if (_fullName.text.trim().isEmpty) {
      setState(() => _error = 'Vui lòng nhập họ và tên.');
      return;
    }
    if (!_emailOk) {
      setState(() => _error = 'Vui lòng nhập địa chỉ email hợp lệ.');
      return;
    }
    if (!_phoneOk) {
      setState(
        () => _error =
            'Vui lòng nhập số điện thoại hợp lệ (10 chữ số, bắt đầu bằng 0).',
      );
      return;
    }
    if (!_passwordOk) {
      setState(
        () => _error =
            'Mật khẩu chưa đủ mạnh (8 ký tự, chữ hoa, chữ thường, số, ký tự đặc biệt).',
      );
      return;
    }
    if (_confirm.text != _password.text) {
      setState(() => _error = 'Xác nhận mật khẩu không khớp.');
      return;
    }
    if (!_agreedTerms) {
      setState(() => _error = 'Vui lòng đồng ý với Điều khoản dịch vụ.');
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final pending = await _api.register(
        email: _email.text.trim(),
        password: _password.text,
        fullName: _fullName.text.trim(),
        phone: _phone.text.trim(),
        confirmPassword: _confirm.text,
      );
      if (!mounted) return;
      Navigator.pushReplacement(
        context,
        MaterialPageRoute(
          builder: (_) => VerifyEmailScreen(
            email: pending.email,
            devOtp: pending.devOtp,
            message: pending.message,
          ),
        ),
      );
    } catch (e) {
      setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Widget _rule(bool ok, String text) {
    return Row(
      children: [
        Icon(
          ok ? Icons.check_circle_rounded : Icons.circle_outlined,
          size: 14,
          color: ok ? const Color(0xFF16A34A) : const Color(0xFF94A3B8),
        ),
        const SizedBox(width: 5),
        Text(
          text,
          style: TextStyle(
            fontSize: 12,
            color: ok ? const Color(0xFF16A34A) : const Color(0xFF64748B),
          ),
        ),
      ],
    );
  }

  InputDecoration _inputDeco(String hint) => InputDecoration(
        hintText: hint,
        hintStyle: const TextStyle(color: Color(0xFFCBD5E1)),
        filled: true,
        fillColor: Colors.white,
        contentPadding:
            const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(10),
          borderSide: const BorderSide(color: Color(0xFF2563EB), width: 1.5),
        ),
      );

  Widget _label(String text) => Text(
        text,
        style: const TextStyle(
          fontSize: 14,
          fontWeight: FontWeight.w500,
          color: Color(0xFF374151),
        ),
      );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.white,
      body: SafeArea(
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // ── top bar ─────────────────────────────────────────
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: [
                        Container(
                          width: 36,
                          height: 36,
                          decoration: BoxDecoration(
                            color: const Color(0xFF2563EB),
                            borderRadius: BorderRadius.circular(8),
                          ),
                          child: const Icon(
                            Icons.directions_car,
                            color: Colors.white,
                            size: 20,
                          ),
                        ),
                        const SizedBox(width: 8),
                        const Text(
                          'DriveX',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                            color: Color(0xFF0F172A),
                          ),
                        ),
                      ],
                    ),
                    const Row(
                      children: [
                        Icon(Icons.language,
                            size: 16, color: Color(0xFF64748B)),
                        SizedBox(width: 4),
                        Text(
                          'Tiếng Việt',
                          style: TextStyle(
                              fontSize: 13, color: Color(0xFF64748B)),
                        ),
                      ],
                    ),
                  ],
                ),
              ),

              // ── heading ─────────────────────────────────────────
              const Padding(
                padding: EdgeInsets.fromLTRB(20, 28, 20, 0),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Tạo tài khoản mới',
                      style: TextStyle(
                        fontSize: 26,
                        fontWeight: FontWeight.w800,
                        color: Color(0xFF0F172A),
                      ),
                    ),
                    SizedBox(height: 4),
                    Text(
                      'Tham gia DriveX để bắt đầu hành trình của bạn.',
                      style: TextStyle(fontSize: 14, color: Color(0xFF64748B)),
                    ),
                  ],
                ),
              ),

              // ── OTP banner ───────────────────────────────────────
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 16, 20, 0),
                child: Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: const Color(0xFFEFF6FF),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: const Color(0xFFBFDBFE)),
                  ),
                  child: const Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(Icons.info_outline_rounded,
                          size: 16, color: Color(0xFF2563EB)),
                      SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'Sau khi đăng ký, bạn sẽ nhận mã OTP để xác thực email trước khi đăng nhập.',
                          style: TextStyle(
                            fontSize: 12.5,
                            color: Color(0xFF1D4ED8),
                            height: 1.45,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),

              // ── form ────────────────────────────────────────────
              Padding(
                padding: const EdgeInsets.fromLTRB(20, 20, 20, 36),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    // error
                    if (_error != null) ...[
                      Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 14, vertical: 10),
                        decoration: BoxDecoration(
                          color: const Color(0xFFFEF2F2),
                          borderRadius: BorderRadius.circular(10),
                          border: Border.all(color: const Color(0xFFFCA5A5)),
                        ),
                        child: Text(
                          _error!,
                          style: const TextStyle(
                              color: Color(0xFFDC2626), fontSize: 13),
                        ),
                      ),
                      const SizedBox(height: 14),
                    ],

                    // Họ và tên
                    _label('Họ và tên'),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _fullName,
                      textCapitalization: TextCapitalization.words,
                      style: const TextStyle(fontSize: 15),
                      decoration: _inputDeco('Nguyễn Văn A'),
                    ),
                    const SizedBox(height: 14),

                    // Email
                    _label('Địa chỉ Email'),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      style: const TextStyle(fontSize: 15),
                      decoration: _inputDeco('abc@gmail.com'),
                    ),
                    const SizedBox(height: 14),

                    // Số điện thoại
                    _label('Số điện thoại'),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _phone,
                      keyboardType: TextInputType.phone,
                      style: const TextStyle(fontSize: 15),
                      decoration: _inputDeco('09xxxxxxxx'),
                    ),
                    const SizedBox(height: 14),

                    // Mật khẩu
                    _label('Mật khẩu'),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _password,
                      obscureText: !_showPassword,
                      onChanged: (_) => setState(() {}),
                      style: const TextStyle(fontSize: 15),
                      decoration: _inputDeco('Nhập mật khẩu').copyWith(
                        suffixIcon: IconButton(
                          icon: Icon(
                            _showPassword
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: const Color(0xFF94A3B8),
                            size: 20,
                          ),
                          onPressed: () =>
                              setState(() => _showPassword = !_showPassword),
                        ),
                      ),
                    ),
                    const SizedBox(height: 8),
                    _rule(_lenOk, 'Ít nhất 8 ký tự'),
                    const SizedBox(height: 3),
                    _rule(_mixOk, 'Bao gồm chữ hoa, chữ thường và số'),
                    const SizedBox(height: 3),
                    _rule(_specialOk, 'Bao gồm ít nhất 1 ký tự đặc biệt'),
                    const SizedBox(height: 14),

                    // Xác nhận mật khẩu
                    _label('Xác nhận mật khẩu'),
                    const SizedBox(height: 6),
                    TextField(
                      controller: _confirm,
                      obscureText: !_showConfirm,
                      style: const TextStyle(fontSize: 15),
                      decoration: _inputDeco('Nhập lại mật khẩu').copyWith(
                        suffixIcon: IconButton(
                          icon: Icon(
                            _showConfirm
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: const Color(0xFF94A3B8),
                            size: 20,
                          ),
                          onPressed: () =>
                              setState(() => _showConfirm = !_showConfirm),
                        ),
                      ),
                    ),
                    const SizedBox(height: 16),

                    // Checkbox điều khoản
                    GestureDetector(
                      onTap: () =>
                          setState(() => _agreedTerms = !_agreedTerms),
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          SizedBox(
                            width: 20,
                            height: 20,
                            child: Checkbox(
                              value: _agreedTerms,
                              activeColor: const Color(0xFF2563EB),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(4),
                              ),
                              onChanged: (v) =>
                                  setState(() => _agreedTerms = v ?? false),
                            ),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: RichText(
                              text: const TextSpan(
                                style: TextStyle(
                                    fontSize: 13, color: Color(0xFF374151)),
                                children: [
                                  TextSpan(text: 'Tôi đồng ý với '),
                                  TextSpan(
                                    text: 'Điều khoản dịch vụ',
                                    style: TextStyle(
                                      color: Color(0xFF2563EB),
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  TextSpan(text: ' và '),
                                  TextSpan(
                                    text: 'Chính sách bảo mật',
                                    style: TextStyle(
                                      color: Color(0xFF2563EB),
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                  TextSpan(text: '.'),
                                ],
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 22),

                    // Nút tạo tài khoản
                    SizedBox(
                      height: 50,
                      child: FilledButton(
                        style: FilledButton.styleFrom(
                          backgroundColor: const Color(0xFF2563EB),
                          disabledBackgroundColor: const Color(0x992563EB),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(10),
                          ),
                          textStyle: const TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        onPressed: _loading ? null : _register,
                        child: _loading
                            ? const SizedBox(
                                height: 22,
                                width: 22,
                                child: CircularProgressIndicator(
                                  strokeWidth: 2,
                                  color: Colors.white,
                                ),
                              )
                            : const Text('Tạo tài khoản'),
                      ),
                    ),
                    const SizedBox(height: 16),

                    // Link đăng nhập
                    Center(
                      child: GestureDetector(
                        onTap: _loading ? null : () => Navigator.pop(context),
                        child: RichText(
                          text: const TextSpan(
                            style: TextStyle(
                                fontSize: 14, color: Color(0xFF64748B)),
                            children: [
                              TextSpan(text: 'Đã có tài khoản? '),
                              TextSpan(
                                text: 'Đăng nhập ngay',
                                style: TextStyle(
                                  color: Color(0xFF2563EB),
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
