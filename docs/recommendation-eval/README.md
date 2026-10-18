# Đánh giá thuật toán gợi ý V7 (v6.py / RecommendationService)

Kết quả Giai đoạn 1 trong `plan1.md`: sửa lỗi rò rỉ dữ liệu khi đánh giá, chạy ablation và so sánh với baseline.

- Script: [`evaluate_v7.py`](evaluate_v7.py) — dùng lại engine V7 thật (`RecommendationService/recommender.py`), không chép logic.
- Số liệu gốc: [`results_2026-10-18.csv`](results_2026-10-18.csv), thông tin lần chạy: [`run_2026-10-18.json`](run_2026-10-18.json).
- Chạy lại: `py docs/recommendation-eval/evaluate_v7.py` (từ thư mục gốc repo; lần đầu cần mạng để tải dataset).

## 1. Thiết lập

| Mục | Giá trị |
|---|---|
| Dataset | HuggingFace `Mayab2/carfit-ai-synthetic` (dữ liệu **tổng hợp**): 250 xe, 10.000 người dùng |
| Chia train/test | 80/20, `stratify=label_segment`, `random_state=42` → 8.000 train / 2.000 test (giống hệt engine V7) |
| Tập đánh giá | **2.000 người dùng của tập test** — Random Forest chưa thấy; có `assert` không giao với tập train |
| Kiểm tra tính đúng | Điểm tính vector hóa trùng khớp `engine.recommend` trên 25 người dùng test (sai số ≤ 1e-9). `v6.py` bản đã sửa (đánh giá trên tập test) cho đúng cùng HitRate/Precision với dòng "V7 đầy đủ" bên dưới |
| Môi trường | Python 3.11.13, scikit-learn 1.8.0, seed 42, chạy ngày 2026-10-18 |

### Các thước đo
- **HitRate@K**: tỉ lệ người dùng có ít nhất 1 xe trong Top-K cùng phân khúc với `label_segment`.
- **Precision@K**: tỉ lệ xe trong Top-K cùng phân khúc với `label_segment`.
- **SeatFit@K** (độc lập với nhãn): tỉ lệ xe trong Top-K có số chỗ ≥ số chỗ cần (`preferred_seats`, cùng luật của engine).
- **BudgetFit@K** (độc lập với nhãn): tỉ lệ xe trong Top-K có chi phí/tháng (bảo dưỡng + bảo hiểm + khấu hao) ≤ `budget_monthly`.

> `label_segment` chỉ là **nhãn proxy** do dataset tổng hợp gán, không phải lịch sử thuê thật.

## 2. Kết quả trên tập test (2.000 người dùng)

| Cấu hình | HitRate@5 | Precision@5 | HitRate@10 | Precision@10 | SeatFit@5 | BudgetFit@5 |
|---|---:|---:|---:|---:|---:|---:|
| Chỉ Text | 42,95% | 15,23% | 73,75% | 15,80% | 97,50% | 37,79% |
| Chỉ Structured | 57,10% | 28,35% | 66,10% | 22,66% | 95,45% | 52,99% |
| Chỉ ML | 48,15% | **48,09%** | 48,35% | **48,08%** | 99,48% | **56,47%** |
| Text + Structured (không ML) | 57,60% | 27,23% | 65,65% | 22,36% | 95,45% | 51,34% |
| **V7 đầy đủ 0.4/0.4/0.2** | **72,75%** | 45,49% | **91,95%** | 43,27% | **99,66%** | 52,04% |
| Baseline ngẫu nhiên | 53,55% | 14,28% | 77,15% | 14,57% | 92,79% | 50,95% |
| Baseline phân khúc phổ biến nhất | 31,10% | 31,10% | 31,10% | 31,10% | 95,45% | 48,20% |

Accuracy Random Forest dự đoán `label_segment`: **48,15% trên tập test**, **100% trên tập train**.

**Baseline ngẫu nhiên là công bằng.** Mọi cấu hình dùng cùng 250 xe ứng viên, cùng 2.000 người dùng test, cùng ground truth và cùng cách lấy Top-K. Giá trị kỳ vọng tính giải tích (chọn ngẫu nhiên K xe không lặp, theo tỉ lệ nhãn của tập test) là HitRate@5 = 53,23%, HitRate@10 = 76,81%, Precision = 14,51%, khớp với số đo (53,55% / 77,15% / 14,28%). HitRate ngẫu nhiên cao vì mỗi phân khúc có 20–58 trong 250 xe, nên 5 xe bốc bừa thường trúng ít nhất 1. Vì vậy cần so V7 với baseline (+19,20 điểm HitRate@5; Precision@5 45,49% so với 14,28%) chứ không đọc HitRate một mình.

## 3. Trước / sau khi sửa rò rỉ

| Giao thức | Tập đánh giá | HitRate@5 | Precision@5 | HitRate@10 | Precision@10 |
|---|---|---:|---:|---:|---:|
| **Cũ** (v6.py trước khi sửa) | 500 người bốc từ toàn bộ users — **406 người nằm trong tập train** | 93,60% | 80,16% | 98,00% | 69,30% |
| **Mới** | 2.000 người tập test | 72,75% | 45,49% | 91,95% | 43,27% |

Nguyên nhân chênh lệch: Random Forest đúng 100% trên tập train (học thuộc), nên với 406/500 người "quen" thành phần ML gần như luôn chỉ đúng phân khúc → số liệu cũ bị thổi phồng khoảng 21 điểm HitRate@5 và 35 điểm Precision@5. **Số liệu cũ không được dùng ở phần kết quả của báo cáo.**

## 4. Nhận xét

1. **Kết hợp 3 thành phần tốt hơn từng thành phần riêng lẻ về HitRate**: V7 đầy đủ đạt 72,75% HitRate@5, cao hơn thành phần tốt nhất đứng riêng (Structured 57,10%) và cao hơn baseline ngẫu nhiên (53,55%). Ở K=10 là 91,95% so với 77,15% của ngẫu nhiên.
2. **Precision đến chủ yếu từ ML**: "Chỉ ML" có Precision@5 48,09%, ngang V7 đầy đủ (45,49%). Đây là điều dự đoán được vì thước đo dựa trên chính nhãn mà ML được huấn luyện (xem mục 5). "Chỉ ML" có HitRate thấp vì xếp hạng gần như theo cả khối phân khúc dự đoán: đúng thì cả 5 xe đúng, sai thì cả 5 sai.
3. **Text yếu**: "Chỉ Text" có HitRate@5 (42,95%) **thấp hơn baseline ngẫu nhiên** (53,55%). TF-IDF trên mô tả xe dạng "make … model … segment …" khớp kém với câu sở thích tự do của người dùng.
4. **Bỏ ML làm giảm mạnh**: Text + Structured (không ML) chỉ đạt 57,60% HitRate@5 so với 72,75% khi có ML.
5. **Số chỗ ngồi gần như luôn đạt** ở mọi cấu hình (kể cả ngẫu nhiên 92,79%) vì 196/250 xe có ≥ 5 chỗ → SeatFit phân biệt kém, chỉ nên báo cáo kèm.
6. **V7 không nhạy với ngân sách**: BudgetFit của V7 (52,04%) xấp xỉ ngẫu nhiên (50,95%). Lý do ở mục 5.

## 4b. Thử nghiệm: so ngân sách với chi phí/tháng thay vì giá mua

Chỉ chạy trong script đánh giá (lớp `MonthlyBudgetStructured`); **engine V7 đang phục vụ web không đổi**. Phần Structured giữ nguyên, chỉ thay cột "giá" của xe bằng chi phí/tháng (bảo dưỡng + bảo hiểm + khấu hao, 1.111–4.715) — cùng đơn vị với `budget_monthly` — và fit lại bộ chuẩn hóa.

| Cấu hình (tập test) | HitRate@5 | Precision@5 | HitRate@10 | Precision@10 | BudgetFit@5 |
|---|---:|---:|---:|---:|---:|
| Chỉ Structured — hiện tại | 57,10% | 28,35% | 66,10% | 22,66% | 52,99% |
| Chỉ Structured — chi phí/tháng | 54,40% | 25,63% | 63,60% | 21,82% | 49,43% |
| V7 đầy đủ — hiện tại | **72,75%** | 45,49% | **91,95%** | 43,27% | 52,04% |
| V7 đầy đủ — chi phí/tháng | 70,75% | **46,72%** | 88,90% | **44,55%** | 50,75% |

Sau khi sửa, ngân sách người dùng đã thực sự khác nhau trên thang chuẩn hóa (z = −2,285 … 3,626 thay vì gần như hằng số), nhưng **V7 vẫn không nhạy với ngân sách** (BudgetFit ≈ ngẫu nhiên) và HitRate giảm nhẹ.

Lý do: Structured dùng **cosine similarity** — thưởng cho xe có chi phí *gần* ngân sách, không phải *không vượt* ngân sách — nên khoảng một nửa xe được đẩy lên vẫn đắt hơn ngân sách. Ngoài ra vector người dùng có các giá trị cố định (cốp 400 L, an toàn 8, 15 km/L) chiếm nhiều chiều.

**Kết luận:** sửa đơn vị là cần thiết cho đúng nghĩa, nhưng **không đủ**. Muốn gợi ý theo ngân sách cần một cơ chế riêng (ví dụ phạt điểm khi chi phí vượt ngân sách, hoặc lọc mềm), và phải đo lại bằng script này. Vì phương án sửa đơn vị không cải thiện chỉ số, **giữ V7 hiện tại làm kết quả chính**, ghi lỗi đơn vị vào phần giới hạn.

## 5. Giới hạn (phải ghi trong báo cáo)

- **Dữ liệu tổng hợp, nhãn proxy**: `label_segment` không phải hành vi thuê thật.
- **Thước đo có phần "tự chấm"**: thành phần ML (20%) được huấn luyện để dự đoán đúng `label_segment` — chính là tiêu chí chấm HitRate/Precision. Vì vậy cần đọc kèm bảng ablation và các thước đo độc lập với nhãn.
- **Lệch đơn vị giá trong phần Structured**: engine đặt `budget_monthly` của người dùng (900–5.315/tháng) vào cùng thang chuẩn hóa với **giá mua xe** (74.580–479.657). Sau chuẩn hóa, giá trị "giá" của mọi người dùng nằm trong khoảng z = −2,31 … −2,265 (gần như hằng số) → chiều giá không giúp phân biệt người dùng, giải thích BudgetFit ≈ ngẫu nhiên. Chi phí/tháng của xe trong dataset (1.111–4.715) mới cùng đơn vị với ngân sách. Đã thử sửa đơn vị (mục 4b): không cải thiện chỉ số vì bản chất cosine similarity, nên V7 đang chạy được giữ nguyên.
- **Trọng số 0.4/0.4/0.2 là thủ công**, chưa tối ưu (chưa grid search trên tập validation riêng).
- Engine V7 phục vụ web dùng **cùng** dữ liệu và mô hình này; đội xe demo trong DB là xe mô phỏng lấy từ catalog dataset.
