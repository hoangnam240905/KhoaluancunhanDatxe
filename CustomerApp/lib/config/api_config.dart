import 'package:flutter/foundation.dart';

class ApiConfig {
  /// Android emulator: 10.0.2.2 maps to the host machine.
  /// Windows / iOS simulator / desktop: localhost.
  static String get baseUrl {
    if (kIsWeb) return 'http://localhost:5199';
    switch (defaultTargetPlatform) {
      case TargetPlatform.android:
        return 'http://10.0.2.2:5199';
      default:
        return 'http://localhost:5199';
    }
  }

  static String? resolveImageUrl(String? url) {
    if (url == null || url.trim().isEmpty) return null;
    final trimmed = url.trim();
    if (trimmed.startsWith('http://') || trimmed.startsWith('https://')) {
      return trimmed;
    }
    if (trimmed.startsWith('/')) return '$baseUrl$trimmed';
    return '$baseUrl/$trimmed';
  }
}
