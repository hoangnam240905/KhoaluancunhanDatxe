import 'package:http/http.dart' as base;

export 'package:http/http.dart' show Response, MultipartRequest, MultipartFile;

/// Same calls as package:http, but every request gives up after [requestTimeout] so a hung network
/// shows an error instead of leaving the app waiting forever. Screens already show
/// `e.toString()` without the "Exception: " prefix, so the message below is what the driver sees.
const requestTimeout = Duration(seconds: 20);
const timeoutMessage =
    'Máy chủ không phản hồi. Kiểm tra kết nối mạng rồi thử lại.';

Future<T> _timed<T>(Future<T> request) => request.timeout(
      requestTimeout,
      onTimeout: () => throw Exception(timeoutMessage),
    );

Future<base.Response> get(Uri url, {Map<String, String>? headers}) =>
    _timed(base.get(url, headers: headers));

Future<base.Response> post(Uri url, {Map<String, String>? headers, Object? body}) =>
    _timed(base.post(url, headers: headers, body: body));

Future<base.Response> put(Uri url, {Map<String, String>? headers, Object? body}) =>
    _timed(base.put(url, headers: headers, body: body));

Future<base.Response> patch(Uri url, {Map<String, String>? headers, Object? body}) =>
    _timed(base.patch(url, headers: headers, body: body));

Future<base.Response> delete(Uri url, {Map<String, String>? headers, Object? body}) =>
    _timed(base.delete(url, headers: headers, body: body));

/// Multipart uploads (licence photos) get a longer limit.
Future<base.StreamedResponse> send(base.BaseRequest request) => request
    .send()
    .timeout(const Duration(seconds: 60), onTimeout: () => throw Exception(timeoutMessage));
