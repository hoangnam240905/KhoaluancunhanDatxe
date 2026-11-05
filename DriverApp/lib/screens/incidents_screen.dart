import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';

class IncidentsScreen extends StatefulWidget {
  const IncidentsScreen({super.key, required this.api});
  final ApiService api;

  @override
  State<IncidentsScreen> createState() => _IncidentsScreenState();
}

class _IncidentsScreenState extends State<IncidentsScreen> {
  List<IncidentReport> _items = [];
  bool _loading = true;
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

  void _onRealtime() => _load();

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      _items = await widget.api.getMyIncidents();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Sự cố của tôi'),
      actions: [
        IconButton(
          onPressed: _load,
          icon: const Icon(Icons.refresh),
          tooltip: 'Tải lại',
        ),
      ],
    ),
    body: _loading
        ? const Center(child: AppLoading(message: 'Đang tải báo cáo sự cố...'))
        : RefreshIndicator(
            onRefresh: _load,
            child: _error != null
                ? ListView(
                    children: [
                      const SizedBox(height: 80),
                      AppErrorState(message: _error!, onRetry: _load),
                    ],
                  )
                : _items.isEmpty
                ? ListView(
                    children: const [
                      SizedBox(height: 80),
                      AppEmptyState(
                        icon: Icons.warning_amber_outlined,
                        title: 'Chưa có báo cáo sự cố',
                        subtitle:
                            'Các sự cố bạn gửi trong chuyến sẽ được lưu ở đây.',
                      ),
                    ],
                  )
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _items.length,
                    separatorBuilder: (_, index) => const SizedBox(height: 12),
                    itemBuilder: (context, index) => _card(_items[index]),
                  ),
          ),
  );

  Widget _card(IncidentReport incident) {
    final color = incident.status.toLowerCase() == 'open'
        ? AppColors.warning
        : incident.status.toLowerCase() == 'resolved'
        ? AppColors.success
        : AppColors.muted;
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(
                Icons.report_gmailerrorred_outlined,
                color: AppColors.danger,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  _typeLabel(incident.incidentType),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    fontSize: 16,
                  ),
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 5,
                ),
                decoration: BoxDecoration(
                  color: color.withValues(alpha: 0.14),
                  borderRadius: BorderRadius.circular(20),
                ),
                child: Text(
                  _statusLabel(incident.status),
                  style: TextStyle(
                    color: color,
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 10),
          Text(incident.description),
          const SizedBox(height: 12),
          Wrap(
            spacing: 14,
            runSpacing: 6,
            children: [
              _meta(
                Icons.confirmation_number_outlined,
                'Đơn #${incident.bookingId}',
              ),
              _meta(Icons.schedule, Formatters.dt(incident.occurredAt)),
            ],
          ),
        ],
      ),
    );
  }

  Widget _meta(IconData icon, String text) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(
        icon,
        size: 15,
        color: Theme.of(context).colorScheme.onSurfaceVariant,
      ),
      const SizedBox(width: 5),
      Text(
        text,
        style: TextStyle(
          color: Theme.of(context).colorScheme.onSurfaceVariant,
          fontSize: 12,
        ),
      ),
    ],
  );

  String _typeLabel(String type) => switch (type) {
    'Accident' => 'Tai nạn',
    'VehicleIssue' => 'Sự cố xe',
    'CustomerIssue' => 'Vấn đề khách hàng',
    'Other' => 'Khác',
    _ => type,
  };

  String _statusLabel(String status) => switch (status.toLowerCase()) {
    'open' => 'Đang xử lý',
    'resolved' => 'Đã xử lý',
    _ => status,
  };
}
