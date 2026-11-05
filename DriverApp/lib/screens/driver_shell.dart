import 'package:flutter/material.dart';
import '../services/api_service.dart';
import 'account_screen.dart';
import 'incidents_screen.dart';
import 'schedule_screen.dart';
import 'status_screen.dart';
import 'trips_screen.dart';

class DriverShell extends StatefulWidget {
  final ApiService api;
  final String userName;
  final bool darkMode;
  final ValueChanged<bool> onDarkModeChanged;

  const DriverShell({
    super.key,
    required this.api,
    required this.userName,
    required this.darkMode,
    required this.onDarkModeChanged,
  });

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
  Widget build(BuildContext context) => Scaffold(
    body: IndexedStack(
      index: _index,
      children: [
        TripsScreen(api: widget.api, userName: widget.userName),
        ScheduleScreen(api: widget.api),
        IncidentsScreen(api: widget.api),
        StatusScreen(api: widget.api),
        AccountScreen(
          api: widget.api,
          userName: widget.userName,
          darkMode: widget.darkMode,
          onDarkModeChanged: widget.onDarkModeChanged,
        ),
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
          icon: Icon(Icons.calendar_month_outlined),
          selectedIcon: Icon(Icons.calendar_month),
          label: 'Lịch',
        ),
        NavigationDestination(
          icon: Icon(Icons.report_gmailerrorred_outlined),
          selectedIcon: Icon(Icons.report),
          label: 'Sự cố',
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
