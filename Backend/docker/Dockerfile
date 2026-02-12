# 1. Použijeme lehký oficiální Python image (slim verze)
FROM python:3.11-slim

WORKDIR /app

# 2. Instalace systémových závislostí
# PŘIDÁNO: build-essential (obsahuje gcc/cc kompilátory pro instalaci balíčků)
RUN apt-get update && apt-get install -y \
    ffmpeg \
    curl \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# 3. Instalace 'uv'
COPY --from=ghcr.io/astral-sh/uv:latest /uv /bin/uv

# 4. Kopírování definic závislostí
COPY pyproject.toml uv.lock ./

# 5. Instalace
# Vytvoříme requirements.txt z lock souboru
RUN uv export --frozen --no-dev --format requirements-txt > requirements.txt

# Nainstalujeme knihovny (teď už to projde, i když bude muset kompilovat)
RUN uv pip install --system --no-cache -r requirements.txt

# 6. Kopírování zdrojového kódu
COPY api/src .

# 7. Otevření portu
EXPOSE 8000

# 8. Spuštění aplikace
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]