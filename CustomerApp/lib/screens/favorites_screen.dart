import 'package:flutter/material.dart';

import '../models/models.dart';
import '../services/api_service.dart';
import '../utils/formatters.dart';
import '../widgets/app_widgets.dart';

class FavoritesScreen extends StatefulWidget {
  final ApiService api;

  const FavoritesScreen({super.key, required this.api});

  @override
  State<FavoritesScreen> createState() => _FavoritesScreenState();
}

class _FavoritesScreenState extends State<FavoritesScreen> {
  List<FavoriteVehicle> _items = [];
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
      _items = await widget.api.getFavorites();
    } catch (e) {
      _error = e.toString().replaceFirst('Exception: ', '');
    }
    if (mounted) setState(() => _loading = false);
  }

  Future<void> _remove(FavoriteVehicle item) async {
    try {
      await widget.api.setFavorite(item.vehicleId, false);
      if (mounted) {
        setState(
          () => _items.removeWhere((x) => x.vehicleId == item.vehicleId),
        );
      }
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
    return Scaffold(
      appBar: AppBar(title: const Text('Xe yêu thích')),
      body: _loading
          ? const Center(child: AppLoading(message: 'Đang tải xe yêu thích...'))
          : _error != null
          ? AppErrorState(message: _error!, onRetry: _load)
          : RefreshIndicator(
              onRefresh: _load,
              child: _items.isEmpty
                  ? ListView(
                      children: const [
                        SizedBox(height: 120),
                        AppEmptyState(
                          icon: Icons.favorite_border,
                          title: 'Chưa có xe yêu thích',
                          subtitle:
                              'Nhấn biểu tượng tim ở chi tiết xe để lưu lại.',
                        ),
                      ],
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(16),
                      itemCount: _items.length,
                      separatorBuilder: (_, _) => const SizedBox(height: 12),
                      itemBuilder: (_, index) {
                        final item = _items[index];
                        return Card(
                          child: ListTile(
                            contentPadding: const EdgeInsets.all(12),
                            leading: SizedBox(
                              width: 76,
                              height: 64,
                              child: VehicleImage(
                                imageUrl: item.primaryImageUrl,
                                typeName: item.typeName,
                                seatCapacity: item.seatCapacity,
                                height: 64,
                                borderRadius: BorderRadius.circular(10),
                              ),
                            ),
                            title: Text(
                              '${item.brand} ${item.model}',
                              style: const TextStyle(
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            subtitle: Text(
                              '${item.typeName} · ${item.seatCapacity} chỗ\n${Formatters.vnd(item.pricePerDay)}/ngày',
                            ),
                            trailing: IconButton(
                              tooltip: 'Bỏ yêu thích',
                              onPressed: () => _remove(item),
                              icon: const Icon(
                                Icons.favorite,
                                color: Colors.redAccent,
                              ),
                            ),
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}
