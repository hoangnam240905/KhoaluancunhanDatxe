import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';
import 'create_booking_screen.dart';

/// "Xe yêu thích" — same list as the web page /Customer/Favorites (GET /api/favorites).
/// Cars are added with the heart in the booking screen's vehicle list.
class FavoritesScreen extends StatefulWidget {
  final ApiService api;

  const FavoritesScreen({super.key, required this.api});

  @override
  State<FavoritesScreen> createState() => _FavoritesScreenState();
}

class _FavoritesScreenState extends State<FavoritesScreen> {
  List<FavoriteVehicle> _items = [];
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
      final items = await widget.api.getFavorites();
      final types = _types.isEmpty ? await widget.api.getVehicleTypes() : _types;
      if (!mounted) return;
      setState(() {
        _items = items;
        _types = types;
      });
    } catch (e) {
      if (mounted) setState(() => _error = e.toString().replaceFirst('Exception: ', ''));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _remove(FavoriteVehicle v) async {
    try {
      await widget.api.removeFavorite(v.vehicleId);
      if (!mounted) return;
      setState(() => _items.removeWhere((x) => x.vehicleId == v.vehicleId));
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Đã bỏ ${v.title} khỏi xe yêu thích.')),
      );
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
  }

  void _book(FavoriteVehicle v) {
    VehicleType? type;
    for (final t in _types) {
      if (t.typeId == v.typeId) type = t;
    }
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => CreateBookingScreen(
          api: widget.api,
          vehicleType: type,
          vehicleTypes: _types.isEmpty ? null : _types,
          initialVehicleId: v.vehicleId,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Xe yêu thích')),
      body: RefreshIndicator(onRefresh: _load, child: _body()),
    );
  }

  Widget _body() {
    if (_loading) return const AppLoading(message: 'Đang tải xe yêu thích...');
    if (_error != null) return AppErrorState(message: _error!, onRetry: _load);
    if (_items.isEmpty) {
      return ListView(
        children: const [
          SizedBox(height: 60),
          AppEmptyState(
            icon: Icons.favorite_border,
            title: 'Chưa có xe yêu thích',
            subtitle: 'Khi đặt xe, bấm biểu tượng trái tim cạnh một xe để lưu lại.',
          ),
        ],
      );
    }
    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _items.length,
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (_, i) {
        final v = _items[i];
        return AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  const Icon(Icons.directions_car_filled_outlined, color: AppColors.primary),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(v.title, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                  ),
                  IconButton(
                    tooltip: 'Bỏ yêu thích',
                    icon: const Icon(Icons.favorite, color: Colors.redAccent),
                    onPressed: () => _remove(v),
                  ),
                ],
              ),
              Text(
                [
                  v.licensePlate,
                  v.typeName,
                  if (v.year > 0) 'Đời ${v.year}',
                  if ((v.color ?? '').isNotEmpty) v.color!,
                ].join(' · '),
                style: const TextStyle(color: AppColors.muted),
              ),
              if (v.pricePerDay > 0) ...[
                const SizedBox(height: 4),
                Text('Từ ${Formatters.vnd(v.pricePerDay)}/ngày', style: const TextStyle(fontWeight: FontWeight.w700)),
              ],
              const SizedBox(height: 10),
              Align(
                alignment: Alignment.centerRight,
                child: FilledButton.icon(
                  onPressed: () => _book(v),
                  icon: const Icon(Icons.event_available_outlined),
                  label: const Text('Đặt xe này'),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}
