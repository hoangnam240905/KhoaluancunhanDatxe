# ============================================================
# CAR RENTAL RECOMMENDATION - VERSION 5
# ============================================================
# Content-Based Filtering
# TF-IDF + Cosine Similarity
# Structured Features
# Evaluation:
# - HitRate@K
# - Precision@K
# ============================================================

import pandas as pd
import numpy as np

from datasets import load_dataset

from sklearn.preprocessing import OneHotEncoder, StandardScaler
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity


# ============================================================
# 1. ĐỌC DATASET
# ============================================================

print("==========================================")
print("CAR RENTAL RECOMMENDATION - VERSION 5")
print("==========================================")


# ------------------------------------------------------------
# Tải bảng CARS
# ------------------------------------------------------------

cars_dataset = load_dataset(
    "Mayab2/carfit-ai-synthetic",
    "cars"
)

cars = cars_dataset["train"].to_pandas()


# ------------------------------------------------------------
# Tải bảng USERS
# ------------------------------------------------------------

users_dataset = load_dataset(
    "Mayab2/carfit-ai-synthetic",
    "users"
)

users = users_dataset["train"].to_pandas()


print("\nSố lượng xe:", len(cars))
print("Số lượng user:", len(users))


# ============================================================
# 2. CHUẨN BỊ THÔNG TIN XE
# ============================================================

# ------------------------------------------------------------
# Phân loại giá tương đối
# ------------------------------------------------------------

price_q1 = cars["price"].quantile(0.33)
price_q2 = cars["price"].quantile(0.66)


def get_price_category(price):

    if price <= price_q1:

        return "budget friendly"

    elif price <= price_q2:

        return "mid range"

    else:

        return "premium"


cars["price_category"] = cars["price"].apply(
    get_price_category
)


# ------------------------------------------------------------
# Ngưỡng khoang hành lý
# ------------------------------------------------------------

trunk_q1 = cars["trunk_l"].quantile(0.33)
trunk_q2 = cars["trunk_l"].quantile(0.66)


# ------------------------------------------------------------
# Hiệu suất nhiên liệu trung vị
# ------------------------------------------------------------

efficiency_median = cars["km_per_l"].median()


# ============================================================
# 3. TẠO CAR DESCRIPTION
# ============================================================

def create_car_description(row):

    text_parts = []


    # --------------------------------------------------------
    # Hãng + model
    # --------------------------------------------------------

    text_parts.append(
        str(row["make"])
    )

    text_parts.append(
        str(row["model"])
    )


    # --------------------------------------------------------
    # Phân khúc
    # --------------------------------------------------------

    segment = str(
        row["segment"]
    ).lower()


    if segment in ["mini", "supermini"]:

        text_parts.append("small car")
        text_parts.append("city driving")
        text_parts.append("city friendly")


    elif segment == "sedan":

        text_parts.append("sedan")
        text_parts.append("city driving")
        text_parts.append("daily driving")


    elif segment in ["crossover", "suv"]:

        text_parts.append("family car")
        text_parts.append("family friendly")


    elif segment == "mpv":

        text_parts.append("family car")
        text_parts.append("large family")
        text_parts.append("family friendly")


    else:

        text_parts.append(segment)


    # --------------------------------------------------------
    # Số ghế
    # --------------------------------------------------------

    seats = row["seats"]


    if seats <= 4:

        text_parts.append("small family")


    elif seats <= 5:

        text_parts.append("medium family")


    else:

        text_parts.append("large family")
        text_parts.append("many passengers")


    # --------------------------------------------------------
    # Khoang hành lý
    # --------------------------------------------------------

    trunk = row["trunk_l"]


    if trunk >= trunk_q2:

        text_parts.append("large trunk")
        text_parts.append("large luggage space")


    elif trunk >= trunk_q1:

        text_parts.append("medium trunk")


    else:

        text_parts.append("small trunk")


    # --------------------------------------------------------
    # An toàn
    # --------------------------------------------------------

    safety = row["safety_score"]


    if safety >= 4.5:

        text_parts.append("high safety")
        text_parts.append("safe")


    elif safety >= 4.0:

        text_parts.append("good safety")


    else:

        text_parts.append("standard safety")


    # --------------------------------------------------------
    # Hiệu suất nhiên liệu
    # --------------------------------------------------------

    if row["km_per_l"] >= efficiency_median:

        text_parts.append(
            "fuel efficient"
        )

    else:

        text_parts.append(
            "normal fuel consumption"
        )


    # --------------------------------------------------------
    # Loại nhiên liệu
    # --------------------------------------------------------

    fuel = str(
        row["fuel_type"]
    ).lower()


    if fuel == "electric":

        text_parts.append("electric")
        text_parts.append(
            "environment friendly"
        )


    elif fuel == "hybrid":

        text_parts.append("hybrid")
        text_parts.append(
            "fuel efficient"
        )


    elif fuel == "gas":

        text_parts.append(
            "gasoline"
        )


    else:

        text_parts.append(
            fuel
        )


    # --------------------------------------------------------
    # Nhóm giá
    # --------------------------------------------------------

    text_parts.append(
        str(row["price_category"])
    )


    return " ".join(
        text_parts
    )


cars["car_description"] = cars.apply(
    create_car_description,
    axis=1
)


# ============================================================
# 4. XÂY DỰNG TF-IDF
# ============================================================

print("\n==========================================")
print("XÂY DỰNG TF-IDF")
print("==========================================")


all_text = pd.concat(
    [
        cars["car_description"],
        users["preferences_text"].fillna("")
    ],
    ignore_index=True
)


tfidf = TfidfVectorizer(
    lowercase=True,
    stop_words="english"
)


tfidf.fit(
    all_text
)


# Vector TF-IDF của xe

car_text_matrix = tfidf.transform(
    cars["car_description"]
)


print(
    "Kích thước TF-IDF:",
    car_text_matrix.shape
)


# ============================================================
# 5. STRUCTURED FEATURES CỦA XE
# ============================================================

print("\n==========================================")
print("XÂY DỰNG STRUCTURED FEATURES")
print("==========================================")


car_categorical = cars[
    [
        "segment",
        "fuel_type"
    ]
]


car_numeric = cars[
    [
        "seats",
        "trunk_l",
        "safety_score",
        "km_per_l"
    ]
]


# ------------------------------------------------------------
# One-Hot Encoding
# ------------------------------------------------------------

encoder = OneHotEncoder(
    handle_unknown="ignore",
    sparse_output=False
)


car_cat_encoded = encoder.fit_transform(
    car_categorical
)


# ------------------------------------------------------------
# StandardScaler
# ------------------------------------------------------------

scaler = StandardScaler()


car_num_scaled = scaler.fit_transform(
    car_numeric
)


# ------------------------------------------------------------
# Ghép features
# ------------------------------------------------------------

car_feature_matrix = np.hstack(
    [
        car_cat_encoded,
        car_num_scaled
    ]
)


print(
    "Kích thước structured feature:",
    car_feature_matrix.shape
)


# ============================================================
# 6. HÀM TẠO USER PROFILE
# ============================================================

def create_user_profile(user):

    kids = user["kids"]

    family_size = user["family_size"]

    annual_km = user["annual_km"]

    charging = user["charging_access"]


    # --------------------------------------------------------
    # Xác định segment
    # --------------------------------------------------------

    if family_size >= 6:

        preferred_segment = "MPV"

    elif family_size >= 4:

        preferred_segment = "SUV"

    elif kids > 0:

        preferred_segment = "Crossover"

    else:

        preferred_segment = "Mini"


    # --------------------------------------------------------
    # Xác định nhiên liệu
    # --------------------------------------------------------

    if charging == 1:

        preferred_fuel = "electric"

    else:

        preferred_fuel = "gas"


    # --------------------------------------------------------
    # Số ghế
    # --------------------------------------------------------

    # Nếu có trẻ em thì cần thêm không gian
    # cho trẻ và đồ dùng/stroller.

    if kids > 0:

        preferred_seats = max(
            family_size + 1,
            4
        )

    else:

        preferred_seats = max(
            family_size,
            2
        )


    # --------------------------------------------------------
    # Khoang hành lý
    # --------------------------------------------------------

    if kids > 0 or family_size >= 4:

        preferred_trunk = 500

    else:

        preferred_trunk = 350


    # --------------------------------------------------------
    # An toàn
    # --------------------------------------------------------

    if kids > 0:

        preferred_safety = 4.5

    else:

        preferred_safety = 4.0


    # --------------------------------------------------------
    # Hiệu suất nhiên liệu
    # --------------------------------------------------------

    if annual_km > 15000:

        preferred_efficiency = 16

    else:

        preferred_efficiency = 14


    return {

        "segment": preferred_segment,

        "fuel_type": preferred_fuel,

        "seats": preferred_seats,

        "trunk_l": preferred_trunk,

        "safety_score": preferred_safety,

        "km_per_l": preferred_efficiency
    }


# ============================================================
# 7. HÀM RECOMMEND
# ============================================================

def recommend_cars(
    user_id,
    top_k=5
):

    # --------------------------------------------------------
    # Tìm user
    # --------------------------------------------------------

    user_rows = users[
        users["user_id"] == user_id
    ]


    if len(user_rows) == 0:

        return None


    user = user_rows.iloc[0]


    # ========================================================
    # 7.1 TEXT SIMILARITY
    # ========================================================

    user_preferences = str(
        user["preferences_text"]
    )


    user_text_vector = tfidf.transform(
        [user_preferences]
    )


    text_scores = cosine_similarity(
        user_text_vector,
        car_text_matrix
    )[0]


    # ========================================================
    # 7.2 STRUCTURED SIMILARITY
    # ========================================================

    profile = create_user_profile(
        user
    )


    # --------------------------------------------------------
    # Categorical
    # --------------------------------------------------------

    user_categorical = pd.DataFrame(
        [
            [
                profile["segment"],
                profile["fuel_type"]
            ]
        ],
        columns=[
            "segment",
            "fuel_type"
        ]
    )


    user_cat_encoded = encoder.transform(
        user_categorical
    )


    # --------------------------------------------------------
    # Numerical
    # --------------------------------------------------------

    user_numeric = pd.DataFrame(
        [
            [
                profile["seats"],
                profile["trunk_l"],
                profile["safety_score"],
                profile["km_per_l"]
            ]
        ],
        columns=[
            "seats",
            "trunk_l",
            "safety_score",
            "km_per_l"
        ]
    )


    user_num_scaled = scaler.transform(
        user_numeric
    )


    # --------------------------------------------------------
    # Ghép vector
    # --------------------------------------------------------

    user_feature_vector = np.hstack(
        [
            user_cat_encoded,
            user_num_scaled
        ]
    )


    # --------------------------------------------------------
    # Cosine Similarity
    # --------------------------------------------------------

    structured_scores = cosine_similarity(
        user_feature_vector,
        car_feature_matrix
    )[0]


    # ========================================================
    # 7.3 FINAL SCORE
    # ========================================================

    text_weight = 0.5

    structured_weight = 0.5


    final_scores = (
        text_weight * text_scores
        +
        structured_weight * structured_scores
    )


    # ========================================================
    # 7.4 TẠO RESULT
    # ========================================================

    result = cars.copy()


    result["text_similarity"] = (
        text_scores
    )


    result["structured_similarity"] = (
        structured_scores
    )


    result["similarity"] = (
        final_scores
    )


    result = result.sort_values(
        by="similarity",
        ascending=False
    )


    return result.head(
        top_k
    )


# ============================================================
# 8. TEST USER_00000
# ============================================================

print("\n==========================================")
print("TEST USER_00000")
print("==========================================")


test_result = recommend_cars(
    "user_00000",
    top_k=5
)


user_00000 = users[
    users["user_id"] == "user_00000"
].iloc[0]


print(
    "\nLabel segment:",
    user_00000["label_segment"]
)


print("\nTOP 5:")


for rank, (_, car) in enumerate(
    test_result.iterrows(),
    start=1
):

    print(
        f"{rank}. "
        f"{car['make']} "
        f"{car['model']} | "
        f"Segment: {car['segment']} | "
        f"Similarity: "
        f"{car['similarity']:.4f}"
    )


# ============================================================
# 9. HÀM ĐÁNH GIÁ HITRATE@K
# ============================================================

def calculate_hit_rate(
    user_ids,
    k=5
):

    hits = 0

    total = 0


    for user_id in user_ids:

        # Lấy user
        user_rows = users[
            users["user_id"] == user_id
        ]


        if len(user_rows) == 0:

            continue


        user = user_rows.iloc[0]


        # Label segment được dùng
        # CHỈ SAU KHI recommendation hoàn thành

        target_segment = user[
            "label_segment"
        ]


        # Recommend

        recommendations = recommend_cars(
            user_id,
            top_k=k
        )


        if recommendations is None:

            continue


        # Kiểm tra Top-K có ít nhất
        # một xe cùng segment với label

        hit = (
            recommendations["segment"]
            == target_segment
        ).any()


        if hit:

            hits += 1


        total += 1


    if total == 0:

        return 0


    return hits / total


# ============================================================
# 10. HÀM ĐÁNH GIÁ PRECISION@K
# ============================================================

def calculate_precision(
    user_ids,
    k=5
):

    total_precision = 0

    total = 0


    for user_id in user_ids:

        user_rows = users[
            users["user_id"] == user_id
        ]


        if len(user_rows) == 0:

            continue


        user = user_rows.iloc[0]


        target_segment = user[
            "label_segment"
        ]


        recommendations = recommend_cars(
            user_id,
            top_k=k
        )


        if recommendations is None:

            continue


        # Số xe trong Top-K
        # có cùng segment với label

        relevant_count = (
            recommendations["segment"]
            == target_segment
        ).sum()


        precision = (
            relevant_count / k
        )


        total_precision += precision

        total += 1


    if total == 0:

        return 0


    return total_precision / total


# ============================================================
# 11. CHỌN SAMPLE USER ĐỂ ĐÁNH GIÁ
# ============================================================

print("\n==========================================")
print("CHUẨN BỊ ĐÁNH GIÁ")
print("==========================================")


# ------------------------------------------------------------
# Để chạy nhanh trên Spyder,
# lấy 500 user làm tập đánh giá.
# ------------------------------------------------------------

evaluation_users = users[
    "user_id"
].sample(
    n=500,
    random_state=42
).tolist()


print(
    "Số user đánh giá:",
    len(evaluation_users)
)


# ============================================================
# 12. TÍNH HITRATE@5
# ============================================================

print("\n==========================================")
print("ĐÁNH GIÁ HITRATE@5")
print("==========================================")


hit_rate_5 = calculate_hit_rate(
    evaluation_users,
    k=5
)


print(
    "HitRate@5 =",
    round(
        hit_rate_5 * 100,
        2
    ),
    "%"
)


# ============================================================
# 13. TÍNH PRECISION@5
# ============================================================

print("\n==========================================")
print("ĐÁNH GIÁ PRECISION@5")
print("==========================================")


precision_5 = calculate_precision(
    evaluation_users,
    k=5
)


print(
    "Precision@5 =",
    round(
        precision_5 * 100,
        2
    ),
    "%"
)


# ============================================================
# 14. ĐÁNH GIÁ TOP-10
# ============================================================

print("\n==========================================")
print("ĐÁNH GIÁ TOP-10")
print("==========================================")


hit_rate_10 = calculate_hit_rate(
    evaluation_users,
    k=10
)


precision_10 = calculate_precision(
    evaluation_users,
    k=10
)


print(
    "HitRate@10 =",
    round(
        hit_rate_10 * 100,
        2
    ),
    "%"
)


print(
    "Precision@10 =",
    round(
        precision_10 * 100,
        2
    ),
    "%"
)


# ============================================================
# 15. BẢNG TỔNG KẾT
# ============================================================

print("\n==========================================")
print("TỔNG KẾT ĐÁNH GIÁ V5")
print("==========================================")


evaluation_result = pd.DataFrame(
    {
        "Metric": [
            "HitRate@5",
            "Precision@5",
            "HitRate@10",
            "Precision@10"
        ],

        "Value": [
            hit_rate_5,
            precision_5,
            hit_rate_10,
            precision_10
        ]
    }
)


evaluation_result["Percentage"] = (
    evaluation_result["Value"] * 100
)


print(
    evaluation_result.to_string(
        index=False
    )
)


print("\n==========================================")
print("HOÀN THÀNH V5")
print("==========================================")