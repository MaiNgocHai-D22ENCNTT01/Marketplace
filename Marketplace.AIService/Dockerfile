FROM python:3.13-slim-bookworm
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 PIP_NO_CACHE_DIR=1 \
    PORT=7860 HOME=/home/user MARKETPLACE_INFERENCE_ONLY=true MARKETPLACE_REQUIRE_API_KEY=true
RUN useradd --create-home --uid 1000 user
WORKDIR /app
COPY requirements-runtime.txt .
RUN pip install --only-binary=:all: -r requirements-runtime.txt
COPY --chown=user:user main.py inference.py labels.json mobilenet.onnx ./
USER user
EXPOSE 7860
CMD ["sh", "-c", "exec python -m uvicorn main:app --host 0.0.0.0 --port ${PORT:-7860} --workers 1 --limit-concurrency 128 --no-access-log"]
