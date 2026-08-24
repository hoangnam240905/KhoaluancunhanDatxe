import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import '../data/portal_content.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import 'bookings_screen.dart';
import 'create_booking_screen.dart';
import 'login_screen.dart';

class HomeScreen extends StatefulWidget {
  final ApiService api;
  final String? userName;

  const HomeScreen({super.key, required this.api, this.userName});

  bool get isLoggedIn => userName != null && userName!.isNotEmpty;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  List<VehicleType> _types = [];
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
      _types = await widget.api.getVehicleTypes();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _logout() async {
    await widget.api.authService.logout();
    if (!mounted) return;
    Navigator.of(context).pushReplacement(
      MaterialPageRoute(builder: (_) => HomeScreen(api: ApiService())),
    );
  }

  void _requireLoginThen(VoidCallback action) {
    if (widget.isLoggedIn) {
      action();
      return;
    }
    Navigator.push(context, MaterialPageRoute(builder: (_) => const LoginScreen()));
  }

  VehicleType? _findType(int typeId) {
    try {
      return _types.firstWhere((t) => t.typeId == typeId);
    } catch (_) {
      return _types.isNotEmpty ? _types.first : null;
    }
  }

  void _bookRoute(PopularRoute route) {
    _requireLoginThen(() {
      final type = _findType(route.vehicleTypeId);
      if (type == null) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Chua tai duoc loai xe. Kiem tra Backend.')),
        );
        return;
      }
      Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => CreateBookingScreen(
            api: widget.api,
            vehicleType: type,
            pickup: route.from,
            dropoff: route.to,
            distanceKm: route.distanceKm.toDouble(),
          ),
        ),
      );
    });
  }

  void _bookType(VehicleType type) {
    _requireLoginThen(() {
      Navigator.push(
        context,
        MaterialPageRoute(
          builder: (_) => CreateBookingScreen(api: widget.api, vehicleType: type),
        ),
      );
    });
  }

  String _money(double v) => NumberFormat('#,###', 'vi_VN').format(v);

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF0F4FF),
      body: RefreshIndicator(
        onRefresh: _load,
        child: CustomScrollView(
          slivers: [
            SliverAppBar(
              expandedHeight: 220,
              pinned: true,
              backgroundColor: const Color(0xFF1E3A8A),
              foregroundColor: Colors.white,
              actions: [
                if (widget.isLoggedIn) ...[
                  IconButton(
                    icon: const Icon(Icons.list_alt),
                    tooltip: 'Don cua toi',
                    onPressed: () => Navigator.push(
                      context,
                      MaterialPageRoute(builder: (_) => BookingsScreen(api: widget.api)),
                    ),
                  ),
                  IconButton(icon: const Icon(Icons.logout), onPressed: _logout),
                ] else ...[
                  TextButton(
                    onPressed: () => Navigator.push(
                      context,
                      MaterialPageRoute(builder: (_) => const LoginScreen()),
                    ),
                    child: const Text('Dang nhap', style: TextStyle(color: Colors.white)),
                  ),
                ],
              ],
              flexibleSpace: FlexibleSpaceBar(
                background: Container(
                  decoration: const BoxDecoration(
                    gradient: LinearGradient(
                      colors: [Color(0xFF0F172A), Color(0xFF1E3A8A), Color(0xFF7C3AED)],
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                    ),
                  ),
                  padding: const EdgeInsets.fromLTRB(20, 80, 20, 20),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        widget.isLoggedIn ? 'Xin chao, ${widget.userName}!' : 'Car Rental System',
                        style: const TextStyle(color: Colors.white70, fontSize: 14),
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'Di moi noi,\nthue xe tron goi',
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 28,
                          fontWeight: FontWeight.w800,
                          height: 1.15,
                        ),
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        'Dat xe 4-16 cho voi tai xe chuyen nghiep',
                        style: TextStyle(color: Colors.white70, fontSize: 13),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            SliverToBoxAdapter(child: _buildBody()),
          ],
        ),
      ),
      floatingActionButton: widget.isLoggedIn
          ? FloatingActionButton.extended(
              onPressed: () {
                if (_types.isEmpty) return;
                _bookType(_types.first);
              },
              backgroundColor: const Color(0xFF2563EB),
              icon: const Icon(Icons.add),
              label: const Text('Dat xe'),
            )
          : FloatingActionButton.extended(
              onPressed: () => Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => const LoginScreen()),
              ),
              backgroundColor: const Color(0xFF2563EB),
              icon: const Icon(Icons.login),
              label: const Text('Dang nhap de dat'),
            ),
    );
  }

  Widget _buildBody() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 20, 16, 100),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _sectionTitle('Tuyen pho bien', 'Goi y hanh trinh hot'),
          const SizedBox(height: 12),
          ...PortalContent.popularRoutes.map(_routeCard),
          const SizedBox(height: 28),
          _sectionTitle('Bang gia xe', 'Chon loai xe phu hop'),
          const SizedBox(height: 12),
          if (_loading)
            const Center(child: Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator()))
          else if (_error != null)
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  children: [
                    Text(_error!, textAlign: TextAlign.center),
                    const SizedBox(height: 8),
                    FilledButton(onPressed: _load, child: const Text('Thu lai')),
                  ],
                ),
              ),
            )
          else
            ..._types.map(_vehicleCard),
          const SizedBox(height: 28),
          _sectionTitle('Uu diem', 'Tai sao chon chung toi'),
          const SizedBox(height: 12),
          GridView.count(
            crossAxisCount: 2,
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            mainAxisSpacing: 10,
            crossAxisSpacing: 10,
            childAspectRatio: 1.15,
            children: PortalContent.features.map(_featureCard).toList(),
          ),
          const SizedBox(height: 28),
          _sectionTitle('Quy trinh', 'Dat xe chi 4 buoc'),
          const SizedBox(height: 12),
          ...PortalContent.steps.map(_stepCard),
          const SizedBox(height: 28),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(20),
            decoration: BoxDecoration(
              gradient: const LinearGradient(colors: [Color(0xFF1E3A8A), Color(0xFF7C3AED)]),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Column(
              children: [
                const Text(
                  'San sang cho chuyen di?',
                  style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 8),
                const Text(
                  'Dat xe online trong vai phut',
                  style: TextStyle(color: Colors.white70),
                ),
                const SizedBox(height: 16),
                FilledButton(
                  style: FilledButton.styleFrom(backgroundColor: Colors.white, foregroundColor: const Color(0xFF1E3A8A)),
                  onPressed: () {
                    if (widget.isLoggedIn && _types.isNotEmpty) {
                      _bookType(_types.first);
                    } else {
                      Navigator.push(context, MaterialPageRoute(builder: (_) => const LoginScreen()));
                    }
                  },
                  child: Text(widget.isLoggedIn ? 'Dat xe ngay' : 'Dang nhap / Dang ky'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _sectionTitle(String title, String subtitle) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(title, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
        Text(subtitle, style: const TextStyle(color: Color(0xFF64748B), fontSize: 13)),
      ],
    );
  }

  Widget _routeCard(PopularRoute route) {
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE2E8F0)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Text(route.icon, style: const TextStyle(fontSize: 28)),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(route.title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
                      Text('${route.from} → ${route.to}', style: const TextStyle(color: Color(0xFF2563EB), fontWeight: FontWeight.w600)),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            Text(route.description, style: const TextStyle(color: Color(0xFF64748B), fontSize: 13)),
            const SizedBox(height: 10),
            Wrap(
              spacing: 6,
              runSpacing: 6,
              children: [
                _chip('${route.distanceKm} km'),
                _chip(route.duration),
                _chip(route.suggestedVehicle),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Tu ${_money(route.estimatedPrice)} VND',
                    style: const TextStyle(fontWeight: FontWeight.w800, color: Color(0xFF2563EB)),
                  ),
                ),
                FilledButton(
                  onPressed: () => _bookRoute(route),
                  style: FilledButton.styleFrom(
                    backgroundColor: const Color(0xFF2563EB),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                  ),
                  child: Text(widget.isLoggedIn ? 'Dat tuyen' : 'Dang nhap'),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _vehicleCard(VehicleType type) {
    return Card(
      margin: const EdgeInsets.only(bottom: 10),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(16),
        side: const BorderSide(color: Color(0xFFE2E8F0)),
      ),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        title: Text(type.typeName, style: const TextStyle(fontWeight: FontWeight.bold)),
        subtitle: Text(
          '${type.seatCapacity} cho\n${_money(type.pricePerDay)} VND/ngay · ${_money(type.pricePerKm)} VND/km',
        ),
        isThreeLine: true,
        trailing: const Icon(Icons.chevron_right, color: Color(0xFF2563EB)),
        onTap: () => _bookType(type),
      ),
    );
  }

  Widget _featureCard(FeatureItem f) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(f.icon, style: const TextStyle(fontSize: 22)),
          const SizedBox(height: 6),
          Text(f.title, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13)),
          const SizedBox(height: 4),
          Expanded(
            child: Text(f.description, style: const TextStyle(fontSize: 11, color: Color(0xFF64748B))),
          ),
        ],
      ),
    );
  }

  Widget _stepCard(ProcessStep step) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(14),
        side: const BorderSide(color: Color(0xFFE2E8F0)),
      ),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: const Color(0xFF2563EB),
          child: Text('${step.step}', style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
        ),
        title: Text(step.title, style: const TextStyle(fontWeight: FontWeight.w700)),
        subtitle: Text(step.description),
      ),
    );
  }

  Widget _chip(String text) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: const Color(0xFFEEF2FF),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(text, style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: Color(0xFF1D4ED8))),
    );
  }
}
