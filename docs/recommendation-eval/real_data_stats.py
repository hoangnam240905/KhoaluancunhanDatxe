"""
Thống kê dữ liệu thật do web ghi lại (plan1.md — Giai đoạn 4). CHỈ ĐỌC.

- RecommendationLogs: mỗi lần trang Gợi ý hiển thị, mỗi xe được gợi ý là 1 dòng.
  Một "lần gợi ý" = các dòng cùng CustomerId + CreatedAt.
- CustomerVehicleInteractions: VIEW / BOOK / RENT / CANCEL.
- Bookings: đơn thật, cờ SourceRecommended = đặt từ trang Gợi ý.

Chỉ MÔ TẢ số liệu (đã ghi log đúng chưa, có bao nhiêu). Không dùng để kết luận chất lượng V7:
dữ liệu hiện chỉ đến từ tài khoản demo / chạy thử.

Chạy (từ thư mục gốc repo):
    py docs/recommendation-eval/real_data_stats.py [đường_dẫn_db]
Mặc định đọc Backend/carrental.db ở chế độ read-only (không ghi, không khóa ghi).
"""

from __future__ import annotations

import datetime as dt
import json
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = Path(__file__).resolve().parent
# A BOOK counts as "after a recommendation" when the same customer was shown that vehicle
# within this window before booking.
ATTRIBUTION_DAYS = 7


def connect(db_path: Path) -> sqlite3.Connection:
    return sqlite3.connect(f"file:{db_path.as_posix()}?mode=ro", uri=True)


def scalar(con: sqlite3.Connection, sql: str) -> int:
    return con.execute(sql).fetchone()[0] or 0


def collect(con: sqlite3.Connection) -> dict:
    lists = con.execute(
        "SELECT CustomerId, CreatedAt, COUNT(*) FROM RecommendationLogs GROUP BY CustomerId, CreatedAt"
    ).fetchall()
    per_day = con.execute(
        "SELECT substr(CreatedAt, 1, 10), COUNT(DISTINCT CustomerId || '|' || CreatedAt) "
        "FROM RecommendationLogs GROUP BY 1 ORDER BY 1"
    ).fetchall()
    by_segment = con.execute(
        "SELECT RecommendationReason, COUNT(*) FROM RecommendationLogs GROUP BY 1 ORDER BY 2 DESC"
    ).fetchall()

    interactions = dict(con.execute(
        "SELECT InteractionType, COUNT(*) FROM CustomerVehicleInteractions GROUP BY 1 ORDER BY 1"
    ).fetchall())
    book_after_rec = scalar(con, f"""
        SELECT COUNT(*) FROM CustomerVehicleInteractions i
        WHERE i.InteractionType = 'BOOK' AND EXISTS (
            SELECT 1 FROM RecommendationLogs r
            WHERE r.CustomerId = i.CustomerId AND r.VehicleId = i.VehicleId
              AND r.CreatedAt <= i.CreatedAt
              AND julianday(i.CreatedAt) - julianday(r.CreatedAt) <= {ATTRIBUTION_DAYS})""")

    bookings_by_status = dict(con.execute(
        "SELECT Status, COUNT(*) FROM Bookings GROUP BY 1 ORDER BY 2 DESC"
    ).fetchall())

    return {
        "khoang_thoi_gian": [
            con.execute("SELECT MIN(CreatedAt) FROM RecommendationLogs").fetchone()[0],
            con.execute("SELECT MAX(CreatedAt) FROM RecommendationLogs").fetchone()[0],
        ],
        "so_khach_hang": scalar(con, "SELECT COUNT(*) FROM Customers"),
        "goi_y": {
            "so_dong_log": scalar(con, "SELECT COUNT(*) FROM RecommendationLogs"),
            "so_lan_goi_y": len(lists),
            "so_khach_duoc_goi_y": len({c for c, _, _ in lists}),
            "trung_binh_xe_moi_lan": round(sum(n for _, _, n in lists) / len(lists), 2) if lists else 0,
            "so_lan_theo_ngay": dict(per_day),
            "theo_phan_khuc_du_doan": dict(by_segment),
        },
        "tuong_tac": {
            "theo_loai": interactions,
            "so_khach_co_tuong_tac": scalar(con, "SELECT COUNT(DISTINCT CustomerId) FROM CustomerVehicleInteractions"),
            f"book_sau_goi_y_{ATTRIBUTION_DAYS}_ngay": book_after_rec,
            "book_co_booking_id": scalar(con,
                "SELECT COUNT(*) FROM CustomerVehicleInteractions WHERE InteractionType='BOOK' AND BookingId IS NOT NULL"),
        },
        "don_thue": {
            "tong": scalar(con, "SELECT COUNT(*) FROM Bookings"),
            "so_khach_dat": scalar(con, "SELECT COUNT(DISTINCT CustomerId) FROM Bookings"),
            "dat_tu_trang_goi_y": scalar(con, "SELECT COUNT(*) FROM Bookings WHERE SourceRecommended = 1"),
            "theo_trang_thai": bookings_by_status,
        },
    }


def main() -> None:
    db_path = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / "Backend" / "carrental.db"
    with connect(db_path) as con:
        stats = collect(con)
    stats["chay_luc"] = dt.datetime.now().isoformat(timespec="seconds")

    out = OUT_DIR / f"real_data_{dt.date.today():%Y-%m-%d}.json"
    out.write_text(json.dumps(stats, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(stats, ensure_ascii=False, indent=2))
    print(f"\nĐã lưu: {out.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
