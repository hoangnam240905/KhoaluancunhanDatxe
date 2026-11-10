import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import 'account_screen.dart';
import 'incidents_screen.dart';
import 'schedule_screen.dart';
import 'status_screen.dart';
import 'trips_screen.dart';
import 'verification_screen.dart';

class DriverShell extends StatefulWidget {
  final ApiService api;
  final String userName;
  final bool darkMode;
  final ValueChanged<bool> onDarkModeChanged;

  const DriverShell({
    super.key,
    required this.api,
    required this.userName,
    required this.darkMode,
    required this.onDarkModeChanged,
    this.openVerificationOnStart = false,
  });

  /// Right after sign-up: open "Bằng lái & giấy tờ" straight away.
  final bool openVerificationOnStart;

  @override
  State<DriverShell> createState() => _DriverShellState();
}

class _DriverShellState extends State<DriverShell> {
  int _index = 0;
  DriverVerification? _verification;
  late final List<Widget?> _pageCache = List<Widget?>.filled(5, null);

  @override
  void initState() {
    super.initState();
    _pageCache[0] = TripsScreen(api: widget.api, userName: widget.userName);
    widget.api.connectRealtime();
    _loadVerification();
    if (widget.openVerificationOnStart) {
      WidgetsBinding.instance.addPostFrameCallback((_) => _openVerification());
    }
  }

  Future<void> _loadVerification() async {
    try {
      final v = await widget.api.getMyVerification();
      if (mounted) setState(() => _verification = v);
    } catch (_) {
      // The banner is a reminder only; the trips still load without it.
    }
  }

  Future<void> _openVerification() async {
    await Navigator.of(context).push(
      MaterialPageRoute(builder: (_) => VerificationScreen(api: widget.api)),
    );
    _loadVerification();
  }

  /// Shown until the dispatcher has verified the licence (plan1.md Phần D), or when it is expiring.
  Widget? _banner() {
    final v = _verification;
    if (v == null) return null;
    String? text;
    if (!v.isVerified) {
      text = v.verificationStatus == 'Rejected'
          ? 'Xác minh bằng lái không thành công. Vui lòng cập nhật lại thông tin.'
          : v.photoStatus == 'Accepted'
          ? 'Ảnh đã được chấp nhận — lên văn phòng để đối chiếu bằng gốc.'
          : 'Tài khoản chưa xác minh. Cập nhật bằng lái và giấy tờ để được phân công chuyến.';
    } else if (v.licenseExpired || v.licenseExpiringSoon) {
      text = v.licenseExpired ? 'Bằng lái đã hết hạn. Vui lòng cập nhật bằng mới.' : 'Bằng lái sắp hết hạn. Vui lòng cập nhật bằng mới.';
    }
    if (text == null) return null;
    return Material(
      color: const Color(0xFFFFF7ED),
      child: SafeArea(
        bottom: false,
        child: ListTile(
          leading: const Icon(Icons.badge_outlined, color: Color(0xFFD97706)),
          title: Text(text, style: const TextStyle(fontSize: 13, color: Color(0xFF7C2D12))),
          trailing: const Icon(Icons.chevron_right, color: Color(0xFF7C2D12)),
          onTap: _openVerification,
        ),
      ),
    );
  }

  @override
  void dispose() {
    widget.api.disconnectRealtime();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    body: Column(
      children: [
        ?_banner(),
        Expanded(child: _pages()),
      ],
    ),
    bottomNavigationBar: _nav(),
  );

  Widget _pages() {
    _pageCache[_index] ??= switch (_index) {
      0 => TripsScreen(api: widget.api, userName: widget.userName),
      1 => ScheduleScreen(api: widget.api),
      2 => IncidentsScreen(api: widget.api),
      3 => StatusScreen(api: widget.api),
      4 => AccountScreen(
          api: widget.api,
          userName: widget.userName,
          darkMode: widget.darkMode,
          onDarkModeChanged: widget.onDarkModeChanged,
          onOpenVerification: _openVerification,
        ),
      _ => const SizedBox.shrink(),
    };

    return IndexedStack(
      index: _index,
      children: List<Widget>.generate(
        _pageCache.length,
        (i) => _pageCache[i] ?? const SizedBox.shrink(),
      ),
    );
  }

  Widget _nav() => NavigationBar(
      selectedIndex: _index,
      onDestinationSelected: (i) => setState(() => _index = i),
      destinations: const [
        NavigationDestination(
          icon: Icon(Icons.route_outlined),
          selectedIcon: Icon(Icons.route),
          label: 'Chuyến',
        ),
        NavigationDestination(
          icon: Icon(Icons.calendar_month_outlined),
          selectedIcon: Icon(Icons.calendar_month),
          label: 'Lịch',
        ),
        NavigationDestination(
          icon: Icon(Icons.report_gmailerrorred_outlined),
          selectedIcon: Icon(Icons.report),
          label: 'Sự cố',
        ),
        NavigationDestination(
          icon: Icon(Icons.toggle_on_outlined),
          selectedIcon: Icon(Icons.toggle_on),
          label: 'Trạng thái',
        ),
        NavigationDestination(
          icon: Icon(Icons.person_outline),
          selectedIcon: Icon(Icons.person),
          label: 'Tài khoản',
        ),
      ],
    );
}
