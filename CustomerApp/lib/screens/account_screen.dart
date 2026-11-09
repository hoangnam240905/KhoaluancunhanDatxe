import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../widgets/app_widgets.dart';
import 'login_screen.dart';
import 'register_screen.dart';
import 'notifications_screen.dart';

enum _ProfileTab {
  info('Thông tin cá nhân'),
  security('Bảo mật'),
  notice('Thông báo'),
  activity('Hoạt động tài khoản');

  final String label;
  const _ProfileTab(this.label);
}

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
  _ProfileTab _currentTab = _ProfileTab.info;

  // Notification toggles matching web DriveX
  bool _noticeBooking = false;
  bool _noticePayment = false;
  bool _noticeContract = false;
  bool _noticeTrip = false;

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
        content: const Text(
          'Bạn có chắc chắn muốn đăng xuất khỏi tài khoản không?',
        ),
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
      MaterialPageRoute(builder: (_) => const LoginScreen()),
      (_) => false,
    );
  }

  void _openChangePasswordSheet() {
    final oldPassword = TextEditingController();
    final newPassword = TextEditingController();
    final confirmPassword = TextEditingController();
    bool obscureOld = true;
    bool obscureNew = true;
    bool obscureConfirm = true;
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
        builder: (ctx, setLocal) {
          final newText = newPassword.text;
          final lengthOk = newText.length >= 8;
          final upperOk = RegExp(r'[A-Z]').hasMatch(newText);
          final specialOk = RegExp(r'[^a-zA-Z0-9]').hasMatch(newText);

          return Padding(
            padding: EdgeInsets.fromLTRB(
              20,
              20,
              20,
              MediaQuery.of(ctx).viewInsets.bottom + 24,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'Đổi mật khẩu',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w800,
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.close),
                      onPressed: () => Navigator.pop(ctx),
                    ),
                  ],
                ),
                const SizedBox(height: 14),
                TextField(
                  controller: oldPassword,
                  obscureText: obscureOld,
                  decoration: InputDecoration(
                    labelText: 'Mật khẩu hiện tại',
                    prefixIcon: const Icon(Icons.lock_outline),
                    suffixIcon: IconButton(
                      icon: Icon(
                        obscureOld
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                      onPressed: () => setLocal(() => obscureOld = !obscureOld),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: newPassword,
                  obscureText: obscureNew,
                  onChanged: (_) => setLocal(() {}),
                  decoration: InputDecoration(
                    labelText: 'Mật khẩu mới',
                    prefixIcon: const Icon(Icons.lock_reset),
                    suffixIcon: IconButton(
                      icon: Icon(
                        obscureNew
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                      onPressed: () => setLocal(() => obscureNew = !obscureNew),
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                Padding(
                  padding: const EdgeInsets.only(left: 4),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _ruleItem('Ít nhất 8 ký tự', lengthOk),
                      _ruleItem('Ít nhất 1 chữ hoa (A-Z)', upperOk),
                      _ruleItem('Ít nhất 1 ký tự đặc biệt', specialOk),
                    ],
                  ),
                ),
                const SizedBox(height: 8),
                TextField(
                  controller: confirmPassword,
                  obscureText: obscureConfirm,
                  decoration: InputDecoration(
                    labelText: 'Xác nhận mật khẩu mới',
                    prefixIcon: const Icon(Icons.check_circle_outline),
                    suffixIcon: IconButton(
                      icon: Icon(
                        obscureConfirm
                            ? Icons.visibility_outlined
                            : Icons.visibility_off_outlined,
                      ),
                      onPressed: () =>
                          setLocal(() => obscureConfirm = !obscureConfirm),
                    ),
                  ),
                ),
                if (message != null) ...[
                  const SizedBox(height: 10),
                  Text(
                    message!,
                    style: const TextStyle(
                      color: AppColors.danger,
                      fontSize: 13,
                    ),
                  ),
                ],
                const SizedBox(height: 18),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: busy
                        ? null
                        : () async {
                            final oldP = oldPassword.text.trim();
                            final newP = newPassword.text;
                            final confirmP = confirmPassword.text;
                            if (oldP.isEmpty) {
                              setLocal(
                                () => message =
                                    'Vui lòng nhập mật khẩu hiện tại.',
                              );
                              return;
                            }
                            if (!lengthOk || !upperOk || !specialOk) {
                              setLocal(
                                () => message =
                                    'Mật khẩu mới chưa đáp ứng đầy đủ điều kiện.',
                              );
                              return;
                            }
                            if (newP != confirmP) {
                              setLocal(
                                () => message = 'Mật khẩu xác nhận không khớp.',
                              );
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
                                  const SnackBar(
                                    content: Text('Đổi mật khẩu thành công!'),
                                  ),
                                );
                              }
                            } catch (e) {
                              if (ctx.mounted) {
                                setLocal(() {
                                  busy = false;
                                  message = e.toString().replaceFirst(
                                    'Exception: ',
                                    '',
                                  );
                                });
                              }
                            }
                          },
                    child: Text(busy ? 'Đang cập nhật…' : 'Cập nhật mật khẩu'),
                  ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  static Widget _ruleItem(String label, bool ok) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          Icon(
            ok ? Icons.check_circle : Icons.radio_button_unchecked,
            size: 14,
            color: ok ? AppColors.success : AppColors.muted,
          ),
          const SizedBox(width: 6),
          Text(
            label,
            style: TextStyle(
              fontSize: 12,
              color: ok ? AppColors.success : AppColors.muted,
              fontWeight: ok ? FontWeight.w600 : FontWeight.w400,
            ),
          ),
        ],
      ),
    );
  }

  void _openEditProfileSheet() {
    final nameController = TextEditingController(
      text: _profile?.fullName ?? _session?.fullName ?? '',
    );
    final phoneController = TextEditingController(text: _profile?.phone ?? '');
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
          padding: EdgeInsets.fromLTRB(
            20,
            20,
            20,
            MediaQuery.of(ctx).viewInsets.bottom + 24,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'Chỉnh sửa thông tin',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800),
                  ),
                  IconButton(
                    icon: const Icon(Icons.close),
                    onPressed: () => Navigator.pop(ctx),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              if (message != null) ...[
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColors.danger.withValues(alpha: 0.1),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    message!,
                    style: const TextStyle(
                      color: AppColors.danger,
                      fontSize: 13,
                    ),
                  ),
                ),
                const SizedBox(height: 12),
              ],
              TextField(
                controller: nameController,
                decoration: const InputDecoration(
                  labelText: 'Họ và tên *',
                  prefixIcon: Icon(Icons.person_outline),
                ),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: phoneController,
                keyboardType: TextInputType.phone,
                decoration: const InputDecoration(
                  labelText: 'Số điện thoại',
                  prefixIcon: Icon(Icons.phone_outlined),
                ),
              ),
              const SizedBox(height: 20),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: busy
                      ? null
                      : () async {
                          final name = nameController.text.trim();
                          final phone = phoneController.text.trim();
                          if (name.isEmpty) {
                            setLocal(
                              () => message = 'Vui lòng nhập họ và tên.',
                            );
                            return;
                          }
                          setLocal(() {
                            busy = true;
                            message = null;
                          });
                          try {
                            final updated = await widget.api.updateProfile(
                              fullName: name,
                              phone: phone.isEmpty ? null : phone,
                            );
                            if (ctx.mounted) {
                              Navigator.pop(ctx);
                            }
                            if (!mounted) return;
                            setState(() {
                              _profile = updated;
                            });
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(
                                content: Text('Cập nhật thông tin thành công.'),
                              ),
                            );
                          } catch (e) {
                            if (ctx.mounted) {
                              setLocal(() {
                                busy = false;
                                message = e.toString().replaceFirst(
                                  'Exception: ',
                                  '',
                                );
                              });
                            }
                          }
                        },
                  child: Text(busy ? 'Đang lưu…' : 'Lưu thay đổi'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String get _heroInitial {
    final name = (_profile?.fullName ?? _session?.fullName ?? '').trim();
    if (name.isEmpty) return 'K';
    final parts = name
        .split(RegExp(r'\s+'))
        .where((p) => p.isNotEmpty)
        .toList();
    if (parts.isNotEmpty) {
      return parts.first[0].toUpperCase();
    }
    return 'K';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Hồ sơ cá nhân'), elevation: 0),
      body: !widget.isLoggedIn
          ? Center(
              child: AppEmptyState(
                icon: Icons.person_outline,
                title: 'Chưa đăng nhập',
                subtitle:
                    'Đăng nhập để xem thông tin cá nhân và quản lý tài khoản.',
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
          ? Center(
              child: AppErrorState(message: _error!, onRetry: _load),
            )
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.symmetric(
                  horizontal: 16,
                  vertical: 12,
                ),
                children: [
                  // Hero Profile Banner (matching web)
                  _buildHeroCard(),
                  const SizedBox(height: 16),

                  // Tab switcher (scrolls to target section)
                  _buildTabs(),
                  const SizedBox(height: 16),

                  // Content corresponding to selected tab only
                  AnimatedSwitcher(
                    duration: const Duration(milliseconds: 200),
                    child: switch (_currentTab) {
                      _ProfileTab.info => Column(
                        key: const ValueKey('info'),
                        children: [
                          _buildPersonalInfoCard(),
                          const SizedBox(height: 14),
                          _buildAccountStatusCard(),
                        ],
                      ),
                      _ProfileTab.security => KeyedSubtree(
                        key: const ValueKey('security'),
                        child: _buildSecurityCard(),
                      ),
                      _ProfileTab.notice => KeyedSubtree(
                        key: const ValueKey('notice'),
                        child: _buildNotificationCard(),
                      ),
                      _ProfileTab.activity => KeyedSubtree(
                        key: const ValueKey('activity'),
                        child: _buildActivityCard(),
                      ),
                    },
                  ),

                  // 6. Account Management (Logout)
                  _buildAccountManagementCard(),
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

  // Hero Card matching Web style
  Widget _buildHeroCard() {
    final fullName = _profile?.fullName ?? _session?.fullName ?? '—';
    final email = _profile?.email ?? _session?.email ?? '';
    final phone = _profile?.phone;

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha: 0.03),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          // Blue circle avatar with initial (matches web hero avatar)
          Container(
            width: 64,
            height: 64,
            decoration: const BoxDecoration(
              color: Color(0xFF2563EB),
              shape: BoxShape.circle,
            ),
            alignment: Alignment.center,
            child: Text(
              _heroInitial,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 28,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'KHÁCH HÀNG',
                  style: TextStyle(
                    color: Color(0xFF2563EB),
                    fontWeight: FontWeight.w800,
                    fontSize: 11,
                    letterSpacing: 0.8,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  fullName,
                  style: const TextStyle(
                    fontSize: 20,
                    fontWeight: FontWeight.w800,
                    color: AppColors.text,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 12,
                  runSpacing: 4,
                  children: [
                    if (email.isNotEmpty)
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(
                            Icons.email_outlined,
                            size: 14,
                            color: AppColors.muted,
                          ),
                          const SizedBox(width: 4),
                          Text(
                            email,
                            style: const TextStyle(
                              color: AppColors.muted,
                              fontSize: 13,
                            ),
                          ),
                        ],
                      ),
                    if (phone != null && phone.isNotEmpty)
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Icon(
                            Icons.phone_outlined,
                            size: 14,
                            color: AppColors.muted,
                          ),
                          const SizedBox(width: 4),
                          Text(
                            phone,
                            style: const TextStyle(
                              color: AppColors.muted,
                              fontSize: 13,
                            ),
                          ),
                        ],
                      ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // Horizontal Tab Pills matching Web
  Widget _buildTabs() {
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: _ProfileTab.values.map((tab) {
          final isSelected = tab == _currentTab;
          return Padding(
            padding: const EdgeInsets.only(right: 8),
            child: InkWell(
              onTap: () => setState(() => _currentTab = tab),
              borderRadius: BorderRadius.circular(20),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                padding: const EdgeInsets.symmetric(
                  horizontal: 16,
                  vertical: 8,
                ),
                decoration: BoxDecoration(
                  color: isSelected ? const Color(0xFF2563EB) : Colors.white,
                  borderRadius: BorderRadius.circular(20),
                  border: Border.all(
                    color: isSelected
                        ? const Color(0xFF2563EB)
                        : AppColors.border,
                  ),
                ),
                child: Text(
                  tab.label,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: isSelected ? FontWeight.w700 : FontWeight.w500,
                    color: isSelected ? Colors.white : AppColors.muted,
                  ),
                ),
              ),
            ),
          );
        }).toList(),
      ),
    );
  }

  // Personal Info Card matching Web
  Widget _buildPersonalInfoCard() {
    final fullName = _profile?.fullName ?? _session?.fullName ?? '—';
    final phone = _profile?.phone;
    final email = _profile?.email ?? _session?.email ?? '—';

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Thông tin cá nhân',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w800,
                  color: AppColors.text,
                ),
              ),
              OutlinedButton(
                onPressed: _openEditProfileSheet,
                style: OutlinedButton.styleFrom(
                  foregroundColor: const Color(0xFF2563EB),
                  side: const BorderSide(color: Color(0xFF2563EB)),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 6,
                  ),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                child: const Text(
                  'Chỉnh sửa thông tin',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          _buildFieldBox('Họ và tên', fullName),
          const SizedBox(height: 12),
          _buildFieldBox(
            'Số điện thoại',
            (phone == null || phone.isEmpty) ? '—' : phone,
          ),
          const SizedBox(height: 12),
          _buildFieldBox('Email', email),
        ],
      ),
    );
  }

  Widget _buildFieldBox(String label, String value) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w500,
            color: AppColors.muted,
          ),
        ),
        const SizedBox(height: 6),
        Container(
          width: double.infinity,
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          decoration: BoxDecoration(
            color: const Color(0xFFF8FAFC),
            borderRadius: BorderRadius.circular(10),
            border: Border.all(color: const Color(0xFFE2E8F0)),
          ),
          child: Text(
            value,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: AppColors.text,
            ),
          ),
        ),
      ],
    );
  }

  // Account Status Card matching Web
  Widget _buildAccountStatusCard() {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Trạng thái tài khoản',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: AppColors.text,
            ),
          ),
          const SizedBox(height: 14),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: const [
              Text(
                'Loại tài khoản',
                style: TextStyle(color: AppColors.muted, fontSize: 14),
              ),
              Text(
                'Khách hàng',
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 14,
                  color: AppColors.text,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  // Security Card matching Web
  Widget _buildSecurityCard() {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Bảo mật tài khoản',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: AppColors.text,
            ),
          ),
          const SizedBox(height: 16),
          // Password row
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const [
                  Text(
                    'Mật khẩu',
                    style: TextStyle(color: AppColors.muted, fontSize: 12),
                  ),
                  SizedBox(height: 4),
                  Text(
                    '••••••••••••',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 15,
                      letterSpacing: 2,
                    ),
                  ),
                ],
              ),
              OutlinedButton(
                onPressed: _openChangePasswordSheet,
                style: OutlinedButton.styleFrom(
                  foregroundColor: const Color(0xFF2563EB),
                  side: const BorderSide(color: Color(0xFF2563EB)),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 6,
                  ),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(10),
                  ),
                ),
                child: const Text(
                  'Đổi mật khẩu',
                  style: TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          const Divider(color: AppColors.border),
          const SizedBox(height: 12),
          // Google login notice box matching Web
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: const Color(0xFFF8FAFC),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: const Color(0xFFE2E8F0)),
            ),
            child: Row(
              children: [
                Container(
                  width: 36,
                  height: 36,
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(color: AppColors.border),
                  ),
                  alignment: Alignment.center,
                  child: const Text(
                    'G',
                    style: TextStyle(
                      fontSize: 20,
                      fontWeight: FontWeight.w900,
                      color: Color(0xFF4285F4),
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Đăng nhập bằng Google',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          fontSize: 13,
                          color: AppColors.text,
                        ),
                      ),
                      SizedBox(height: 2),
                      Text(
                        'DriveX đã hỗ trợ đăng nhập Google cho khách hàng.',
                        style: TextStyle(color: AppColors.muted, fontSize: 12),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  // Notification Card matching Web
  Widget _buildNotificationCard() {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Cài đặt thông báo',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: AppColors.text,
            ),
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(
                builder: (_) => NotificationsScreen(api: widget.api),
              ),
            ),
            icon: const Icon(Icons.notifications_outlined),
            label: const Text('Xem thông báo của tôi'),
          ),
          const SizedBox(height: 4),
          const Text(
            'Cài đặt này sẽ được kết nối trong giai đoạn tích hợp.',
            style: TextStyle(color: AppColors.muted, fontSize: 13),
          ),
          const SizedBox(height: 16),
          _buildNoticeToggle(
            'Đơn thuê',
            'Thông báo khi đơn thuê thay đổi trạng thái',
            _noticeBooking,
            (val) => setState(() => _noticeBooking = val),
          ),
          const Divider(height: 20, color: AppColors.border),
          _buildNoticeToggle(
            'Thanh toán',
            'Thông báo về trạng thái thanh toán',
            _noticePayment,
            (val) => setState(() => _noticePayment = val),
          ),
          const Divider(height: 20, color: AppColors.border),
          _buildNoticeToggle(
            'Hợp đồng',
            'Thông báo về hợp đồng',
            _noticeContract,
            (val) => setState(() => _noticeContract = val),
          ),
          const Divider(height: 20, color: AppColors.border),
          _buildNoticeToggle(
            'Chuyến thuê',
            'Thông báo về lịch trình chuyến thuê',
            _noticeTrip,
            (val) => setState(() => _noticeTrip = val),
          ),
        ],
      ),
    );
  }

  Widget _buildNoticeToggle(
    String title,
    String hint,
    bool value,
    ValueChanged<bool> onChanged,
  ) {
    return Row(
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                title,
                style: const TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 14,
                  color: AppColors.text,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                hint,
                style: const TextStyle(color: AppColors.muted, fontSize: 12),
              ),
            ],
          ),
        ),
        Switch(
          value: value,
          onChanged: onChanged,
          activeThumbColor: const Color(0xFF2563EB),
        ),
      ],
    );
  }

  // Account Activity Card matching Web
  Widget _buildActivityCard() {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: const [
          Text(
            'Hoạt động gần đây',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: AppColors.text,
            ),
          ),
          SizedBox(height: 10),
          Text(
            'Chưa có nhật ký hoạt động từ hệ thống.',
            style: TextStyle(color: AppColors.muted, fontSize: 13),
          ),
        ],
      ),
    );
  }

  // Account Management Card (Logout)
  Widget _buildAccountManagementCard() {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'Quản lý tài khoản',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w800,
              color: AppColors.text,
            ),
          ),
          const SizedBox(height: 14),
          OutlinedButton(
            onPressed: _logout,
            style: OutlinedButton.styleFrom(
              foregroundColor: AppColors.danger,
              side: const BorderSide(color: AppColors.danger),
              minimumSize: const Size.fromHeight(46),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(12),
              ),
            ),
            child: const Text(
              'Đăng xuất',
              style: TextStyle(fontWeight: FontWeight.w700),
            ),
          ),
        ],
      ),
    );
  }
}
