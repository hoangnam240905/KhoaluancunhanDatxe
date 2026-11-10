import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'account_screen.dart';
import 'bookings_screen.dart';
import 'home_screen.dart';

class CustomerShell extends StatefulWidget {
  final ApiService api;
  final String? userName;
  final int initialIndex;

  const CustomerShell({
    super.key,
    required this.api,
    this.userName,
    this.initialIndex = 0,
  });

  bool get isLoggedIn => userName != null && userName!.isNotEmpty;

  @override
  State<CustomerShell> createState() => _CustomerShellState();
}

class _CustomerShellState extends State<CustomerShell> {
  late int _index;
  final Set<int> _loadedIndices = <int>{};

  @override
  void initState() {
    super.initState();
    _index = widget.initialIndex;
    _loadedIndices.add(_index);
    if (widget.isLoggedIn) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) widget.api.connectRealtime();
      });
    }
  }

  @override
  void dispose() {
    widget.api.disconnectRealtime();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: IndexedStack(
        index: _index,
        children: [
          _loadedIndices.contains(0)
              ? HomeScreen(api: widget.api, userName: widget.userName)
              : const SizedBox.shrink(),
          _loadedIndices.contains(1)
              ? BookingsScreen(api: widget.api, isLoggedIn: widget.isLoggedIn)
              : const SizedBox.shrink(),
          _loadedIndices.contains(2)
              ? AccountScreen(api: widget.api, isLoggedIn: widget.isLoggedIn)
              : const SizedBox.shrink(),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) {
          setState(() {
            _index = i;
            _loadedIndices.add(i);
          });
        },
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.home_outlined),
            selectedIcon: Icon(Icons.home),
            label: 'Trang chủ',
          ),
          NavigationDestination(
            icon: Icon(Icons.receipt_long_outlined),
            selectedIcon: Icon(Icons.receipt_long),
            label: 'Đơn thuê',
          ),
          NavigationDestination(
            icon: Icon(Icons.person_outline),
            selectedIcon: Icon(Icons.person),
            label: 'Tài khoản',
          ),
        ],
      ),
    );
  }
}
