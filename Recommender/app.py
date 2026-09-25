from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse

from models import RecommendRequest, RecommendResponse
from scoring import RecommendError, recommend

app = FastAPI(title="Car Rental Recommender", version="1.0.0")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/recommend", response_model=RecommendResponse)
def post_recommend(request: RecommendRequest) -> RecommendResponse:
    try:
        items = recommend(request)
    except RecommendError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    return RecommendResponse(items=items)


@app.exception_handler(HTTPException)
async def http_error(_, exc: HTTPException) -> JSONResponse:
    return JSONResponse(status_code=exc.status_code, content={"message": exc.detail})
