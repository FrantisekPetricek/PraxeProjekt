# AI - Voice Assistant

**AI Assistant** je pokročilý hlasový asistent běžící kompletně lokálně (bez cloudu). Projekt kombinuje moderní LLM pro generování textu, neurální sítě pro syntézu řeči (TTS) a rozpoznávání hlasu (STT) s interaktivním 3D avatarem v Unity.

Cílem projektu je vytvořit konverzačního partnera s **nízkou latencí**, **českým jazykem** a **vyjádřením emocí**.

## Funkce

* **LLM Mozek:** Využívá **Llama 3.1** (přes Ollama) pro generování inteligentních odpovědí v češtině.
* **Hlas (TTS):** Real-time syntéza hlasu pomocí **Coqui XTTS v2** (klonování hlasu).
* **Uši (STT):** Rychlý přepis řeči pomocí **Faster-Whisper** (běží na GPU).
* **Asynchronní Jádro:** Backend postavený na **FastAPI** s plnou podporou `async/await` pro paralelní zpracování více uživatelů.
* **Unity Frontend:** 3D Avatar s lip-syncem (synchronizace rtů) a animacemi podle emocí z textu.

## Technologie

### Backend
* Python, FastAPI, Uvicorn
* **uv** (moderní package manager)

### AI Modely
* **LLM:** Llama 3.1 (8B) / Llama 3.2 (3B)
* **TTS:** XTTS-v2
* **STT:** Faster-Whisper (Large-v3 / Medium)

### Infrastruktura
* Docker, Docker Compose
* NVIDIA CUDA 11.8/12.x

### Frontend
* Unity 2022+ (C#)

---

## Instalace a Spuštění

### Požadavky
* **NVIDIA GPU** (Doporučeno min. 8GB VRAM).
* **Docker & Docker Compose**.
* **Ollama** běžící na hostitelském PC (nebo v kontejneru).

## Struktura projektu
```
├── api/
│    └── src                  # Backend (FastAPI)
│       ├── models/
│               └──model.py   # Pydantic schémata
│       ├── routers.py          # API endpointy
│       ├── controllers.py    # Logika AI (TTS, STT, LLM)
│       └── main.py           # Vstupní bod         
├── docker-compose.yml    # Konfigurace kontejnerů
└── uv.lock               # Uzamčené verze závislostí
```