import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import '../config/api_config.dart';

/// Minimal SignalR JSON client (negotiate + WebSocket). Avoids extra packages.
class RealtimeService {
  /// DEMO TEMP: SignalR client off. Set true next week to restore.
  static const bool enabled = false;

  static const _rs = '\u001e';

  WebSocket? _socket;
  final List<VoidCallback> _listeners = [];
  bool _closedByUs = false;
  int _retry = 0;
  String? _token;
  bool _reloadOnOpen = false;
  bool _connecting = false;

  void addListener(VoidCallback cb) {
    if (!_listeners.contains(cb)) _listeners.add(cb);
  }

  void removeListener(VoidCallback cb) => _listeners.remove(cb);

  void _notify() {
    for (final cb in List<VoidCallback>.from(_listeners)) {
      cb();
    }
  }

  Future<void> connect(String token) async {
    if (!enabled) return;
    _token = token;
    _closedByUs = false;
    if (kIsWeb) return;
    await _open();
  }

  Future<void> disconnect() async {
    _closedByUs = true;
    _retry = 0;
    await _socket?.close();
    _socket = null;
  }

  Future<void> _open() async {
    if (!enabled || kIsWeb || _token == null || _connecting) return;
    _connecting = true;
    try {
      final negotiate = await http.post(
        Uri.parse('${ApiConfig.baseUrl}/hubs/realtime/negotiate?negotiateVersion=1'),
        headers: {'Authorization': 'Bearer $_token'},
      );
      if (negotiate.statusCode != 200) {
        throw Exception('negotiate ${negotiate.statusCode}');
      }
      final body = jsonDecode(negotiate.body) as Map<String, dynamic>;
      final connectionToken =
          (body['connectionToken'] ?? body['connectionId']) as String?;
      if (connectionToken == null || connectionToken.isEmpty) {
        throw Exception('missing connection token');
      }

      final wsBase = ApiConfig.baseUrl.startsWith('https')
          ? ApiConfig.baseUrl.replaceFirst('https', 'wss')
          : ApiConfig.baseUrl.replaceFirst('http', 'ws');
      final wsUrl =
          '$wsBase/hubs/realtime?id=${Uri.encodeComponent(connectionToken)}&access_token=${Uri.encodeComponent(_token!)}';
      final socket = await WebSocket.connect(wsUrl);
      _socket = socket;
      _retry = 0;
      socket.add('{"protocol":"json","version":1}$_rs');
      if (_reloadOnOpen) {
        _reloadOnOpen = false;
        _notify();
      }
      socket.listen(
        _onData,
        onDone: _onDone,
        onError: (_) => socket.close(),
        cancelOnError: true,
      );
    } catch (_) {
      _scheduleReconnect(reload: false);
    } finally {
      _connecting = false;
    }
  }

  void _onData(dynamic data) {
    final text = data is String ? data : utf8.decode(data as List<int>);
    for (final raw in text.split(_rs)) {
      if (raw.isEmpty) continue;
      Map<String, dynamic> msg;
      try {
        msg = jsonDecode(raw) as Map<String, dynamic>;
      } catch (_) {
        continue;
      }
      final type = msg['type'];
      if (type == 6) {
        _socket?.add('{"type":7}$_rs');
        continue;
      }
      if (type == 1 && msg['target'] == 'ReceiveEvent') {
        _notify();
      }
    }
  }

  void _onDone() {
    if (!_closedByUs) _scheduleReconnect(reload: true);
  }

  void _scheduleReconnect({required bool reload}) {
    _retry += 1;
    if (_retry > 8) return;
    if (reload) _reloadOnOpen = true;
    final delay = Duration(milliseconds: (_retry * 1000).clamp(1000, 5000));
    Timer(delay, () {
      if (!_closedByUs) _open();
    });
  }
}
