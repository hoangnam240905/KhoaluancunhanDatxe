# Recommendation Service (V7) — Step 1

Standalone Python HTTP API for the V7 ML-Assisted Content-Based car recommendation algorithm.

**Final Score (unchanged):**

```
Final Score = 0.4 * Text Similarity + 0.4 * Structured Similarity + 0.2 * ML Segment Probability
```

The service loads the Hugging Face dataset `Mayab2/carfit-ai-synthetic`, trains Random Forest **once** at startup, builds TF-IDF and structured features once, then serves recommendations from memory.

This step does **not** integrate with Backend / PortalWeb / SQLite.

## Requirements

- Python 3.10+
- Internet access on first run (to download the dataset)

## Install

```bash
cd RecommendationService
python -m pip install -r requirements.txt
```

## Run

From the `RecommendationService` directory:

```bash
python -m uvicorn app:app --host 127.0.0.1 --port 8000
```

First startup may take a while (dataset download + Random Forest training).

## Health

```bash
curl http://127.0.0.1:8000/health
```

Response:

```json
{ "status": "ok" }
```

## Recommend

`POST /recommend`

### Request body (example)

```json
{
  "age": 25,
  "kids": 0,
  "family_size": 2,
  "income_bracket": "medium",
  "city_type": "urban",
  "commute_km_day": 15,
  "charging_access": 0,
  "annual_km": 10000,
  "usage_primary": "city",
  "budget_monthly": 500,
  "preferences_text": "I need a reliable compact car for city commuting with good fuel economy",
  "top_k": 5
}
```

`top_k` is optional (default `5`).

### curl example

```bash
curl -X POST http://127.0.0.1:8000/recommend ^
  -H "Content-Type: application/json" ^
  -d "{\"age\":25,\"kids\":0,\"family_size\":2,\"income_bracket\":\"medium\",\"city_type\":\"urban\",\"commute_km_day\":15,\"charging_access\":0,\"annual_km\":10000,\"usage_primary\":\"city\",\"budget_monthly\":500,\"preferences_text\":\"compact city car fuel economy\",\"top_k\":5}"
```

### Response (shape)

```json
{
  "success": true,
  "predicted_segment": "Supermini",
  "recommendations": [
    {
      "car_id": "...",
      "make": "...",
      "model": "...",
      "segment": "...",
      "price": 0,
      "fuel_type": "...",
      "seats": 0,
      "safety_score": 0,
      "text_similarity": 0.0,
      "structured_similarity": 0.0,
      "ml_segment_probability": 0.0,
      "final_score": 0.0
    }
  ],
  "train_count": 1
}
```

`train_count` stays `1` across requests (model is not retrained per call).

## Notes

- Ranking runs over **all** cars; `predicted_segment` is not used as a hard filter.
- Invalid / missing fields return HTTP `422` (Pydantic) or `400`.
- OpenAPI docs: http://127.0.0.1:8000/docs
