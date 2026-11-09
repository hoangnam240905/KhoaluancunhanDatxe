import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'booking_detail_screen.dart';

class NotificationsScreen extends StatefulWidget {
  final ApiService api;

  const NotificationsScreen({super.key, required this.api});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  List<CustomerNotification> _items = [];
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

  Future<void> _read(CustomerNotification item) async {
    if (item.isRead) return;
    try {
      final updated = await widget.api.markNotificationRead(item.notificationId);
      if (!mounted) return;
      setState(() {
        _items = _items
            .map((current) => current.notificationId == updated.notificationId ? updated : current)
            .toList();
      });
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
        );
      }
    }
  }

  Future<void> _readAll() async {
    try {
      await widget.api.markAllNotificationsRead();
      await _load();
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final unread = _items.where((item) => !item.isRead).length;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Thông báo'),
        actions: [
          if (unread > 0)
            TextButton(onPressed: _readAll, child: const Text('Đọc tất cả')),
        ],
      ),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải thông báo...'))
          : _error != null
              ? Center(child: AppErrorState(message: _error!, onRetry: _load))
              : RefreshIndicator(
                  onRefresh: _load,
                  child: _items.isEmpty
                      ? ListView(children: const [
                          SizedBox(height: 120),
                          AppEmptyState(
                            icon: Icons.notifications_none,
                            title: 'Chưa có thông báo',
                            subtitle: 'Các cập nhật về đơn thuê sẽ xuất hiện tại đây.',
                          ),
                        ])
                      : ListView.separated(
                          padding: const EdgeInsets.all(12),
                          itemCount: _items.length,
                          separatorBuilder: (_, _) => const SizedBox(height: 8),
                          itemBuilder: (_, index) {
                            final item = _items[index];
                            return Card(
                              color: item.isRead ? Colors.white : const Color(0xFFEFF6FF),
                              child: ListTile(
                                onTap: () async {
                                  await _read(item);
                                  if (item.bookingId != null && context.mounted) {
                                    try {
                                      final booking = await widget.api.getBooking(item.bookingId!);
                                      if (context.mounted) {
                                        Navigator.push(
                                          context,
                                          MaterialPageRoute(
                                            builder: (_) => BookingDetailScreen(
                                              api: widget.api,
                                              booking: booking,
                                            ),
                                          ),
                                        );
                                      }
                                    } catch (_) {}
                                  }
                                },
                                leading: CircleAvatar(
                                  backgroundColor: item.isRead ? AppColors.border : AppColors.primary,
                                  child: Icon(
                                    item.isRead ? Icons.notifications_none : Icons.notifications,
                                    color: item.isRead ? AppColors.muted : Colors.white,
                                  ),
                                ),
                                title: Text(item.title, style: const TextStyle(fontWeight: FontWeight.w800)),
                                subtitle: Padding(
                                  padding: const EdgeInsets.only(top: 5),
                                  child: Text('${item.message}\n${Formatters.rentalDt(item.createdAt)}'),
                                ),
                              ),
                            );
                          },
                        ),
                ),
    );
  }
}
