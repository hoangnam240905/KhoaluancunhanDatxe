# ============================================================
# V7 - ML-ASSISTED CONTENT-BASED CAR RECOMMENDATION
# ============================================================
# Ý tưởng:
# 1. Random Forest học sở thích phân khúc xe của người dùng
# 2. TF-IDF đo độ tương đồng giữa preferences_text và mô tả xe
# 3. Structured Similarity đo độ phù hợp giữa thông tin người dùng và xe
# 4. ML Segment Probability bổ sung tín hiệu từ mô hình ML
# 5. KHÔNG lọc cứng xe theo predicted_segment
# 6. Xếp hạng trên TOÀN BỘ danh sách xe
#
# Công thức:
# Final Score =
#     0.4 * Text Similarity
#   + 0.4 * Structured Similarity
#   + 0.2 * ML Segment Probability
# ============================================================


# ============================================================
# 1. IMPORT THƯ VIỆN
# ============================================================

import pandas as pd
import numpy as np

from datasets import load_dataset

from sklearn.model_selection import train_test_split
from sklearn.compose import ColumnTransformer
from sklearn.preprocessing import OneHotEncoder, StandardScaler
from sklearn.pipeline import Pipeline
from sklearn.ensemble import RandomForestClassifier

from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity

from sklearn.metrics import accuracy_score, classification_report


# ============================================================
# 2. LOAD DATASET
# ============================================================

print("=" * 70)
print("V7 - ML-ASSISTED CONTENT-BASED RECOMMENDATION")
print("=" * 70)


# Load bảng cars
cars_dataset = load_dataset(
    "Mayab2/carfit-ai-synthetic",
    "cars"
)

cars = cars_dataset["train"].to_pandas()


# Load bảng users
users_dataset = load_dataset(
    "Mayab2/carfit-ai-synthetic",
    "users"
)

users = users_dataset["train"].to_pandas()


print("\nĐã load dataset")

print("Số lượng xe:", len(cars))
print("Số lượng user:", len(users))


# ============================================================
# 3. KIỂM TRA PHÂN BỐ LABEL
# ============================================================

print("\n" + "=" * 70)
print("PHÂN BỐ LABEL_SEGMENT")
print("=" * 70)

print(
    users["label_segment"]
    .value_counts()
)


# ============================================================
# 4. HUẤN LUYỆN RANDOM FOREST
# ============================================================
# ML học:
#
# Input:
# - age
# - kids
# - family_size
# - income_bracket
# - city_type
# - commute_km_day
# - charging_access
# - annual_km
# - usage_primary
# - budget_monthly
#
# Target:
# - label_segment
#
# label_segment KHÔNG được đưa trực tiếp vào recommendation.
# Nó chỉ dùng làm target để huấn luyện và đánh giá ML.


feature_columns = [
    "age",
    "kids",
    "family_size",
    "income_bracket",
    "city_type",
    "commute_km_day",
    "charging_access",
    "annual_km",
    "usage_primary",
    "budget_monthly"
]


target_column = "label_segment"


X = users[feature_columns].copy()
y = users[target_column].copy()


# Các cột categorical
categorical_features = [
    "income_bracket",
    "city_type",
    "usage_primary"
]


# Các cột numerical
numerical_features = [
    "age",
    "kids",
    "family_size",
    "commute_km_day",
    "charging_access",
    "annual_km",
    "budget_monthly"
]


# Preprocessing
preprocessor = ColumnTransformer(
    transformers=[
        (
            "num",
            StandardScaler(),
            numerical_features
        ),
        (
            "cat",
            OneHotEncoder(
                handle_unknown="ignore"
            ),
            categorical_features
        )
    ]
)


# Random Forest
rf_model = RandomForestClassifier(
    n_estimators=200,
    random_state=42,
    class_weight="balanced"
)


# Pipeline
ml_pipeline = Pipeline(
    steps=[
        (
            "preprocessor",
            preprocessor
        ),
        (
            "classifier",
            rf_model
        )
    ]
)


# Chia train/test
X_train, X_test, y_train, y_test = train_test_split(
    X,
    y,
    test_size=0.2,
    random_state=42,
    stratify=y
)


print("\n" + "=" * 70)
print("HUẤN LUYỆN RANDOM FOREST")
print("=" * 70)

print("Train:", len(X_train))
print("Test :", len(X_test))


# Train
ml_pipeline.fit(
    X_train,
    y_train
)


# Dự đoán
y_pred = ml_pipeline.predict(X_test)


# Accuracy
ml_accuracy = accuracy_score(
    y_test,
    y_pred
)


print("\nAccuracy Random Forest:")
print(
    f"{ml_accuracy * 100:.2f}%"
)


print("\nClassification Report:")

print(
    classification_report(
        y_test,
        y_pred
    )
)


# ============================================================
# 5. TẠO MÔ TẢ TEXT CHO XE
# ============================================================
# TF-IDF cần cùng một không gian vector.
#
# Vì user có preferences_text,
# ta tạo car_description từ các thuộc tính của xe.


def create_car_description(row):

    description = (
        f"make {row['make']} "
        f"model {row['model']} "
        f"segment {row['segment']} "
        f"fuel {row['fuel_type']} "
        f"seats {row['seats']} "
        f"price {row['price']} "
        f"trunk {row['trunk_l']} "
        f"safety {row['safety_score']} "
        f"fuel economy {row['km_per_l']}"
    )

    return description


cars["car_description"] = cars.apply(
    create_car_description,
    axis=1
)


# ============================================================
# 6. TF-IDF
# ============================================================

print("\n" + "=" * 70)
print("XÂY DỰNG TF-IDF")
print("=" * 70)


tfidf_vectorizer = TfidfVectorizer(
    lowercase=True,
    ngram_range=(1, 2)
)


# Vector cho xe
car_tfidf = tfidf_vectorizer.fit_transform(
    cars["car_description"]
)


print(
    "TF-IDF shape:",
    car_tfidf.shape
)


# ============================================================
# 7. STRUCTURED FEATURES CHO XE
# ============================================================

structured_columns = [
    "segment",
    "fuel_type",
    "seats",
    "price",
    "trunk_l",
    "safety_score",
    "km_per_l"
]


cars_structured = cars[
    structured_columns
].copy()


# One-hot cho categorical
cars_structured = pd.get_dummies(
    cars_structured,
    columns=[
        "segment",
        "fuel_type"
    ]
)


# Chuẩn hóa numeric
numeric_car_columns = [
    "seats",
    "price",
    "trunk_l",
    "safety_score",
    "km_per_l"
]


scaler = StandardScaler()


cars_structured[
    numeric_car_columns
] = scaler.fit_transform(
    cars_structured[
        numeric_car_columns
    ]
)


# Đảm bảo toàn bộ là numeric
cars_structured = cars_structured.astype(float)


print(
    "Structured feature shape:",
    cars_structured.shape
)


# ============================================================
# 8. HÀM TẠO USER PROFILE
# ============================================================

def build_user_profile(user):

    family_size = int(
        user["family_size"]
    )

    kids = int(
        user["kids"]
    )

    # Nếu có con thì cần thêm không gian
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


    user_profile = {

        "age": user["age"],

        "kids": kids,

        "family_size": family_size,

        "income_bracket":
            user["income_bracket"],

        "city_type":
            user["city_type"],

        "commute_km_day":
            user["commute_km_day"],

        "charging_access":
            user["charging_access"],

        "annual_km":
            user["annual_km"],

        "usage_primary":
            user["usage_primary"],

        "budget_monthly":
            user["budget_monthly"],

        "preferred_seats":
            preferred_seats,

        "preferences_text":
            user["preferences_text"]

    }

    return user_profile


# ============================================================
# 9. TÍNH STRUCTURED SIMILARITY
# ============================================================

def calculate_structured_similarity(
    user_profile
):

    user_segment = None

    usage = str(
        user_profile["usage_primary"]
    ).lower()


    # Xác định phân khúc ưu tiên theo rule
    if user_profile["kids"] > 0:

        if user_profile["family_size"] >= 5:

            user_segment = "7-Seater"

        else:

            user_segment = "Crossover"


    elif usage == "work":

        user_segment = "Sedan"


    elif usage == "city":

        user_segment = "Supermini"


    else:

        user_segment = "Crossover"


    # Tạo DataFrame user
    user_df = pd.DataFrame(
        [
            {
                "segment": user_segment,

                "fuel_type": "Petrol",

                "seats":
                    user_profile["preferred_seats"],

                "price":
                    user_profile["budget_monthly"],

                "trunk_l": 400,

                "safety_score": 8,

                "km_per_l": 15
            }
        ]
    )


    # One-hot giống cars
    user_df = pd.get_dummies(
        user_df,
        columns=[
            "segment",
            "fuel_type"
        ]
    )


    # Đồng bộ column
    user_df = user_df.reindex(
        columns=cars_structured.columns,
        fill_value=0
    )


    # Chuẩn hóa numeric
    user_df[
        numeric_car_columns
    ] = scaler.transform(
        user_df[
            numeric_car_columns
        ]
    )


    user_df = user_df.astype(float)


    # Cosine similarity
    similarity = cosine_similarity(
        user_df,
        cars_structured
    )[0]


    return similarity


# ============================================================
# 10. HÀM RECOMMEND V7
# ============================================================

def recommend_v7(
    user,
    top_k=10
):

    # --------------------------------------------------------
    # A. USER PROFILE
    # --------------------------------------------------------

    user_profile = build_user_profile(
        user
    )


    # --------------------------------------------------------
    # B. TEXT SIMILARITY
    # --------------------------------------------------------

    user_text = [
        str(
            user_profile["preferences_text"]
        )
    ]


    user_tfidf = (
        tfidf_vectorizer.transform(
            user_text
        )
    )


    text_scores = cosine_similarity(
        user_tfidf,
        car_tfidf
    )[0]


    # --------------------------------------------------------
    # C. STRUCTURED SIMILARITY
    # --------------------------------------------------------

    structured_scores = (
        calculate_structured_similarity(
            user_profile
        )
    )


    # --------------------------------------------------------
    # D. ML PREDICT PROBABILITY
    # --------------------------------------------------------
    # Quan trọng:
    #
    # Không lấy predicted_segment để FILTER xe.
    #
    # Thay vào đó:
    # xác suất ML của từng segment được đưa
    # vào điểm cuối cùng.
    #
    # Ví dụ:
    #
    # ML dự đoán:
    # Crossover = 0.77
    #
    # Một chiếc Crossover:
    # ml_segment_score = 0.77
    #
    # Một chiếc Sedan:
    # ml_segment_score = 0.13
    #
    # Như vậy ML chỉ "ưu tiên" chứ không loại xe.


    user_ml_df = pd.DataFrame(
        [user_profile]
    )


    user_ml_df = user_ml_df[
        feature_columns
    ]


    # Probability của từng class
    probabilities = (
        ml_pipeline.predict_proba(
            user_ml_df
        )[0]
    )


    classes = (
        ml_pipeline
        .named_steps["classifier"]
        .classes_
    )


    probability_map = dict(
        zip(
            classes,
            probabilities
        )
    )


    # Segment probability cho từng xe
    ml_segment_scores = []

    for segment in cars["segment"]:

        score = probability_map.get(
            segment,
            0
        )

        ml_segment_scores.append(
            score
        )


    ml_segment_scores = np.array(
        ml_segment_scores
    )


    # --------------------------------------------------------
    # E. PREDICTED SEGMENT
    # --------------------------------------------------------

    predicted_segment = (
        ml_pipeline.predict(
            user_ml_df
        )[0]
    )


    # --------------------------------------------------------
    # F. FINAL SCORE
    # --------------------------------------------------------
    #
    # 40% Text
    # 40% Structured
    # 20% ML
    #
    # Đây là trọng số thử nghiệm của V7.
    # Không phải trọng số được ML tự học.


    final_scores = (

        0.4 * text_scores

        + 0.4 * structured_scores

        + 0.2 * ml_segment_scores
    )


    # --------------------------------------------------------
    # G. TẠO RESULT
    # --------------------------------------------------------

    result = cars[
        [
            "car_id",
            "make",
            "model",
            "segment",
            "price",
            "fuel_type",
            "seats",
            "safety_score"
        ]
    ].copy()


    result[
        "text_similarity"
    ] = text_scores


    result[
        "structured_similarity"
    ] = structured_scores


    result[
        "ml_segment_probability"
    ] = ml_segment_scores


    result[
        "final_score"
    ] = final_scores


    # --------------------------------------------------------
    # H. SORT
    # --------------------------------------------------------

    result = result.sort_values(
        "final_score",
        ascending=False
    )


    # --------------------------------------------------------
    # I. TOP K
    # --------------------------------------------------------

    result = result.head(
        top_k
    ).reset_index(
        drop=True
    )


    return (
        result,
        predicted_segment,
        probability_map
    )


# ============================================================
# 11. TEST USER_00000
# ============================================================

print("\n" + "=" * 70)
print("TEST RECOMMENDATION - USER_00000")
print("=" * 70)


test_user = users[
    users["user_id"] == "user_00000"
].iloc[0]


print("\nThông tin user:")

print(
    "User ID:",
    test_user["user_id"]
)

print(
    "Age:",
    test_user["age"]
)

print(
    "Family size:",
    test_user["family_size"]
)

print(
    "Kids:",
    test_user["kids"]
)

print(
    "Usage:",
    test_user["usage_primary"]
)

print(
    "Budget:",
    test_user["budget_monthly"]
)

print(
    "Actual label:",
    test_user["label_segment"]
)


# Recommend
recommendations, predicted_segment, probability_map = (
    recommend_v7(
        test_user,
        top_k=5
    )
)


print(
    "\nML predicted segment:",
    predicted_segment
)


print("\nML probabilities:")

for segment, probability in sorted(
    probability_map.items(),
    key=lambda x: x[1],
    reverse=True
):

    print(
        f"{segment:12s} : "
        f"{probability:.4f}"
    )


print("\nTop 5 recommended cars:")


print(
    recommendations[
        [
            "car_id",
            "make",
            "model",
            "segment",
            "price",
            "text_similarity",
            "structured_similarity",
            "ml_segment_probability",
            "final_score"
        ]
    ].to_string(
        index=False
    )
)


# ============================================================
# 12. EVALUATION V7
# ============================================================
# Ground Truth:
# label_segment
#
# Lưu ý:
# label_segment chỉ là proxy để đánh giá.
# Nó không phải dữ liệu booking thực tế.


print("\n" + "=" * 70)
print("EVALUATION V7")
print("=" * 70)


# Lấy 500 user để đánh giá
evaluation_users = users.sample(
    n=500,
    random_state=42
)


hit_5 = 0
precision_5 = []

hit_10 = 0
precision_10 = []


for _, user in evaluation_users.iterrows():

    actual_segment = (
        user["label_segment"]
    )


    # Top 10
    recommendations, _, _ = recommend_v7(
        user,
        top_k=10
    )


    # -----------------------------
    # TOP 5
    # -----------------------------

    top5 = recommendations.head(
        5
    )


    matches_5 = (
        top5["segment"] ==
        actual_segment
    )


    match_count_5 = (
        matches_5.sum()
    )


    if match_count_5 > 0:

        hit_5 += 1


    precision_5.append(
        match_count_5 / 5
    )


    # -----------------------------
    # TOP 10
    # -----------------------------

    top10 = recommendations.head(
        10
    )


    matches_10 = (
        top10["segment"] ==
        actual_segment
    )


    match_count_10 = (
        matches_10.sum()
    )


    if match_count_10 > 0:

        hit_10 += 1


    precision_10.append(
        match_count_10 / 10
    )


# ============================================================
# 13. TÍNH METRICS
# ============================================================

hit_rate_5 = (
    hit_5 /
    len(evaluation_users)
)


precision_at_5 = np.mean(
    precision_5
)


hit_rate_10 = (
    hit_10 /
    len(evaluation_users)
)


precision_at_10 = np.mean(
    precision_10
)


# ============================================================
# 14. IN KẾT QUẢ
# ============================================================

print("\n" + "=" * 70)
print("KẾT QUẢ V7")
print("=" * 70)


print(
    f"\nML Accuracy: "
    f"{ml_accuracy * 100:.2f}%"
)


print(
    f"HitRate@5: "
    f"{hit_rate_5 * 100:.2f}%"
)


print(
    f"Precision@5: "
    f"{precision_at_5 * 100:.2f}%"
)


print(
    f"HitRate@10: "
    f"{hit_rate_10 * 100:.2f}%"
)


print(
    f"Precision@10: "
    f"{precision_at_10 * 100:.2f}%"
)


# ============================================================
# 15. TÓM TẮT
# ============================================================

print("\n" + "=" * 70)
print("TÓM TẮT KIẾN TRÚC V7")
print("=" * 70)

print("""
V7 sử dụng 3 nguồn thông tin:

1. TF-IDF
   -> Phân tích preferences_text của user
   -> So sánh với mô tả của xe

2. Structured Similarity
   -> So sánh đặc điểm user với đặc điểm xe
   -> Ví dụ: số ghế, phân khúc, giá, an toàn...

3. Random Forest
   -> Học từ dữ liệu user
   -> Dự đoán xác suất từng segment
   -> Xác suất này được đưa vào Final Score

Công thức:

Final Score =
    0.4 * Text Similarity
  + 0.4 * Structured Similarity
  + 0.2 * ML Segment Probability

Điểm quan trọng:

V7 KHÔNG lọc xe theo predicted_segment.

Toàn bộ xe được đưa vào ranking.

ML chỉ đóng vai trò bổ sung tín hiệu
cho quá trình xếp hạng.
""")