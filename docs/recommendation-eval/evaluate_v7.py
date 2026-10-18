"""
Đánh giá V7 đúng chuẩn + ablation (plan1.md — Giai đoạn 1).

- Dùng lại engine V7 thật (RecommendationService/recommender.py): cùng dataset, cùng
  Random Forest, cùng TF-IDF/structured, cùng cách chia train/test (random_state=42, stratify).
- Chỉ đánh giá trên user thuộc TẬP TEST (RF chưa thấy). Có assert không giao với tập train.
- Ablation: từng thành phần riêng, Text+Structured, full 0.4/0.4/0.2, 2 baseline không cá nhân hóa.
- Metric theo label (proxy) + metric độc lập với label (số chỗ, chi phí/tháng).
- Chạy lại giao thức cũ (500 user bốc từ toàn bộ users) để có bảng "trước / sau".

Chạy (từ thư mục gốc repo):
    py docs/recommendation-eval/evaluate_v7.py
Lần đầu cần mạng để tải dataset HuggingFace; sau đó có thể đặt HF_DATASETS_OFFLINE=1.
"""

from __future__ import annotations

import datetime as dt
import json
import platform
import sys
from pathlib import Path

import numpy as np
import pandas as pd
import sklearn
from sklearn.metrics import accuracy_score
from sklearn.metrics.pairwise import cosine_similarity
from sklearn.model_selection import train_test_split

ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "RecommendationService"))

from recommender import (  # noqa: E402
    FEATURE_COLUMNS,
    NUMERIC_CAR_COLUMNS,
    TARGET_COLUMN,
    CarRecommendationEngine,
)

SEED = 42
KS = (5, 10)
LEAKY_SAMPLE = 500

# name -> (w_text, w_structured, w_ml)
CONFIGS: dict[str, tuple[float, float, float]] = {
    "Chỉ Text": (1.0, 0.0, 0.0),
    "Chỉ Structured": (0.0, 1.0, 0.0),
    "Chỉ ML": (0.0, 0.0, 1.0),
    "Text + Structured (không ML)": (0.5, 0.5, 0.0),
    "V7 đầy đủ 0.4/0.4/0.2": (0.4, 0.4, 0.2),
}


# ---------------------------------------------------------------------------
# Component scores for many users at once (same math as the engine, vectorised)
# ---------------------------------------------------------------------------

def preferred_seats(users: pd.DataFrame) -> np.ndarray:
    kids = users["kids"].astype(int).to_numpy()
    family = users["family_size"].astype(int).to_numpy()
    return np.where(kids > 0, np.maximum(family + 1, 4), np.maximum(family, 2))


def rule_segment(users: pd.DataFrame) -> np.ndarray:
    kids = users["kids"].astype(int).to_numpy()
    family = users["family_size"].astype(int).to_numpy()
    usage = users["usage_primary"].astype(str).str.lower().to_numpy()
    return np.select(
        [(kids > 0) & (family >= 5), kids > 0, usage == "work", usage == "city"],
        ["7-Seater", "Crossover", "Sedan", "Supermini"],
        default="Crossover",
    )


def text_matrix(engine: CarRecommendationEngine, users: pd.DataFrame) -> np.ndarray:
    user_tfidf = engine.tfidf_vectorizer.transform(users["preferences_text"].astype(str))
    return cosine_similarity(user_tfidf, engine.car_tfidf)


def structured_user_frame(engine: CarRecommendationEngine, users: pd.DataFrame) -> pd.DataFrame:
    frame = pd.DataFrame({
        "segment": rule_segment(users),
        "fuel_type": "Petrol",
        "seats": preferred_seats(users),
        "price": users["budget_monthly"].to_numpy(),
        "trunk_l": 400,
        "safety_score": 8,
        "km_per_l": 15,
    })
    frame = pd.get_dummies(frame, columns=["segment", "fuel_type"])
    frame = frame.reindex(columns=engine.cars_structured.columns, fill_value=0)
    frame[NUMERIC_CAR_COLUMNS] = engine.scaler.transform(frame[NUMERIC_CAR_COLUMNS])
    return frame.astype(float)


def structured_matrix(engine: CarRecommendationEngine, users: pd.DataFrame) -> np.ndarray:
    return cosine_similarity(structured_user_frame(engine, users), engine.cars_structured)


def ml_matrix(engine: CarRecommendationEngine, users: pd.DataFrame) -> np.ndarray:
    proba = engine.ml_pipeline.predict_proba(users[FEATURE_COLUMNS])
    classes = list(engine.ml_pipeline.named_steps["classifier"].classes_)
    out = np.zeros((len(users), len(engine.cars)))
    for j, segment in enumerate(engine.cars["segment"]):
        if segment in classes:
            out[:, j] = proba[:, classes.index(segment)]
    return out


# ---------------------------------------------------------------------------
# Metrics
# ---------------------------------------------------------------------------

def top_k(scores: np.ndarray, k: int) -> np.ndarray:
    return np.argsort(-scores, axis=1, kind="stable")[:, :k]


def evaluate(scores: np.ndarray, users: pd.DataFrame, cars: pd.DataFrame) -> dict[str, float]:
    labels = users[TARGET_COLUMN].to_numpy()
    seats_needed = preferred_seats(users)
    budget = users["budget_monthly"].to_numpy()
    car_segment = cars["segment"].to_numpy()
    car_seats = cars["seats"].to_numpy()
    car_monthly = (cars["maintenance_monthly_est"] + cars["insurance_monthly_est"]
                   + cars["depreciation_monthly_est"]).to_numpy()

    row: dict[str, float] = {}
    for k in KS:
        idx = top_k(scores, k)
        match = car_segment[idx] == labels[:, None]
        row[f"HitRate@{k}"] = float(match.any(axis=1).mean())
        row[f"Precision@{k}"] = float(match.mean())
        row[f"SeatFit@{k}"] = float((car_seats[idx] >= seats_needed[:, None]).mean())
        row[f"BudgetFit@{k}"] = float((car_monthly[idx] <= budget[:, None]).mean())
    return row


def combine(components: dict[str, np.ndarray], weights: tuple[float, float, float]) -> np.ndarray:
    wt, ws, wm = weights
    return wt * components["text"] + ws * components["structured"] + wm * components["ml"]


def baselines(users: pd.DataFrame, cars: pd.DataFrame, y_train: pd.Series) -> dict[str, np.ndarray]:
    rng = np.random.default_rng(SEED)
    random_scores = rng.random((len(users), len(cars)))
    # Non-personalised: rank cars by how common their segment is among TRAIN labels.
    freq = y_train.value_counts(normalize=True)
    per_car = cars["segment"].map(freq).fillna(0).to_numpy()
    popular_scores = np.tile(per_car - np.arange(len(cars)) * 1e-9, (len(users), 1))
    return {
        "Baseline ngẫu nhiên": random_scores,
        "Baseline phân khúc phổ biến nhất": popular_scores,
    }


# ---------------------------------------------------------------------------
# Sanity checks: vectorised maths must reproduce engine.recommend exactly
# ---------------------------------------------------------------------------

def check_against_engine(engine: CarRecommendationEngine, users: pd.DataFrame,
                         components: dict[str, np.ndarray], n: int = 25) -> None:
    full = combine(components, CONFIGS["V7 đầy đủ 0.4/0.4/0.2"])
    for i in range(min(n, len(users))):
        user = users.iloc[i]
        expected = engine.recommend(user.to_dict(), top_k=5)["recommendations"]
        expected_scores = np.array([r["final_score"] for r in expected])
        got_scores = np.sort(full[i])[::-1][:5]
        assert np.allclose(expected_scores, got_scores, atol=1e-9), f"score mismatch at test user {i}"
        ours = set(engine.cars["car_id"].iloc[top_k(full[i:i + 1], 5)[0]])
        theirs = {r["car_id"] for r in expected}
        tie = np.isclose(np.sort(full[i])[::-1][4], np.sort(full[i])[::-1][5])
        assert ours == theirs or tie, f"top-5 mismatch at test user {i}"


# ---------------------------------------------------------------------------

def main() -> None:
    started = dt.datetime.now()
    engine = CarRecommendationEngine()
    users, cars = engine.users, engine.cars

    X = users[FEATURE_COLUMNS].copy()
    y = users[TARGET_COLUMN].copy()
    # Identical call to the engine's own split -> identical train/test partition.
    X_train, X_test, y_train, y_test = train_test_split(
        X, y, test_size=0.2, random_state=SEED, stratify=y)
    assert X_test.index.intersection(X_train.index).empty

    rf_accuracy = accuracy_score(y_test, engine.ml_pipeline.predict(X_test))
    rf_train_accuracy = accuracy_score(y_train, engine.ml_pipeline.predict(X_train))

    test_users = users.loc[X_test.index]
    comps = {
        "text": text_matrix(engine, test_users),
        "structured": structured_matrix(engine, test_users),
        "ml": ml_matrix(engine, test_users),
    }
    check_against_engine(engine, test_users, comps)

    rows = []
    for name, weights in CONFIGS.items():
        rows.append({"Cấu hình": name, "Tập": "test", **evaluate(combine(comps, weights), test_users, cars)})
    for name, scores in baselines(test_users, cars, y_train).items():
        rows.append({"Cấu hình": name, "Tập": "test", **evaluate(scores, test_users, cars)})

    # Old (leaky) protocol: 500 users sampled from ALL users, as v6.py used to do.
    leaky_users = users.sample(n=LEAKY_SAMPLE, random_state=SEED)
    leaky_overlap = int(leaky_users.index.isin(X_train.index).sum())
    leaky_comps = {
        "text": text_matrix(engine, leaky_users),
        "structured": structured_matrix(engine, leaky_users),
        "ml": ml_matrix(engine, leaky_users),
    }
    rows.append({"Cấu hình": "V7 đầy đủ 0.4/0.4/0.2 — giao thức CŨ (rò rỉ)",
                 "Tập": f"500 bốc từ toàn bộ users ({leaky_overlap} nằm trong train)",
                 **evaluate(combine(leaky_comps, CONFIGS["V7 đầy đủ 0.4/0.4/0.2"]), leaky_users, cars)})

    table = pd.DataFrame(rows)

    # Diagnostic: budget_monthly is fed into the car *purchase price* scaler.
    z_price = structured_user_frame(engine, test_users)["price"]
    car_monthly = (cars["maintenance_monthly_est"] + cars["insurance_monthly_est"]
                   + cars["depreciation_monthly_est"])
    diagnostics = {
        "run_at": started.isoformat(timespec="seconds"),
        "python": platform.python_version(),
        "sklearn": sklearn.__version__,
        "seed": SEED,
        "n_cars": int(len(cars)),
        "n_users": int(len(users)),
        "n_train": int(len(X_train)),
        "n_test": int(len(X_test)),
        "rf_accuracy_test": round(float(rf_accuracy), 4),
        "rf_accuracy_train": round(float(rf_train_accuracy), 4),
        "leaky_sample_in_train": leaky_overlap,
        "car_price_range": [float(cars["price"].min()), float(cars["price"].max())],
        "user_budget_monthly_range": [float(users["budget_monthly"].min()), float(users["budget_monthly"].max())],
        "budget_as_price_zscore_range": [round(float(z_price.min()), 3), round(float(z_price.max()), 3)],
        "car_monthly_cost_range": [round(float(car_monthly.min()), 1), round(float(car_monthly.max()), 1)],
        "engine_consistency_check": "passed (25 test users, vectorised scores == engine.recommend)",
    }

    stamp = started.strftime("%Y-%m-%d")
    csv_path = OUT_DIR / f"results_{stamp}.csv"
    json_path = OUT_DIR / f"run_{stamp}.json"
    table.to_csv(csv_path, index=False, encoding="utf-8-sig")
    json_path.write_text(json.dumps(diagnostics, ensure_ascii=False, indent=2), encoding="utf-8")

    pct = table.copy()
    for col in pct.columns[2:]:
        pct[col] = (pct[col] * 100).map(lambda v: f"{v:.2f}%")
    print(json.dumps(diagnostics, ensure_ascii=False, indent=2))
    print(pct.to_string(index=False))
    print(f"\nĐã lưu: {csv_path.relative_to(ROOT)} và {json_path.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
