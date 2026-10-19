# ============================================================
# V7 - ML-ASSISTED CONTENT-BASED CAR RECOMMENDATION (engine)
# ============================================================
# Final Score =
#     0.4 * Text Similarity
#   + 0.4 * Structured Similarity
#   + 0.2 * ML Segment Probability
#
# Logic and weights are preserved from v6.py (V7).
# Model/TF-IDF/structured features are built once at init.
# ============================================================

from __future__ import annotations

from typing import Any

import numpy as np
import pandas as pd
from datasets import load_dataset
from sklearn.compose import ColumnTransformer
from sklearn.ensemble import RandomForestClassifier
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.metrics.pairwise import cosine_similarity
from sklearn.model_selection import train_test_split
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import OneHotEncoder, StandardScaler

from history import blend, history_affinity


FEATURE_COLUMNS = [
    "age",
    "kids",
    "family_size",
    "income_bracket",
    "city_type",
    "commute_km_day",
    "charging_access",
    "annual_km",
    "usage_primary",
    "budget_monthly",
]

CATEGORICAL_FEATURES = [
    "income_bracket",
    "city_type",
    "usage_primary",
]

NUMERICAL_FEATURES = [
    "age",
    "kids",
    "family_size",
    "commute_km_day",
    "charging_access",
    "annual_km",
    "budget_monthly",
]

STRUCTURED_COLUMNS = [
    "segment",
    "fuel_type",
    "seats",
    "price",
    "trunk_l",
    "safety_score",
    "km_per_l",
]

NUMERIC_CAR_COLUMNS = [
    "seats",
    "price",
    "trunk_l",
    "safety_score",
    "km_per_l",
]

TARGET_COLUMN = "label_segment"


def create_car_description(row: pd.Series) -> str:
    return (
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


def _to_python(value: Any) -> Any:
    if isinstance(value, (np.floating, float)):
        return float(value)
    if isinstance(value, (np.integer, int)):
        return int(value)
    if isinstance(value, (np.bool_, bool)):
        return bool(value)
    if pd.isna(value):
        return None
    return value


class CarRecommendationEngine:
    """V7 recommendation engine. Heavy work happens once in __init__."""

    def __init__(self) -> None:
        self._train_count = 0
        self._ready = False

        cars_dataset = load_dataset("Mayab2/carfit-ai-synthetic", "cars")
        users_dataset = load_dataset("Mayab2/carfit-ai-synthetic", "users")

        self.cars = cars_dataset["train"].to_pandas()
        self.users = users_dataset["train"].to_pandas()

        X = self.users[FEATURE_COLUMNS].copy()
        y = self.users[TARGET_COLUMN].copy()

        preprocessor = ColumnTransformer(
            transformers=[
                ("num", StandardScaler(), NUMERICAL_FEATURES),
                ("cat", OneHotEncoder(handle_unknown="ignore"), CATEGORICAL_FEATURES),
            ]
        )

        rf_model = RandomForestClassifier(
            n_estimators=200,
            random_state=42,
            class_weight="balanced",
        )

        self.ml_pipeline = Pipeline(
            steps=[
                ("preprocessor", preprocessor),
                ("classifier", rf_model),
            ]
        )

        X_train, _, y_train, _ = train_test_split(
            X,
            y,
            test_size=0.2,
            random_state=42,
            stratify=y,
        )

        self.ml_pipeline.fit(X_train, y_train)
        self._train_count += 1

        self.segment_of_car_id = dict(zip(self.cars["car_id"].astype(str), self.cars["segment"]))
        self.cars["car_description"] = self.cars.apply(create_car_description, axis=1)

        self.tfidf_vectorizer = TfidfVectorizer(lowercase=True, ngram_range=(1, 2))
        self.car_tfidf = self.tfidf_vectorizer.fit_transform(self.cars["car_description"])

        cars_structured = self.cars[STRUCTURED_COLUMNS].copy()
        cars_structured = pd.get_dummies(
            cars_structured,
            columns=["segment", "fuel_type"],
        )

        self.scaler = StandardScaler()
        cars_structured[NUMERIC_CAR_COLUMNS] = self.scaler.fit_transform(
            cars_structured[NUMERIC_CAR_COLUMNS]
        )
        self.cars_structured = cars_structured.astype(float)

        self._ready = True

    @property
    def is_ready(self) -> bool:
        return self._ready

    @property
    def train_count(self) -> int:
        """How many times Random Forest was fitted (must stay 1 after startup)."""
        return self._train_count

    def build_user_profile(self, user: dict[str, Any]) -> dict[str, Any]:
        family_size = int(user["family_size"])
        kids = int(user["kids"])

        if kids > 0:
            preferred_seats = max(family_size + 1, 4)
        else:
            preferred_seats = max(family_size, 2)

        return {
            "age": user["age"],
            "kids": kids,
            "family_size": family_size,
            "income_bracket": user["income_bracket"],
            "city_type": user["city_type"],
            "commute_km_day": user["commute_km_day"],
            "charging_access": user["charging_access"],
            "annual_km": user["annual_km"],
            "usage_primary": user["usage_primary"],
            "budget_monthly": user["budget_monthly"],
            "preferred_seats": preferred_seats,
            "preferences_text": user["preferences_text"],
        }

    def calculate_structured_similarity(self, user_profile: dict[str, Any]) -> np.ndarray:
        usage = str(user_profile["usage_primary"]).lower()

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

        user_df = pd.DataFrame(
            [
                {
                    "segment": user_segment,
                    "fuel_type": "Petrol",
                    "seats": user_profile["preferred_seats"],
                    "price": user_profile["budget_monthly"],
                    "trunk_l": 400,
                    "safety_score": 8,
                    "km_per_l": 15,
                }
            ]
        )

        user_df = pd.get_dummies(user_df, columns=["segment", "fuel_type"])
        user_df = user_df.reindex(columns=self.cars_structured.columns, fill_value=0)
        user_df[NUMERIC_CAR_COLUMNS] = self.scaler.transform(user_df[NUMERIC_CAR_COLUMNS])
        user_df = user_df.astype(float)

        return cosine_similarity(user_df, self.cars_structured)[0]

    def recommend(
        self,
        user: dict[str, Any],
        top_k: int = 5,
        history: list[dict[str, Any]] | None = None,
        history_weight: float = 0.0,
    ) -> dict[str, Any]:
        """
        Rank all cars with V7 final score; return JSON-serializable Top-K.
        Does not hard-filter by predicted_segment.
        Optional rental history is blended AFTER the V7 score (history.py); weight 0 = pure V7.
        """
        if top_k < 1:
            raise ValueError("top_k must be >= 1")

        user_profile = self.build_user_profile(user)

        user_text = [str(user_profile["preferences_text"])]
        user_tfidf = self.tfidf_vectorizer.transform(user_text)
        text_scores = cosine_similarity(user_tfidf, self.car_tfidf)[0]

        structured_scores = self.calculate_structured_similarity(user_profile)

        user_ml_df = pd.DataFrame([user_profile])[FEATURE_COLUMNS]
        probabilities = self.ml_pipeline.predict_proba(user_ml_df)[0]
        classes = self.ml_pipeline.named_steps["classifier"].classes_
        probability_map = dict(zip(classes, probabilities))

        ml_segment_scores = np.array(
            [probability_map.get(segment, 0) for segment in self.cars["segment"]]
        )

        predicted_segment = self.ml_pipeline.predict(user_ml_df)[0]

        # V7 weights — unchanged
        final_scores = (
            0.4 * text_scores
            + 0.4 * structured_scores
            + 0.2 * ml_segment_scores
        )

        result = self.cars[
            [
                "car_id",
                "make",
                "model",
                "segment",
                "price",
                "fuel_type",
                "seats",
                "safety_score",
            ]
        ].copy()
        result["text_similarity"] = text_scores
        result["structured_similarity"] = structured_scores
        result["ml_segment_probability"] = ml_segment_scores

        affinity = history_affinity(
            self.cars["segment"], self.cars["seats"], history or [], self.segment_of_car_id)
        result["v7_score"] = final_scores
        result["history_affinity"] = affinity
        result["final_score"] = blend(final_scores, affinity, history_weight)

        result = result.sort_values("final_score", ascending=False)
        result = result.head(top_k).reset_index(drop=True)

        recommendations: list[dict[str, Any]] = []
        for _, row in result.iterrows():
            recommendations.append(
                {
                    "car_id": _to_python(row["car_id"]),
                    "make": _to_python(row["make"]),
                    "model": _to_python(row["model"]),
                    "segment": _to_python(row["segment"]),
                    "price": _to_python(row["price"]),
                    "fuel_type": _to_python(row["fuel_type"]),
                    "seats": _to_python(row["seats"]),
                    "safety_score": _to_python(row["safety_score"]),
                    "text_similarity": _to_python(row["text_similarity"]),
                    "structured_similarity": _to_python(row["structured_similarity"]),
                    "ml_segment_probability": _to_python(row["ml_segment_probability"]),
                    "v7_score": _to_python(row["v7_score"]),
                    "history_affinity": _to_python(row["history_affinity"]),
                    "final_score": _to_python(row["final_score"]),
                }
            )

        return {
            "predicted_segment": str(predicted_segment),
            "recommendations": recommendations,
            "ml_probabilities": {
                str(k): float(v) for k, v in probability_map.items()
            },
            "train_count": self._train_count,
        }
