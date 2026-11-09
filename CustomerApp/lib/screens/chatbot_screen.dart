import 'package:flutter/material.dart';
import '../models/models.dart';
import '../services/api_service.dart';
import '../theme/app_theme.dart';
import '../utils/formatters.dart';
import 'create_booking_screen.dart';

class ChatbotScreen extends StatefulWidget {
  final ApiService api;
  final List<VehicleType> vehicleTypes;

  const ChatbotScreen({
    super.key,
    required this.api,
    required this.vehicleTypes,
  });

  @override
  State<ChatbotScreen> createState() => _ChatbotScreenState();
}

class _ChatbotScreenState extends State<ChatbotScreen> {
  final _message = TextEditingController();
  final _scroll = ScrollController();

  static const _suggestions = [
    'Từ ngày 22 đến 25 còn xe 7 chỗ nào?',
    'Xe 4 chỗ dưới 1 triệu/ngày',
    'Tôi cần xe cho 6 người',
    'Có xe SUV gầm cao không?',
    'Xe nào tiết kiệm nhất?',
    'So sánh tự lái và có tài xế',
    'Tôi muốn thuê xe 3 ngày',
    'Giá thuê xe tính như thế nào?',
    'Cách đặt xe như thế nào?',
    'Tiền cọc bao nhiêu?',
    'Có những phương thức thanh toán nào?',
    'Tôi muốn hủy đơn',
    'Tôi muốn thuê lại chuyến cũ',
    'Ký hợp đồng online thế nào?',
    'Cần giấy tờ và bằng lái gì?',
    'Có giao xe tận nơi không?',
    'Trả xe muộn thì sao?',
    'Có thể trả xe ở điểm khác không?',
    'Nếu xe gặp sự cố thì làm gì?',
    'Bao lâu được hoàn tiền cọc?',
    'Có cho thuê xe máy không?',
    'Có thể thuê nhiều xe cùng lúc không?',
    'Làm sao để đăng ký tài khoản?',
    'Tôi quên mật khẩu phải làm sao?',
    'Làm sao để đánh giá chuyến đi?',
    'Địa chỉ văn phòng ở đâu?',
    'Giờ làm việc của DriveX?',
    'Tôi cần hỗ trợ, liên hệ thế nào?',
  ];

  final List<_ChatMessage> _messages = [
    const _ChatMessage(
      text:
          'Xin chào! Tôi là Trợ lý ảo AI của DriveX 🚗✨\n\n'
          'Tôi có thể hỗ trợ bạn:\n'
          '• Gợi ý xe phù hợp theo số chỗ & mức ngân sách\n'
          '• Tư vấn thuê xe tự lái hoặc có tài xế\n'
          '• Hướng dẫn đặt cọc, bảng giá, hợp đồng & giao xe\n\n'
          'Bạn có thể chọn nhanh các câu hỏi gợi ý bên dưới hoặc nhập câu hỏi trực tiếp nhé!',
      fromBot: true,
    ),
  ];

  bool _sending = false;

  @override
  void dispose() {
    _message.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) {
        _scroll.animateTo(
          _scroll.position.maxScrollExtent,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeOut,
        );
      }
    });
  }

  Future<void> _ask() async {
    final text = _message.text.trim();
    if (text.isEmpty || _sending) return;
    _message.clear();
    setState(() {
      _messages.add(_ChatMessage(text: text, fromBot: false));
      _sending = true;
    });
    _scrollToBottom();

    try {
      final reply = await widget.api.askChatbot(text);
      if (!mounted) return;
      setState(() {
        _messages.add(
          _ChatMessage(text: reply.reply, fromBot: true, reply: reply),
        );
        _sending = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _messages.add(
          _ChatMessage(
            text: e.toString().replaceFirst('Exception: ', ''),
            fromBot: true,
          ),
        );
        _sending = false;
      });
    }

    _scrollToBottom();
  }

  void _askSuggestion(String suggestion) {
    if (_sending) return;
    _message.text = suggestion;
    _ask();
  }

  void _book(ChatbotVehicle vehicle, ChatbotReply? reply) {
    VehicleType? selected;
    for (final type in widget.vehicleTypes) {
      if (type.typeName == vehicle.typeName) {
        selected = type;
        break;
      }
    }
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (_) => CreateBookingScreen(
          api: widget.api,
          vehicleTypes: widget.vehicleTypes,
          vehicleType: selected,
          initialRentalMode: reply?.rentalMode ?? 'WithDriver',
          initialStart: reply?.start,
          initialEnd: reply?.end,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      body: SafeArea(
        child: Column(
          children: [
            _buildHeader(context),
            Expanded(child: _buildMessages()),
            _buildComposer(),
          ],
        ),
      ),
    );
  }

  Widget _buildHeader(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 12, 12, 16),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF0F172A), Color(0xFF1E3A8A), Color(0xFF2563EB)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: const BorderRadius.vertical(bottom: Radius.circular(24)),
        boxShadow: [
          BoxShadow(
            color: const Color(0xFF2563EB).withValues(alpha: 0.25),
            blurRadius: 14,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Row(
        children: [
          IconButton(
            onPressed: () => Navigator.pop(context),
            color: Colors.white,
            icon: const Icon(Icons.arrow_back_ios_new_rounded, size: 20),
            tooltip: 'Quay lại',
          ),
          const SizedBox(width: 4),
          const _BotAvatar(size: 42),
          const SizedBox(width: 12),
          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Text(
                      'Trợ lý ảo DriveX',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 17,
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.2,
                      ),
                    ),
                    SizedBox(width: 6),
                    _AiBadge(),
                  ],
                ),
                SizedBox(height: 3),
                Row(
                  children: [
                    _OnlineDot(),
                    SizedBox(width: 6),
                    Text(
                      'Trực tuyến 24/7 · Sẵn sàng giải đáp',
                      style: TextStyle(
                        color: Color(0xFFDCEBFF),
                        fontSize: 11.5,
                        fontWeight: FontWeight.w500,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          IconButton(
            onPressed: () => setState(
              () => _messages
                ..clear()
                ..add(
                  const _ChatMessage(
                    text:
                        'Cuộc trò chuyện đã được làm mới. Tôi có thể hỗ trợ gì cho kế hoạch thuê xe của bạn?',
                    fromBot: true,
                  ),
                ),
            ),
            color: Colors.white,
            tooltip: 'Cuộc trò chuyện mới',
            icon: const Icon(Icons.refresh_rounded, size: 22),
          ),
        ],
      ),
    );
  }

  Widget _buildMessages() {
    final count = _messages.length + (_sending ? 1 : 0);
    return ListView.builder(
      controller: _scroll,
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 16),
      itemCount: count,
      itemBuilder: (_, index) {
        if (index == _messages.length && _sending) {
          return _buildTypingIndicator();
        }

        final item = _messages[index];
        return Align(
          alignment: item.fromBot ? Alignment.centerLeft : Alignment.centerRight,
          child: Container(
            constraints: const BoxConstraints(maxWidth: 350),
            margin: const EdgeInsets.only(bottom: 14),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                if (item.fromBot) ...[
                  const _BotAvatar(size: 28),
                  const SizedBox(width: 8),
                ],
                Flexible(
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 13,
                    ),
                    decoration: BoxDecoration(
                      color: item.fromBot ? Colors.white : AppColors.primary,
                      borderRadius: BorderRadius.only(
                        topLeft: const Radius.circular(18),
                        topRight: const Radius.circular(18),
                        bottomLeft: Radius.circular(item.fromBot ? 4 : 18),
                        bottomRight: Radius.circular(item.fromBot ? 18 : 4),
                      ),
                      border: item.fromBot
                          ? Border.all(color: const Color(0xFFE2E8F0))
                          : null,
                      boxShadow: [
                        BoxShadow(
                          color: item.fromBot
                              ? const Color(0x0C0F172A)
                              : const Color(0x282563EB),
                          blurRadius: 8,
                          offset: const Offset(0, 3),
                        ),
                      ],
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          item.text,
                          style: TextStyle(
                            color: item.fromBot ? AppColors.text : Colors.white,
                            fontSize: 14,
                            height: 1.45,
                            fontWeight: FontWeight.w400,
                          ),
                        ),
                        if (item.reply?.vehicles.isNotEmpty == true) ...[
                          const SizedBox(height: 12),
                          ...item.reply!.vehicles.map(
                            (vehicle) => _vehicleCard(vehicle, item.reply),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildTypingIndicator() {
    return Align(
      alignment: Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(bottom: 14),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            const _BotAvatar(size: 28),
            const SizedBox(width: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: const BorderRadius.only(
                  topLeft: Radius.circular(18),
                  topRight: Radius.circular(18),
                  bottomLeft: Radius.circular(4),
                  bottomRight: Radius.circular(18),
                ),
                border: Border.all(color: const Color(0xFFE2E8F0)),
                boxShadow: const [
                  BoxShadow(
                    color: Color(0x0C0F172A),
                    blurRadius: 8,
                    offset: Offset(0, 3),
                  ),
                ],
              ),
              child: const Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  SizedBox(
                    width: 13,
                    height: 13,
                    child: CircularProgressIndicator(
                      strokeWidth: 2,
                      color: AppColors.primary,
                    ),
                  ),
                  SizedBox(width: 8),
                  Text(
                    'DriveX AI đang tìm câu trả lời...',
                    style: TextStyle(
                      color: AppColors.muted,
                      fontSize: 12.5,
                      fontStyle: FontStyle.italic,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _vehicleCard(ChatbotVehicle vehicle, ChatbotReply? reply) {
    return Container(
      margin: const EdgeInsets.only(bottom: 9),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFFF8FAFC),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Row(
        children: [
          Container(
            width: 38,
            height: 38,
            decoration: BoxDecoration(
              color: const Color(0xFFEFF6FF),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFFDBEAFE)),
            ),
            child: const Icon(
              Icons.directions_car_filled_rounded,
              color: AppColors.primary,
              size: 20,
            ),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  vehicle.name,
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13.5,
                    color: AppColors.text,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 3),
                Row(
                  children: [
                    if (vehicle.seats != null) ...[
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 6,
                          vertical: 1.5,
                        ),
                        decoration: BoxDecoration(
                          color: const Color(0xFFE2E8F0),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: Text(
                          '${vehicle.seats} chỗ',
                          style: const TextStyle(
                            fontSize: 10.5,
                            fontWeight: FontWeight.w600,
                            color: Color(0xFF475569),
                          ),
                        ),
                      ),
                      const SizedBox(width: 6),
                    ],
                    if (vehicle.totalAmount != null)
                      Text(
                        Formatters.vnd(vehicle.totalAmount!),
                        style: const TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w700,
                          color: AppColors.primary,
                        ),
                      ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          FilledButton.icon(
            onPressed: () => _book(vehicle, reply),
            icon: const Icon(Icons.arrow_forward_rounded, size: 13),
            label: const Text('Đặt ngay', style: TextStyle(fontSize: 12)),
            style: FilledButton.styleFrom(
              visualDensity: VisualDensity.compact,
              backgroundColor: AppColors.primary,
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(10),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildComposer() {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 10, 14, 14),
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
        boxShadow: [
          BoxShadow(
            color: Color(0x100F172A),
            blurRadius: 16,
            offset: Offset(0, -4),
          ),
        ],
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            height: 38,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: _suggestions.length,
              separatorBuilder: (_, _) => const SizedBox(width: 8),
              itemBuilder: (_, index) => ActionChip(
                avatar: const Icon(
                  Icons.auto_awesome_rounded,
                  size: 13,
                  color: AppColors.primary,
                ),
                label: Text(_suggestions[index]),
                onPressed: _sending
                    ? null
                    : () => _askSuggestion(_suggestions[index]),
                backgroundColor: const Color(0xFFF1F5F9),
                side: const BorderSide(color: Color(0xFFE2E8F0)),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(19),
                ),
                labelStyle: const TextStyle(
                  color: Color(0xFF334155),
                  fontSize: 11.5,
                  fontWeight: FontWeight.w500,
                ),
              ),
            ),
          ),
          const SizedBox(height: 10),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _message,
                  onSubmitted: (_) => _ask(),
                  textInputAction: TextInputAction.send,
                  decoration: InputDecoration(
                    hintText: 'Nhập câu hỏi cần tư vấn xe...',
                    hintStyle: const TextStyle(
                      color: AppColors.muted,
                      fontSize: 13.5,
                    ),
                    filled: true,
                    fillColor: const Color(0xFFF8FAFC),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(22),
                      borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
                    ),
                    enabledBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(22),
                      borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
                    ),
                    focusedBorder: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(22),
                      borderSide: const BorderSide(
                        color: AppColors.primary,
                        width: 1.5,
                      ),
                    ),
                    contentPadding: const EdgeInsets.symmetric(
                      horizontal: 16,
                      vertical: 12,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 10),
              Container(
                width: 46,
                height: 46,
                decoration: BoxDecoration(
                  gradient: const LinearGradient(
                    colors: [Color(0xFF2563EB), Color(0xFF1D4ED8)],
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                  ),
                  borderRadius: BorderRadius.circular(16),
                  boxShadow: [
                    BoxShadow(
                      color: const Color(0xFF2563EB).withValues(alpha: 0.3),
                      blurRadius: 8,
                      offset: const Offset(0, 3),
                    ),
                  ],
                ),
                child: IconButton(
                  onPressed: _sending ? null : _ask,
                  icon: const Icon(
                    Icons.arrow_upward_rounded,
                    color: Colors.white,
                    size: 22,
                  ),
                  tooltip: 'Gửi',
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _ChatMessage {
  final String text;
  final bool fromBot;
  final ChatbotReply? reply;

  const _ChatMessage({required this.text, required this.fromBot, this.reply});
}

class _BotAvatar extends StatelessWidget {
  final double size;

  const _BotAvatar({required this.size});

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF3B82F6), Color(0xFF1D4ED8)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white, width: 1.5),
        boxShadow: [
          BoxShadow(
            color: const Color(0xFF2563EB).withValues(alpha: 0.3),
            blurRadius: 6,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Center(
        child: Icon(
          Icons.auto_awesome_rounded,
          color: Colors.white,
          size: size * 0.52,
        ),
      ),
    );
  }
}

class _AiBadge extends StatelessWidget {
  const _AiBadge();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF60A5FA), Color(0xFF3B82F6)],
        ),
        borderRadius: BorderRadius.circular(6),
      ),
      child: const Text(
        'AI',
        style: TextStyle(
          color: Colors.white,
          fontSize: 9.5,
          fontWeight: FontWeight.w900,
          letterSpacing: 0.5,
        ),
      ),
    );
  }
}

class _OnlineDot extends StatelessWidget {
  const _OnlineDot();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 7,
      height: 7,
      decoration: const BoxDecoration(
        color: Color(0xFF4ADE80),
        shape: BoxShape.circle,
        boxShadow: [
          BoxShadow(
            color: Color(0x664ADE80),
            blurRadius: 4,
            spreadRadius: 1,
          ),
        ],
      ),
    );
  }
}
