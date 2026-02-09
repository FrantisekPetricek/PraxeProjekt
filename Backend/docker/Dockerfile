# 1. Stage: Export requirements z pyproject.toml
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim AS export-reqs
WORKDIR /tmp
# Kopírujeme definice závislostí z kořenového adresáře (díky contextu v compose to půjde)
COPY pyproject.toml uv.lock ./
# Vygenerujeme requirements.txt
RUN uv export --format requirements-txt --output-file requirements.txt

# 2. Stage: Instalace knihoven do čistého adresáře
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim AS deps
WORKDIR /tmp
COPY --from=export-reqs /tmp/requirements.txt /tmp/requirements.txt
# Instalace do složky /install
RUN uv pip install --system --prefix=/install -r /tmp/requirements.txt

# 3. Stage: Finální image (Runtime)
FROM ghcr.io/astral-sh/uv:python3.13-bookworm-slim AS run-stage

WORKDIR /app

# Proměnné prostředí
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PATH="/usr/local/bin:$PATH" \
    # Nastavení cesty pro Nvidia knihovny (aby je Whisper našel)
    LD_LIBRARY_PATH=/usr/local/lib/python3.13/site-packages/nvidia/cudnn/lib:/usr/local/lib/python3.13/site-packages/nvidia/cublas/lib:$LD_LIBRARY_PATH

# Instalace systémových nástrojů (FFmpeg)
RUN apt-get update && apt-get install -y \
    ffmpeg \
    libsndfile1 \
    && rm -rf /var/lib/apt/lists/*

# Zkopírujeme před-nainstalované python knihovny z kroku 2
COPY --from=deps /install /usr/local

# Zkopírujeme zdrojový kód (ze složky src do /app)
COPY api/src .

EXPOSE 8000

# Spuštění aplikace
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--reload", "--port", "8000"]