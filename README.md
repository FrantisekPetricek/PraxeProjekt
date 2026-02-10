# AI - Voice Assistant

**AI Assistant** je pokročilý hlasový asistent běžící kompletně lokálně (bez cloudu). Projekt kombinuje moderní LLM pro generování textu, neurální sítě pro syntézu řeči (TTS) a rozpoznávání hlasu (STT) s interaktivním 3D avatarem v Unity.

Cílem projektu je vytvořit konverzačního partnera s **nízkou latencí**, **českým jazykem** a **vyjádřením emocí**.

## Funkce

* **LLM :** Využívá **Llama 3.1** (přes Ollama) pro generování inteligentních odpovědí v češtině.
* **Hlas (TTS):** Real-time syntéza hlasu pomocí **Coqui XTTS v2** (klonování hlasu).
* **Speach to text:** Rychlý přepis řeči pomocí **Faster-Whisper**.
* **Asynchronní Jádro:** Backend postavený na **FastAPI** s plnou podporou `async/await` pro paralelní zpracování více uživatelů.
* **Unity Frontend:** 3D Avatar s lip-syncem (synchronizace rtů) a animacemi podle emocí z textu.

## Technologie

### Backend
* Python, FastAPI, Uvicorn
* **uv** (moderní package manager)

### AI Modely
* **LLM:** Llama 3.1:latest
* **TTS:** XTTS-v2
* **STT:** Faster-Whisper (Medium)

### Infrastruktura
* Docker, Docker Compose
* NVIDIA CUDA 11.8/12.x

### Frontend
* Unity 2022+ (C#)
###  Architektura projektu

```mermaid
graph TD
    %% --- DEFINICE STYLŮ ---
    classDef ai fill:#f9f,stroke:#333,stroke-width:2px;
    classDef unity fill:#222,stroke:#fff,stroke-width:2px,color:#fff;
    classDef backend fill:#69b3a2,stroke:#333,stroke-width:2px;
    classDef decision fill:#ffcc00,stroke:#333,stroke-width:2px,color:#000;

    User((Uživatel))

    
    subgraph Client ["Frontend (Unity)"]
        Unity[Unity Avatar]:::unity
        InputDecider{"Zvuk / Text"}:::unity
    end

    User -- "Mluví (Audio)" --> InputDecider
    User -- "Píše (Text)" --> InputDecider
    InputDecider --> Unity

    Unity -- "Request" --> API

    
    subgraph Server ["Backend Services"]
        API[FastAPI Router]:::backend
        
        CheckInput{"Audio nebo<br/>Text?"}:::decision
        
        STT["Faster-Whisper<br/>(Audio to Text)"]:::ai
        LLM["Llama 3.1<br/>(Generování odpovědi)"]:::ai
        TTS["Coqui XTTS<br/>(Text to Speech)"]:::ai
    end

    %% --- TOK DAT V BACKENDU ---
    API --> CheckInput
    
    %% Cesta 1: Zvuk
    CheckInput -- "Je to Zvuk" --> STT
    STT -- "Přepsaný text" --> LLM
    
    %% Cesta 2: Text (přímá)
    CheckInput -- "Je to Text" --> LLM

    %% Zpracování odpovědi
    LLM -- "Odpověď (stream po větách)" --> TTS
    TTS -- "Audio Stream (WAV)" --> API

    %% --- VÝSTUP ZPĚT K UŽIVATELI ---
    API -- "Audio" --> Unity
    Unity -- "Synchronizace rtů a Zvuk" --> User
```

## Ukázka 
![Ukázka konverzace](images/UnityShowcase.gif)
    
---

## Spuštění

### Požadavky
* **NVIDIA GPU** (Doporučeno min. 8GB VRAM).
* **Docker & Docker Compose**.
* **Ollama** běžící na hostitelském PC (nebo v kontejneru).

## Struktura projektu
```
├── Backend/
│   ├── api/
│   │   ├── src/                  
│   │   │   ├── models/           # Pydantic schémata a datové modely
│   │   │   ├── controllers.py    # Hlavní logika (propojení TTS, STT, LLM)
│   │   │   ├── main.py           # Vstupní bod serveru (FastAPI app)
│   │   │   ├── routers.py        # Definice API endpointů
│   │   │   └── chat_history.json # Ukládání historie konverzace
│   │   └── whisper_cache/        # Cache pro stažené modely Whisperu
│   │
│   ├── docker/                   # Dockerfiles
│   │
│   ├── makefile                  # Příkazy pro snadné spouštění
│   ├── pyproject.toml            # Definice projektu a závislostí
│   └── uv.lock                   # Uzamčené verze python balíčků (uv)
│
└── UnityClient/                  # Frontend (Unity 3D projekt)
```

Vytvořte soubor `.env` v adresáří backend 
``` 
cd .\Backend\
cp .\.env_example .env
```

#### Spuštění backendu
```{bash}
cd .\Backend\
make dev
```

Tím se spustí

- **API** na portu `8000`

#### API Endpoints

- `POST /tts` - Dostane text který následně vrátí jako audio stream (WAV)
- `POST /stt_file` - Převod hlasu na audio (Whisper)
- `GET /get_history` - Výpis hisotrie  
- `DELETE /delete_history` - Smazání historie  
- `POST /chat_realtime` - Komunikace mezi uživatelem a AI pomocí audio streamu  
