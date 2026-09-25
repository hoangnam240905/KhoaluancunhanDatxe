import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/app_widgets.dart';
import 'login_screen.dart';

class AccountScreen extends StatefulWidget {
  final ApiService api;
  final String userName;

  const AccountScreen({super.key, required this.api, required this.userName});

  @override
  State<AccountScreen> createState() => _AccountScreenState();
}

class _AccountScreenState extends State<AccountScreen> {
  UserProfile? _profile;
  AuthResponse? _session;
  bool _loading = true;
  String? _error;
  final _oldPassword = TextEditingController();
  final _newPassword = TextEditingController();
  String? _passwordMessage;
  bool _changingPassword = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _session = await widget.api.authService.getAuth();
      _profile = await widget.api.getMe();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  void dispose() {
    _oldPassword.dispose();
    _newPassword.dispose();
    super.dispose();
  }

  bool get _newPasswordOk {
    final p = _newPassword.text;
    return p.length >= 8 &&
        RegExp(r'[A-Z]').hasMatch(p) &&
        RegExp(r'[^a-zA-Z0-9]').hasMatch(p);
  }

  Future<void> _changePassword() async {
    if (_oldPassword.text.isEmpty) {
      setState(() => _passwordMessage = 'Vui lòng nhập mật khẩu cũ.');
      return;
    }
    if (!_newPasswordOk) {
      setState(
        () => _passwordMessage =
            'Mật khẩu chưa đủ mạnh (8 ký tự, chữ hoa, ký tự đặc biệt).',
      );
      return;
    }
    setState(() {
      _changingPassword = true;
      _passwordMessage = null;
    });
    try {
      await widget.api.changePassword(
        oldPassword: _oldPassword.text,
        newPassword: _newPassword.text,
      );
      if (!mounted) return;
      _oldPassword.clear();
      _newPassword.clear();
      setState(() => _passwordMessage = 'Đã đổi mật khẩu.');
    } catch (e) {
      if (!mounted) return;
      setState(
        () => _passwordMessage = e.toString().replaceFirst('Exception: ', ''),
      );
    } finally {
      if (mounted) setState(() => _changingPassword = false);
    }
  }

  Future<void> _logout() async {
    await widget.api.disconnectRealtime();
    await widget.api.authService.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }

  @override
  Widget build(BuildContext context) {
    final name = _profile?.fullName ?? _session?.fullName ?? widget.userName;
    final email = _profile?.email ?? _session?.email ?? '—';
    final phone = _profile?.phone;
    return Scaffold(
      appBar: AppBar(title: const Text('Tài khoản')),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải hồ sơ...'))
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                AppCard(
                  child: Column(
                    children: [
                      CircleAvatar(
                        radius: 36,
                        backgroundColor: const Color(0xFFFFEDD5),
                        child: Text(
                          name.isEmpty
                              ? 'T'
                              : name.substring(0, 1).toUpperCase(),
                          style: const TextStyle(
                            fontSize: 28,
                            fontWeight: FontWeight.w800,
                            color: AppColors.primary,
                          ),
                        ),
                      ),
                      const SizedBox(height: 12),
                      Text(
                        name,
                        style: const TextStyle(
                          fontSize: 20,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const Text(
                        'Tài xế',
                        style: TextStyle(
                          color: AppColors.primary,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ],
                  ),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 12),
                  AppCard(
                    child: Text(
                      'Không tải được hồ sơ máy chủ: $_error',
                      style: const TextStyle(color: AppColors.muted),
                    ),
                  ),
                ],
                const SizedBox(height: 12),
                AppCard(
                  child: Column(
                    children: [
                      _row(Icons.email_outlined, 'Email', email),
                      const Divider(height: 24),
                      _row(
                        Icons.phone_outlined,
                        'Số điện thoại',
                        (phone == null || phone.isEmpty) ? '—' : phone,
                      ),
                      const Divider(height: 24),
                      _row(Icons.badge_outlined, 'Vai trò', 'Driver'),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                AppCard(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'Đổi mật khẩu',
                        style: TextStyle(fontWeight: FontWeight.w800),
                      ),
                      const SizedBox(height: 12),
                      TextField(
                        controller: _oldPassword,
                        obscureText: true,
                        decoration: const InputDecoration(
                          labelText: 'Mật khẩu cũ',
                        ),
                      ),
                      const SizedBox(height: 8),
                      TextField(
                        controller: _newPassword,
                        obscureText: true,
                        decoration: const InputDecoration(
                          labelText: 'Mật khẩu mới',
                        ),
                      ),
                      if (_passwordMessage != null) ...[
                        const SizedBox(height: 8),
                        Text(_passwordMessage!),
                      ],
                      const SizedBox(height: 12),
                      FilledButton(
                        onPressed: _changingPassword ? null : _changePassword,
                        child: Text(
                          _changingPassword ? 'Đang lưu...' : 'Đổi mật khẩu',
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),
                OutlinedButton.icon(
                  onPressed: _logout,
                  icon: const Icon(Icons.logout),
                  label: const Text('Đăng xuất'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.danger,
                    side: const BorderSide(color: AppColors.danger),
                    minimumSize: const Size.fromHeight(48),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(14),
                    ),
                  ),
                ),
              ],
            ),
    );
  }

  Widget _row(IconData icon, String label, String value) {
    return Row(
      children: [
        Icon(icon, color: AppColors.primary),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(color: AppColors.muted, fontSize: 12),
              ),
              Text(value, style: const TextStyle(fontWeight: FontWeight.w700)),
            ],
          ),
        ),
      ],
    );
  }
}
