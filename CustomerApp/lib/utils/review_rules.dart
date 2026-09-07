class ReviewRules {
  static const int commentMaxLength = 500;
  static const String commentRequired =
      'Vui lòng nhập nhận xét khi đánh giá từ 1 đến 3 sao.';
  static const String commentTooLong = 'Nhận xét không được vượt quá 500 ký tự.';

  static String? normalizeComment(String? comment) {
    final trimmed = comment?.trim();
    if (trimmed == null || trimmed.isEmpty) return null;
    return trimmed;
  }

  static String? validateComment(int rating, String? comment) {
    final normalized = normalizeComment(comment);
    if (rating >= 1 && rating <= 3 && normalized == null) {
      return commentRequired;
    }
    if (normalized != null && normalized.length > commentMaxLength) {
      return commentTooLong;
    }
    return null;
  }
}
