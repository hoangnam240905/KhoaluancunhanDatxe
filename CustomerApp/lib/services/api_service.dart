import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import '../config/api_config.dart';
import '../models/models.dart';
import '../utils/formatters.dart';
import 'realtime_service.dart';

class AuthService {
  static const _tokenKey = 'auth_token';
  static const _userKey = 'auth_user';

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
  final RealtimeService realtime = RealtimeService();

  Future<void> connectRealtime() async {
    final token = await authService.getToken();
    if (token != null) await realtime.connect(token);
  }

  Future<void> disconnectRealtime() async {
    await realtime.disconnect();
  }

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
        final errors = decoded['errors'];
        if (errors is Map) {
          final parts = <String>[];
          for (final value in errors.values) {
            if (value is List) {
              parts.addAll(value.map((e) => e.toString()));
            } else {
              parts.add(value.toString());
            }
          }
          if (parts.isNotEmpty) return parts.join('\n');
        }
        final title = decoded['title'] as String?;
        if (title != null && title.isNotEmpty) return title;
      }
    } catch (_) {}
    return 'Lỗi ${response.statusCode}';
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
    await authService.saveAuth(auth);
    return auth;
  }

  Future<void> changePassword({
    required String oldPassword,
    required String newPassword,
  }) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/change-password'),
      headers: await _headers(auth: true),
      body: jsonEncode({
        'oldPassword': oldPassword,
        'newPassword': newPassword,
      }),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }

  Future<RegisterPendingResponse> register({
    required String email,
    required String password,
    required String fullName,
    String? phone,
    String? address,
    String? confirmPassword,
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
        'confirmPassword': confirmPassword,
      }),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return RegisterPendingResponse.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<AuthResponse> verifyEmail({
    required String email,
    required String otp,
  }) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/verify-email'),
      headers: await _headers(),
      body: jsonEncode({'email': email, 'otp': otp}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final auth = AuthResponse.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
    await authService.saveAuth(auth);
    return auth;
  }

  Future<String> resendVerificationOtp(String email) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/resend-verification-otp'),
      headers: await _headers(),
      body: jsonEncode({'email': email}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return json['message'] as String? ?? 'Đã gửi mã OTP.';
  }

  Future<String> forgotPassword(String email) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/forgot-password'),
      headers: await _headers(),
      body: jsonEncode({'email': email}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return json['message'] as String? ??
        'Nếu email tồn tại, mã OTP đã được gửi.';
  }

  Future<void> resetPassword({
    required String email,
    required String otp,
    required String newPassword,
    required String confirmPassword,
  }) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/reset-password'),
      headers: await _headers(),
      body: jsonEncode({
        'email': email,
        'otp': otp,
        'newPassword': newPassword,
        'confirmPassword': confirmPassword,
      }),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }

  Future<LoginOptionsResponse> getLoginOptions() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/login-options'),
      headers: await _headers(),
    );
    if (response.statusCode != 200) {
      return LoginOptionsResponse(googleEnabled: false);
    }
    return LoginOptionsResponse.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<AuthResponse> googleLogin(String idToken) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/auth/google'),
      headers: await _headers(),
      body: jsonEncode({'idToken': idToken}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final auth = AuthResponse.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
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

  Future<List<VehicleType>> getVehicleTypes() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/vehicle-types'),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((e) => VehicleType.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<Vehicle>> getVehicles({String? status}) async {
    final uri = Uri.parse('${ApiConfig.baseUrl}/api/vehicles').replace(
      queryParameters: status == null ? null : {'status': status},
    );
    final response = await http.get(uri);
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((e) => Vehicle.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<List<VehicleTypeRecommendation>> getRecommended({
    required DateTime startDate,
    required DateTime endDate,
    int? seats,
    double? priceMax,
    double? estimatedDistance,
  }) async {
    final params = <String, String>{
      'startDate': Formatters.rentalQuery(startDate),
      'endDate': Formatters.rentalQuery(endDate),
    };
    if (seats != null) params['seats'] = '$seats';
    if (priceMax != null) params['priceMax'] = '$priceMax';
    if (estimatedDistance != null) {
      params['estimatedDistance'] = '$estimatedDistance';
    }
    final response = await http.get(
      Uri.parse(
        '${ApiConfig.baseUrl}/api/vehicle-types/recommended',
      ).replace(queryParameters: params),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map(
          (e) => VehicleTypeRecommendation.fromJson(e as Map<String, dynamic>),
        )
        .toList();
  }

  Future<List<Booking>> getBookings() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((e) => Booking.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<Booking> getBooking(int id) async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings/$id'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return Booking.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<BookingQuote> getQuote({
    required int vehicleTypeId,
    required DateTime startDate,
    required DateTime endDate,
    required String rentalMode,
    double? estimatedDistance,
  }) async {
    final params = <String, String>{
      'vehicleTypeId': '$vehicleTypeId',
      'startDate': Formatters.rentalQuery(startDate),
      'endDate': Formatters.rentalQuery(endDate),
      'rentalMode': rentalMode,
    };
    if (estimatedDistance != null) {
      params['estimatedDistance'] = '$estimatedDistance';
    }
    final response = await http.get(
      Uri.parse(
        '${ApiConfig.baseUrl}/api/bookings/quote',
      ).replace(queryParameters: params),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return BookingQuote.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<Booking> createBooking({
    required int vehicleTypeId,
    required String rentalMode,
    required String pickupAddress,
    required String dropoffAddress,
    required DateTime startDate,
    required DateTime endDate,
    double? estimatedDistance,
    String? notes,
    bool fromRecommendation = false,
    int? vehicleId,
  }) async {
    final uri = Uri.parse('${ApiConfig.baseUrl}/api/bookings').replace(
      queryParameters: fromRecommendation
          ? {'fromRecommendation': 'true'}
          : null,
    );
    final response = await http.post(
      uri,
      headers: await _headers(auth: true),
      body: jsonEncode({
        'vehicleTypeId': vehicleTypeId,
        'rentalMode': rentalMode,
        'pickupAddress': pickupAddress,
        'dropoffAddress': dropoffAddress,
        'startDate': Formatters.rentalQuery(startDate),
        'endDate': Formatters.rentalQuery(endDate),
        'estimatedDistance': estimatedDistance,
        'notes': notes,
        if (vehicleId != null) 'vehicleId': vehicleId,
      }),
    );
    if (response.statusCode != 201 && response.statusCode != 200) {
      throw Exception(_errorMessage(response));
    }
    return Booking.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<void> createReview({
    required int bookingId,
    required int rating,
    String? comment,
  }) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/reviews'),
      headers: await _headers(auth: true),
      body: jsonEncode({'rating': rating, 'comment': comment}),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }

  Future<List<Payment>> getBookingPayments(int bookingId) async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/payments'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((e) => Payment.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<Payment> createDepositPayment({
    required int bookingId,
    required String method,
    String? transactionRef,
  }) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/payments'),
      headers: await _headers(auth: true),
      body: jsonEncode(
        Payment.depositCreateBody(
          bookingId: bookingId,
          method: method,
          transactionRef: transactionRef,
        ),
      ),
    );
    if (response.statusCode != 201 && response.statusCode != 200) {
      throw Exception(_errorMessage(response));
    }
    return Payment.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<RentalContract?> getContract(int bookingId) async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/contract'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode == 404) return null;
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return RentalContract.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<RentalContract> createContract(int bookingId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/bookings/$bookingId/contract'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 201 && response.statusCode != 200) {
      throw Exception(_errorMessage(response));
    }
    return RentalContract.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<RentalContract> simulateSignContract(int contractId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/contracts/$contractId/simulate-sign'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return RentalContract.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<Payment> simulatePaymentSuccess(int paymentId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/payments/$paymentId/simulate-success'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return Payment.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<Payment> simulatePaymentFailure(int paymentId) async {
    final response = await http.post(
      Uri.parse('${ApiConfig.baseUrl}/api/payments/$paymentId/simulate-failure'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    return Payment.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<List<DriverBooking>> getMyTrips() async {
    final response = await http.get(
      Uri.parse('${ApiConfig.baseUrl}/api/drivers/me/trips'),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
    final list = jsonDecode(response.body) as List<dynamic>;
    return list
        .map((e) => DriverBooking.fromJson(e as Map<String, dynamic>))
        .toList();
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
      Uri.parse(
        '${ApiConfig.baseUrl}/api/drivers/trips/$assignmentId/complete',
      ),
      headers: await _headers(auth: true),
    );
    if (response.statusCode != 200) throw Exception(_errorMessage(response));
  }
}
