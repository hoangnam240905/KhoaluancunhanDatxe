import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import '../config/api_config.dart';
import '../models/models.dart';

class AuthService {
  static const _tokenKey = 'driver_auth_token';
  static const _userKey = 'driver_auth_user';

  Future<void> saveAuth(AuthResponse auth) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, auth.token);
    await prefs.setString(
      _userKey,
      jsonEncode({
        'userId': auth.userId,
        'email': auth.email,
        'fullName': auth.fullName,
        'role': auth.role,
        'expiresAt': auth.expiresAt.toIso8601String(),
      }),
    );
  }

  Future<String?> getToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_tokenKey);
  }

  Future<AuthResponse?> getAuth() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_tokenKey);
    final userJson = prefs.getString(_userKey);
    if (token == null || userJson == null) return null;
    final user = jsonDecode(userJson) as Map<String, dynamic>;
    return AuthResponse(
      token: token,
      userId: user['userId'] as int,
      email: user['email'] as String,
      fullName: user['fullName'] as String,
      role: user['role'] as String,
      expiresAt: DateTime.parse(user['expiresAt'] as String),
    );
  }

  Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove(_userKey);
  }
}

class ApiService {
  final AuthService authService = AuthService();

  Future<Map<String, String>> _headers({bool auth = false}) async {
    final headers = {'Content-Type': 'application/json'};
    if (auth) {
      final token = await authService.getToken();
      if (token != null) headers['Authorization'] = 'Bearer $token';
    }
    return headers;
  }

  String _errorMessage(http.Response response) {
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) {
        final message = decoded['message'] as String?;
        if (message != null && message.isNotEmpty) return message;
        final title = decoded['title'] as String?;
        if (title != null && title.isNotEmpty) return title;
      }
    } catch (_) {}
    if (response.statusCode == 400) {
      return 'Yêu cầu không hợp lệ. Kiểm tra trạng thái chuyến và thử lại.';
    }
    if (response.statusCode == 401) {
      return 'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.';
    }
    return 'Lỗi ${response.statusCode}';
  }

  String _successMessage(http.Response response, String fallback) {
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) {
        final message = decoded['message'] as String?;
        if (message != null && message.isNotEmpty) return message;
      }
    } catch (_) {}
    return fallback;
  }

  Future<AuthResponse> login(String email, String password) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/login'),
      headers: await _headers(),
      body: jsonEncode({'email': email, 'password': password}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final auth = AuthResponse.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
    if (auth.role != 'Driver') {
      throw Exception(
        'Tài khoản này không phải tài xế. DriverApp chỉ dành cho tài xế.',
      );
    }
    await authService.saveAuth(auth);
    return auth;
  }

  Future<UserProfile> getMe() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/me'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return UserProfile.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<DriverProfile> getMyDriverProfile() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/me'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return DriverProfile.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<List<DriverBooking>> getMyTrips() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/me/trips'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body);
    if (list is! List) return [];
    final parsed = <DriverBooking>[];
    for (final item in list) {
      if (item is Map<String, dynamic>) {
        parsed.add(DriverBooking.fromJson(item));
      }
    }
    return TripFilters.forDriver(parsed);
  }

  Future<DriverProfile> updateStatus(String status) async {
    final response = await http.patch(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/me/status'),
      headers: await _headers(auth: true),
      body: jsonEncode({'status': status}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return DriverProfile.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<String> acceptTrip(int assignmentId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/trips/$assignmentId/accept'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return _successMessage(response, 'Đã nhận chuyến.');
  }

  Future<String> startTrip(int assignmentId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/trips/$assignmentId/start'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return _successMessage(response, 'Đã bắt đầu chuyến.');
  }

  Future<String> completeTrip(int assignmentId) async {
    final response = await http.post(
      Uri.parse(
        '${ApiConfig.baseUrl}/api/drivers/trips/$assignmentId/complete',
      ),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return _successMessage(response, 'Đã hoàn thành chuyến.');
  }
}
