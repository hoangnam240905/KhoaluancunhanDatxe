import 'package:customer_app/utils/review_rules.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('ReviewRules', () {
    test('1/2/3 sao + comment rỗng hoặc spaces thì fail', () {
      expect(ReviewRules.validateComment(1, null), ReviewRules.commentRequired);
      expect(ReviewRules.validateComment(2, ''), ReviewRules.commentRequired);
      expect(ReviewRules.validateComment(3, '   '), ReviewRules.commentRequired);
    });

    test('1/2/3 sao + comment hợp lệ thì pass', () {
      expect(ReviewRules.validateComment(1, 'Tài xế đến muộn'), isNull);
      expect(ReviewRules.validateComment(2, '  Cần cải thiện  '), isNull);
      expect(ReviewRules.validateComment(3, 'Xe ồn'), isNull);
    });

    test('4/5 sao không comment thì pass', () {
      expect(ReviewRules.validateComment(4, null), isNull);
      expect(ReviewRules.validateComment(5, ''), isNull);
      expect(ReviewRules.validateComment(5, '   '), isNull);
    });

    test('4/5 sao + comment hợp lệ thì pass', () {
      expect(ReviewRules.validateComment(4, 'Tốt'), isNull);
      expect(ReviewRules.validateComment(5, 'Rất tốt'), isNull);
    });

    test('comment > 500 thì fail', () {
      final tooLong = 'a' * 501;
      expect(ReviewRules.validateComment(3, tooLong), ReviewRules.commentTooLong);
      expect(ReviewRules.validateComment(5, tooLong), ReviewRules.commentTooLong);
    });

    test('comment = 500 thì pass', () {
      expect(ReviewRules.validateComment(3, 'b' * 500), isNull);
    });
  });
}
