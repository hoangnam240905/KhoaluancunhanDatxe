import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'account_screen.dart';
import 'status_screen.dart';
import 'trips_screen.dart';

class DriverShell extends StatefulWidget {
  final ApiService api;
  final String userName;

  const DriverShell({super.key, required this.api, required this.userName});

  @override
  State<DriverShell> createState() => _DriverShellState();
}

class _DriverShellState extends State<DriverShell> {
  int _index = 0;

  @override
  void initState() {
    super.initState();
    widget.api.connectRealtime();
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
          TripsScreen(api: widget.api, userName: widget.userName),
          StatusScreen(api: widget.api),
          AccountScreen(api: widget.api, userName: widget.userName),
        ],
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(
            icon: Icon(Icons.route_outlined),
            selectedIcon: Icon(Icons.route),
            label: 'Chuyến',
          ),
          NavigationDestination(
            icon: Icon(Icons.toggle_on_outlined),
            selectedIcon: Icon(Icons.toggle_on),
            label: 'Trạng thái',
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
