import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'trip_detail_screen.dart';

/// The driver's notification history (GET /api/notifications): assignments, pre-handover checks,
/// vehicle changes, licence verification, incidents recorded by the dispatcher…
class NotificationsScreen extends StatefulWidget {
  final ApiService api;

  const NotificationsScreen({super.key, required this.api});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  List<DriverNotification> _items = [];
  bool _loading = true;
  String? _error;

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
      _items = await widget.api.getNotifications();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    }
    if (mounted) setState(() => _loading = false);
  }

  void _showError(Object e) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
    );
  }

  Future<void> _open(DriverNotification item) async {
    if (!item.isRead) {
      try {
        final updated = await widget.api.markNotificationRead(item.notificationId);
        if (!mounted) return;
        setState(() {
          _items = [
            for (final n in _items) n.notificationId == updated.notificationId ? updated : n,
          ];
        });
      } catch (e) {
        _showError(e);
        return;
      }
    }
    final bookingId = item.bookingId;
    if (bookingId == null) return;
    try {
      final trips = await widget.api.getMyTrips();
      DriverBooking? trip;
      for (final t in trips) {
        if (t.bookingId == bookingId) trip = t;
      }
      if (!mounted) return;
      if (trip == null) {
        _showError('Chuyến #$bookingId không còn trong danh sách của bạn.');
        return;
      }
      final found = trip;
      await Navigator.push(
        context,
        MaterialPageRoute(builder: (_) => TripDetailScreen(api: widget.api, trip: found)),
      );
    } catch (e) {
      _showError(e);
    }
  }

  Future<void> _readAll() async {
    try {
      await widget.api.markAllNotificationsRead();
      await _load();
    } catch (e) {
      _showError(e);
    }
  }

  @override
  Widget build(BuildContext context) {
    final unread = _items.where((n) => !n.isRead).length;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Thông báo'),
        actions: [
          if (unread > 0) TextButton(onPressed: _readAll, child: const Text('Đọc tất cả')),
        ],
      ),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải thông báo...'))
          : _error != null
          ? Center(child: AppErrorState(message: _error!, onRetry: _load))
          : RefreshIndicator(
              onRefresh: _load,
              child: _items.isEmpty
                  ? ListView(
                      children: const [
                        SizedBox(height: 120),
                        AppEmptyState(
                          icon: Icons.notifications_none,
                          title: 'Chưa có thông báo',
                          subtitle: 'Phân công chuyến, biên bản kiểm tra xe, đổi xe… sẽ hiện tại đây.',
                        ),
                      ],
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(12),
                      itemCount: _items.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 8),
                      itemBuilder: (_, i) {
                        final n = _items[i];
                        final scheme = Theme.of(context).colorScheme;
                        return Card(
                          color: n.isRead ? null : scheme.primaryContainer.withValues(alpha: 0.35),
                          child: ListTile(
                            onTap: () => _open(n),
                            leading: CircleAvatar(
                              backgroundColor: n.isRead ? scheme.surfaceContainerHighest : AppColors.primary,
                              child: Icon(
                                n.isRead ? Icons.notifications_none : Icons.notifications,
                                color: n.isRead ? AppColors.muted : Colors.white,
                              ),
                            ),
                            title: Text(n.title, style: const TextStyle(fontWeight: FontWeight.w800)),
                            subtitle: Padding(
                              padding: const EdgeInsets.only(top: 5),
                              child: Text('${n.message}\n${Formatters.dt(n.createdAt)}'),
                            ),
                            trailing: n.bookingId == null ? null : const Icon(Icons.chevron_right),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}
