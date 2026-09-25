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
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Xác nhận đăng xuất'),
        content: const Text('Bạn có chắc chắn muốn đăng xuất khỏi tài khoản không?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Hủy'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
            child: const Text('Đăng xuất'),
          ),
        ],
      ),
    );

    if (confirm != true || !mounted) return;

    await widget.api.disconnectRealtime();
    await widget.api.authService.logout();
    if (!mounted) return;
    Navigator.of(context).pushAndRemoveUntil(
      MaterialPageRoute(builder: (_) => CustomerShell(api: widget.api)),
      (_) => false,
    );
  }

  void _openChangePasswordSheet() {
    final oldPassword = TextEditingController();
    final newPassword = TextEditingController();
    final confirmPassword = TextEditingController();
    String? message;
    bool busy = false;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setLocal) => Padding(
          padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.of(ctx).viewInsets.bottom + 24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('Đổi mật khẩu', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.pop(ctx),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              TextField(
                controller: oldPassword,
                obscureText: true,
                decoration: const InputDecoration(
                  labelText: 'Mật khẩu hiện tại',
                  prefixIcon: Icon(Icons.lock_outline),
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: newPassword,
                obscureText: true,
                decoration: const InputDecoration(
                  labelText: 'Mật khẩu mới (tối thiểu 8 ký tự)',
                  prefixIcon: Icon(Icons.lock_reset),
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: confirmPassword,
                obscureText: true,
                decoration: const InputDecoration(
                  labelText: 'Xác nhận mật khẩu mới',
                  prefixIcon: Icon(Icons.check_circle_outline),
                ),
              ),
              if (message != null) ...[
                const SizedBox(height: 10),
                Text(message!, style: const TextStyle(color: AppColors.danger, fontSize: 13)),
              ],
              const SizedBox(height: 18),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: busy
                      ? null
                      : () async {
                          final oldP = oldPassword.text;
                          final newP = newPassword.text;
                          final confirmP = confirmPassword.text;
                          if (oldP.isEmpty) {
                            setLocal(() => message = 'Vui lòng nhập mật khẩu hiện tại.');
                            return;
                          }
                          if (newP.length < 8) {
                            setLocal(() => message = 'Mật khẩu mới phải có ít nhất 8 ký tự.');
                            return;
                          }
                          if (newP != confirmP) {
                            setLocal(() => message = 'Mật khẩu xác nhận không khớp.');
                            return;
                          }
                          setLocal(() {
                            busy = true;
                            message = null;
                          });
                          try {
                            await widget.api.changePassword(
                              oldPassword: oldP,
                              newPassword: newP,
                            );
                            if (ctx.mounted) {
                              Navigator.pop(ctx);
                            }
                            if (mounted) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(content: Text('Đổi mật khẩu thành công!')),
                              );
                            }
                          } catch (e) {
                            if (ctx.mounted) {
                              setLocal(() {
                                busy = false;
                                message = e.toString().replaceFirst('Exception: ', '');
                              });
                            }
                          }
                        },
                  child: Text(busy ? 'Đang xử lý...' : 'Lưu mật khẩu mới'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String get _roleLabel {
    final role = _profile?.role ?? _session?.role ?? '';
    switch (role) {
      case 'Customer':
        return 'Khách hàng thân thiết';
      case 'Driver':
        return 'Tài xế';
      default:
        return role.isEmpty ? '—' : role;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Hồ sơ & Tài khoản')),
      body: !widget.isLoggedIn
          ? Center(
              child: AppEmptyState(
                icon: Icons.person_outline,
                title: 'Chưa đăng nhập',
                subtitle: 'Đăng nhập để xem thông tin cá nhân và quản lý tài khoản.',
                actionLabel: 'Đăng nhập ngay',
                onAction: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => const LoginScreen()),
                ),
              ),
            )
          : _loading
              ? const Center(child: AppLoading(message: 'Đang tải hồ sơ...'))
              : _error != null && _profile == null
                  ? Center(child: AppErrorState(message: _error!, onRetry: _load))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView(
                    padding: const EdgeInsets.all(16),
                    children: [
                      // Profile Header Card
                      Container(
                        padding: const EdgeInsets.all(20),
                        decoration: BoxDecoration(
                          gradient: const LinearGradient(
                            colors: [Color(0xFF0F172A), Color(0xFF1E3A8A)],
                            begin: Alignment.topLeft,
                            end: Alignment.bottomRight,
                          ),
                          borderRadius: BorderRadius.circular(20),
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withValues(alpha: 0.1),
                              blurRadius: 12,
                              offset: const Offset(0, 6),
                            ),
                          ],
                        ),
                        child: Row(
                          children: [
                            CircleAvatar(
                              radius: 36,
                              backgroundColor: Colors.white.withValues(alpha: 0.2),
                              child: Text(
                                () {
                                  final name = _profile?.fullName ?? _session?.fullName ?? '?';
                                  return name.isEmpty ? '?' : name.substring(0, 1).toUpperCase();
                                }(),
                                style: const TextStyle(
                                  fontSize: 28,
                                  fontWeight: FontWeight.w900,
                                  color: Colors.white,
                                ),
                              ),
                            ),
                            const SizedBox(width: 16),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    _profile?.fullName ?? _session?.fullName ?? 'Khách hàng',
                                    style: const TextStyle(
                                      fontSize: 18,
                                      fontWeight: FontWeight.w900,
                                      color: Colors.white,
                                    ),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    _profile?.email ?? _session?.email ?? '',
                                    style: TextStyle(
                                      fontSize: 13,
                                      color: Colors.white.withValues(alpha: 0.8),
                                    ),
                                  ),
                                  const SizedBox(height: 8),
                                  Container(
                                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                                    decoration: BoxDecoration(
                                      color: AppColors.primary,
                                      borderRadius: BorderRadius.circular(20),
                                    ),
                                    child: Text(
                                      _roleLabel,
                                      style: const TextStyle(
                                        color: Colors.white,
                                        fontWeight: FontWeight.w700,
                                        fontSize: 11,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 20),

                      // Personal Information Section
                      Container(
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(color: AppColors.border),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const SectionHeader(
                              icon: Icons.badge_outlined,
                              title: 'Thông tin cá nhân',
                              subtitle: 'Dùng để xác thực khi lập hợp đồng thuê xe',
                            ),
                            const SizedBox(height: 16),
                            _infoRow(
                              Icons.tag_rounded,
                              'Mã thành viên',
                              '#${_profile?.userId ?? _session?.userId ?? '—'}',
                            ),
                            const Divider(height: 20, color: AppColors.border),
                            _infoRow(
                              Icons.email_outlined,
                              'Email',
                              _profile?.email ?? _session?.email ?? '—',
                            ),
                            const Divider(height: 20, color: AppColors.border),
                            _infoRow(
                              Icons.phone_outlined,
                              'Số điện thoại',
                              (_profile?.phone == null || _profile!.phone!.isEmpty)
                                  ? 'Chưa cập nhật'
                                  : _profile!.phone!,
                            ),
                            const Divider(height: 20, color: AppColors.border),
                            _infoRow(
                              Icons.verified_user_outlined,
                              'Trạng thái tài khoản',
                              'Đã kích hoạt & Xác thực',
                            ),
                            const Divider(height: 20, color: AppColors.border),
                            _infoRow(
                              Icons.card_membership_outlined,
                              'Cấp độ hội viên',
                              _roleLabel,
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 20),

                      // Account Settings & Security
                      Container(
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(color: AppColors.border),
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const SectionHeader(
                              icon: Icons.security_outlined,
                              title: 'Bảo mật & Cài đặt',
                              subtitle: 'Quản lý mật khẩu và quyền riêng tư',
                            ),
                            const SizedBox(height: 14),
                            ListTile(
                              dense: true,
                              contentPadding: EdgeInsets.zero,
                              leading: Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: const Color(0xFFEFF6FF),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: const Icon(Icons.lock_reset, color: AppColors.primary, size: 20),
                              ),
                              title: const Text('Đổi mật khẩu', style: TextStyle(fontWeight: FontWeight.w700)),
                              subtitle: const Text('Cập nhật mật khẩu định kỳ để an toàn hơn', style: TextStyle(fontSize: 12)),
                              trailing: const Icon(Icons.chevron_right, color: AppColors.muted),
                              onTap: _openChangePasswordSheet,
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 24),

                      // Logout button
                      OutlinedButton.icon(
                        onPressed: _logout,
                        icon: const Icon(Icons.logout),
                        label: const Text('Đăng xuất'),
                        style: OutlinedButton.styleFrom(
                          foregroundColor: AppColors.danger,
                          side: const BorderSide(color: AppColors.danger),
                          minimumSize: const Size.fromHeight(50),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(14),
                          ),
                        ),
                      ),
                      const SizedBox(height: 32),
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
                          MaterialPageRoute(builder: (_) => const RegisterScreen()),
                        ),
                        child: const Text('Đăng ký'),
                      ),
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: FilledButton(
                        onPressed: () => Navigator.push(
                          context,
                          MaterialPageRoute(builder: (_) => const LoginScreen()),
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
        Icon(icon, size: 20, color: AppColors.primary),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(label, style: const TextStyle(color: AppColors.muted, fontSize: 12)),
              const SizedBox(height: 2),
              Text(value, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14)),
            ],
          ),
        ),
      ],
    );
  }
}
