import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import '../config/api_config.dart';
import '../models/models.dart';

class AuthService {
  static const _tokenKey = 'auth_token';
  static const _userKey = 'auth_user';

  Future<void> saveAuth(AuthResponse auth) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, auth.token);
    await prefs.setString(_userKey, jsonEncode({
      'userId': auth.userId,
      'email': auth.email,
      'fullName': auth.fullName,
      'role': auth.role,
      'expiresAt': auth.expiresAt.toIso8601String(),
    }));
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
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      return body['message'] as String? ?? 'Loi ${response.statusCode}';
    } catch (_) {
      return 'Loi ${response.statusCode}';
    }
  }

  Future<AuthResponse> login(String email, String password) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/login'),
      headers: await _headers(),
      body: jsonEncode({'email': email, 'password': password}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final auth = AuthResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    await authService.saveAuth(auth);
    return auth;
  }

  Future<AuthResponse> register({
    required String email,
    required String password,
    required String fullName,
    String? phone,
    String? address,
  }) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/register'),
      headers: await _headers(),
      body: jsonEncode({
        'email': email,
        'password': password,
        'fullName': fullName,
        'phone': phone,
        'address': address,
      }),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final auth = AuthResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    await authService.saveAuth(auth);
    return auth;
  }

  Future<List<VehicleType>> getVehicleTypes() async {
    final response = await http.get(Uri.parse('${ApiConfig.baseUrl}/api/vehicle-types'));
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list.map((e) => VehicleType.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Booking>> getBookings() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list.map((e) => Booking.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<Booking> createBooking({
    required int vehicleTypeId,
    required String pickupAddress,
    required String dropoffAddress,
    required DateTime startDate,
    required DateTime endDate,
    double? estimatedDistance,
    String? notes,
  }) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings'),
      headers: await _headers(auth: true),
      body: jsonEncode({
        'vehicleTypeId': vehicleTypeId,
        'pickupAddress': pickupAddress,
        'dropoffAddress': dropoffAddress,
        'startDate': startDate.toIso8601String(),
        'endDate': endDate.toIso8601String(),
        'estimatedDistance': estimatedDistance,
        'notes': notes,
      }),
    );
    if (response.statusCode != 201 && response.statusCode != 200) {
      throw Exception(_errorMessage(response));
    }
    return Booking.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<List<DriverBooking>> getMyTrips() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/me/trips'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list.map((e) => DriverBooking.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> updateDriverStatus(String status) async {
    final response = await http.patch(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/me/status'),
      headers: await _headers(auth: true),
      body: jsonEncode({'status': status}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }

  Future<void> acceptTrip(int assignmentId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/trips/$assignmentId/accept'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }

  Future<void> startTrip(int assignmentId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/trips/$assignmentId/start'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }

  Future<void> completeTrip(int assignmentId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/trips/$assignmentId/complete'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }
}
