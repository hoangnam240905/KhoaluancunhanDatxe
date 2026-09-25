import 'package:flutter/material.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';

class StatusScreen extends StatefulWidget {
  final ApiService api;
  const StatusScreen({super.key, required this.api});

  @override
  State<StatusScreen> createState() => _StatusScreenState();
}

class _StatusScreenState extends State<StatusScreen> {
  String _status = 'Available';
  bool _loading = true;
  bool _busy = false;
  bool _hasOpenTrip = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
    widget.api.realtime.addListener(_onRealtime);
  }

  @override
  void dispose() {
    widget.api.realtime.removeListener(_onRealtime);
    super.dispose();
  }

  void _onRealtime() {
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final profile = await widget.api.getMyDriverProfile();
      final trips = await widget.api.getMyTrips();
      _status = profile.status;
      _hasOpenTrip = trips.any((t) {
        final s = t.assignment?.status;
        return s == 'Assigned' || s == 'Accepted' || s == 'InProgress';
      });
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _setStatus(String status) async {
    setState(() => _busy = true);
    try {
      final profile = await widget.api.updateStatus(status);
      if (!mounted) return;
      setState(() => _status = profile.status);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Trạng thái: ${Formatters.driverStatus(profile.status)}',
          ),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
      await _load();
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Trạng thái tài xế')),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải...'))
          : _error != null
          ? Center(
              child: AppErrorState(message: _error!, onRetry: _load),
            )
          : ListView(
              padding: const EdgeInsets.all(16),
              children: [
                AppCard(
                  child: Row(
                    children: [
                      const Icon(
                        Icons.badge_outlined,
                        color: AppColors.primary,
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text(
                              'Trạng thái máy chủ',
                              style: TextStyle(
                                color: AppColors.muted,
                                fontSize: 12,
                              ),
                            ),
                            Text(
                              Formatters.driverStatus(_status),
                              style: const TextStyle(
                                fontWeight: FontWeight.w800,
                                fontSize: 18,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                if (_hasOpenTrip) ...[
                  const SizedBox(height: 12),
                  const AppCard(
                    child: Text(
                      'Bạn đang có chuyến chưa hoàn thành. Máy chủ sẽ từ chối chuyển Available.',
                      style: TextStyle(fontWeight: FontWeight.w600),
                    ),
                  ),
                ],
                const SizedBox(height: 12),
                _choice(
                  'Available',
                  'Sẵn sàng nhận chuyến mới',
                  Icons.check_circle_outline,
                ),
                const SizedBox(height: 10),
                _choice('Busy', 'Đang bận / đang có việc', Icons.work_outline),
                const SizedBox(height: 10),
                _choice(
                  'Offline',
                  'Không nhận chuyến',
                  Icons.power_settings_new,
                ),
                if (_busy)
                  const Padding(
                    padding: EdgeInsets.only(top: 16),
                    child: Center(child: CircularProgressIndicator()),
                  ),
              ],
            ),
    );
  }

  Widget _choice(String value, String subtitle, IconData icon) {
    final selected = _status == value;
    return AppCard(
      onTap: _busy ? null : () => _setStatus(value),
      child: Row(
        children: [
          Icon(icon, color: selected ? AppColors.primary : AppColors.muted),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  Formatters.driverStatus(value),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
                Text(subtitle, style: const TextStyle(color: AppColors.muted)),
              ],
            ),
          ),
          Icon(
            selected ? Icons.radio_button_checked : Icons.radio_button_off,
            color: selected ? AppColors.primary : AppColors.muted,
          ),
        ],
      ),
    );
  }
}
