import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/app_widgets.dart';
import 'customer_shell.dart';
import 'login_screen.dart';
import 'register_screen.dart';

class AccountScreen extends StatefulWidget {
  final ApiService api;
  final bool isLoggedIn;

  const AccountScreen({super.key, required this.api, required this.isLoggedIn});

  @override
  State<AccountScreen> createState() => _AccountScreenState();
}

class _AccountScreenState extends State<AccountScreen> {
  UserProfile? _profile;
  AuthResponse? _session;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void didUpdateWidget(covariant AccountScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.isLoggedIn != widget.isLoggedIn) {
      _load();
    }
  }

  Future<void> _load() async {
    if (!widget.isLoggedIn) {
      setState(() {
        _loading = false;
        _profile = null;
        _session = null;
        _error = null;
      });
      return;
    }
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

  Future<void> _logout() async {
    await widget.api.authService.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => CustomerShell(api: widget.api)),
      (_) => false,
    );
  }

  String get _roleLabel {
    final role = _profile?.role ?? _session?.role ?? '';
    switch (role) {
      case 'Customer':
        return 'Khách hàng';
      case 'Driver':
        return 'Tài xế';
      default:
        return role.isEmpty ? '—' : role;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Tài khoản')),
      body: !widget.isLoggedIn
          ? Center(
              child: AppEmptyState(
                icon: Icons.person_outline,
                title: 'Chưa đăng nhập',
                subtitle:
                    'Đăng nhập để xem thông tin tài khoản và quản lý đơn thuê.',
                actionLabel: 'Đăng nhập',
                onAction: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const LoginScreen()),
                ),
              ),
            )
          : _loading
          ? const Center(child: AppLoading(message: 'Đang tải hồ sơ...'))
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  AppCard(
                    child: Column(
                      children: [
                        CircleAvatar(
                          radius: 36,
                          backgroundColor: const Color(0xFFDBEAFE),
                          child: Text(
                            () {
                              final name =
                                  _profile?.fullName ??
                                  _session?.fullName ??
                                  '?';
                              return name.isEmpty
                                  ? '?'
                                  : name.substring(0, 1).toUpperCase();
                            }(),
                            style: const TextStyle(
                              fontSize: 28,
                              fontWeight: FontWeight.w800,
                              color: AppColors.primary,
                            ),
                          ),
                        ),
                        const SizedBox(height: 12),
                        Text(
                          _profile?.fullName ?? _session?.fullName ?? '',
                          style: const TextStyle(
                            fontSize: 20,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _roleLabel,
                          style: const TextStyle(
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
                        'Không tải được hồ sơ từ máy chủ: $_error\nĐang hiển thị thông tin phiên đăng nhập.',
                        style: const TextStyle(color: AppColors.muted),
                      ),
                    ),
                  ],
                  const SizedBox(height: 12),
                  AppCard(
                    child: Column(
                      children: [
                        _infoRow(
                          Icons.email_outlined,
                          'Email',
                          _profile?.email ?? _session?.email ?? '—',
                        ),
                        const Divider(height: 24),
                        _infoRow(
                          Icons.phone_outlined,
                          'Số điện thoại',
                          (_profile?.phone == null || _profile!.phone!.isEmpty)
                              ? '—'
                              : _profile!.phone!,
                        ),
                        const Divider(height: 24),
                        _infoRow(Icons.badge_outlined, 'Vai trò', _roleLabel),
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
            ),
      bottomNavigationBar: !widget.isLoggedIn
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                child: Row(
                  children: [
                    Expanded(
                      child: OutlinedButton(
                        onPressed: () => Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => const RegisterScreen(),
                          ),
                        ),
                        child: const Text('Đăng ký'),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: FilledButton(
                        onPressed: () => Navigator.push(
                          context,
                          MaterialPageRoute(
                            builder: (_) => const LoginScreen(),
                          ),
                        ),
                        child: const Text('Đăng nhập'),
                      ),
                    ),
                  ],
                ),
              ),
            )
          : null,
    );
  }

  Widget _infoRow(IconData icon, String label, String value) {
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
