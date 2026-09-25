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

  @override
  void initState() {
    super.initState();
    _index = widget.initialIndex;
    if (widget.isLoggedIn) widget.api.connectRealtime();
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
          HomeScreen(api: widget.api, userName: widget.userName),
          BookingsScreen(api: widget.api, isLoggedIn: widget.isLoggedIn),
          AccountScreen(api: widget.api, isLoggedIn: widget.isLoggedIn),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
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
