import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../utils/driver_trip_actions.dart';
import 'customer_shell.dart';

class DriverTripsScreen extends StatefulWidget {
  final ApiService api;
  final String userName;

  const DriverTripsScreen({
    super.key,
    required this.api,
    required this.userName,
  });

  @override
  State<DriverTripsScreen> createState() => _DriverTripsScreenState();
}

class _DriverTripsScreenState extends State<DriverTripsScreen> {
  List<DriverBooking> _trips = [];
  bool _loading = true;
  String _driverStatus = 'Available';

  @override
  void initState() {
    super.initState();
    widget.api.connectRealtime();
    _load();
    widget.api.realtime.addListener(_onRealtime);
  }

  @override
  void dispose() {
    widget.api.realtime.removeListener(_onRealtime);
    widget.api.disconnectRealtime();
    super.dispose();
  }

  void _onRealtime() {
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      _trips = await widget.api.getMyTrips();
    } catch (_) {}
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _setStatus(String status) async {
    try {
      await widget.api.updateDriverStatus(status);
      setState(() => _driverStatus = status);
      if (!mounted) return;
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(SnackBar(content: Text('Trạng thái: $status')));
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
  }

  Future<void> _action(Future<void> Function() fn) async {
    try {
      await fn();
      await _load();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
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

  List<Widget> _actionButtons(int assignmentId, String? assignmentStatus) {
    final action = DriverTripActions.forAssignmentStatus(assignmentStatus);
    if (action == null) return [];

    return [
      const SizedBox(height: 10),
      switch (action) {
        DriverTripAction.accept => FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: const Color(0xFFEA580C),
            ),
            onPressed: () => _action(() => widget.api.acceptTrip(assignmentId)),
            child: Text(action.label),
          ),
        DriverTripAction.start => FilledButton(
            onPressed: () => _action(() => widget.api.startTrip(assignmentId)),
            child: Text(action.label),
          ),
        DriverTripAction.complete => FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: const Color(0xFF16A34A),
            ),
            onPressed: () =>
                _action(() => widget.api.completeTrip(assignmentId)),
            child: Text(action.label),
          ),
      },
    ];
  }

  @override
  Widget build(BuildContext context) {
    final fmt = DateFormat('dd/MM/yyyy HH:mm');
    final money = NumberFormat.currency(locale: 'vi_VN', symbol: 'd');

    return Scaffold(
      appBar: AppBar(
        title: Text('Tài xế · ${widget.userName}'),
        backgroundColor: const Color(0xFFEA580C),
        foregroundColor: Colors.white,
        actions: [
          IconButton(icon: const Icon(Icons.refresh), onPressed: _load),
          IconButton(icon: const Icon(Icons.logout), onPressed: _logout),
        ],
      ),
      body: Column(
        children: [
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            color: const Color(0xFFFFF7ED),
            child: Wrap(
              spacing: 8,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                const Text(
                  'Trạng thái:',
                  style: TextStyle(fontWeight: FontWeight.w600),
                ),
                ChoiceChip(
                  label: const Text('Sẵn sàng'),
                  selected: _driverStatus == 'Available',
                  onSelected: (_) => _setStatus('Available'),
                ),
                ChoiceChip(
                  label: const Text('Bận'),
                  selected: _driverStatus == 'Busy',
                  onSelected: (_) => _setStatus('Busy'),
                ),
                ChoiceChip(
                  label: const Text('Offline'),
                  selected: _driverStatus == 'Offline',
                  onSelected: (_) => _setStatus('Offline'),
                ),
              ],
            ),
          ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : RefreshIndicator(
                    onRefresh: _load,
                    child: _trips.isEmpty
                        ? ListView(
                            children: const [
                              SizedBox(height: 120),
                              Center(child: Text('Chưa có chuyến nào')),
                            ],
                          )
                        : ListView.builder(
                            padding: const EdgeInsets.all(12),
                            itemCount: _trips.length,
                            itemBuilder: (context, i) {
                              final t = _trips[i];
                              final aid = t.assignmentId;
                              return Card(
                                margin: const EdgeInsets.only(bottom: 10),
                                child: Padding(
                                  padding: const EdgeInsets.all(14),
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        '#${t.bookingId} · ${t.vehicleTypeName}',
                                        style: const TextStyle(
                                          fontWeight: FontWeight.bold,
                                          fontSize: 16,
                                        ),
                                      ),
                                      const SizedBox(height: 6),
                                      Text('Khách: ${t.customerName}'),
                                      Text(
                                        '${t.pickupAddress} → ${t.dropoffAddress}',
                                      ),
                                      Text(fmt.format(t.startDate.toLocal())),
                                      Text(
                                        'Cước: ${money.format(t.totalAmount)}',
                                      ),
                                      Text(
                                        'Trạng thái: ${t.assignment?.status ?? t.status}',
                                      ),
                                      if (t.assignment != null)
                                        Text(
                                          'Xe: ${t.assignment!.licensePlate}',
                                        ),
                                      if (aid != null)
                                        ..._actionButtons(aid, t.assignment?.status),
                                    ],
                                  ),
                                ),
                              );
                            },
                          ),
                  ),
          ),
        ],
      ),
    );
  }
}
