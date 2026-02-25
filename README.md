# AI - Voice Assistant

**AI Assistant** je pokročilý hlasový asistent, jehož backend běží lokálně v Dockeru. Projekt kombinuje moderní LLM pro generování textu, neurální sítě pro syntézu řeči a rozpoznávání hlasu s interaktivním 3D avatarem v Unity.

Cílem projektu je vytvořit konverzačního partnera s **nízkou latencí**, **českým jazykem** a **vyjádřením emocí**.

## Funkce 

* **LLM :** Lokální LLM běžící v **Ollama** 
* **Hlas (TTS):** Real-time syntéza hlasu pomocí lokální služby **XTTS** (HTTP API).
* **Speech-to-text (STT):** Rychlý přepis řeči pomocí **Faster-Whisper** .
* **Asynchronní jádro:** Backend postavený na **FastAPI** s plnou podporou `async/await` pro paralelní zpracování více uživatelů.
* **Unity Frontend:** 3D Avatar s lip-syncem.

## Technologie

### Backend
* Python, FastAPI, Uvicorn
* **uv** 

### AI modely 
* **LLM:** model v Ollama (např. `llama3.1:latest`)
* **TTS:** XTTS 
* **STT:** Faster-Whisper 

### Infrastruktura
* Docker, Docker Compose
* NVIDIA CUDA 11.8/12.x

### Frontend
* Unity 2022+ 
###  Architektura projektu

```mermaid
graph TD
    User((Uživatel))

    subgraph Client ["Frontend (Unity)"]
        Unity[Unity Avatar]
        InputDecider{"Zvuk / Text"}
    end

    User -- "Mluví (Audio)" --> InputDecider
    User -- "Píše (Text)" --> InputDecider
    InputDecider --> Unity

    Unity -- "HTTP Request" --> API

    subgraph Server ["Backend Services (Docker)"]
        API[FastAPI Router]
        
        CheckInput{"Audio nebo<br/>Text?"}
        
        STT["Faster-Whisper<br/>(Audio to Text)"]
        LLM["Ollama<br/>(Generování odpovědi)"]
        TTS["XTTS<br/>(Text to Speech)"]
    end

    %% --- TOK DAT V BACKENDU ---
    API --> CheckInput
    
    %% Cesta 1: Zvuk
    CheckInput -- "Je to Zvuk (WAV)" --> STT
    STT -- "Přepsaný text" --> LLM
    
    %% Cesta 2: Text (přímá)
    CheckInput -- "Je to Text" --> LLM

    %% Zpracování odpovědi
    LLM -- "Odpověď (text)" --> TTS
    TTS -- "Audio Stream (WAV)" --> API

    %% --- VÝSTUP ZPĚT K UŽIVATELI ---
    API -- "Audio" --> Unity
    Unity -- "Synchronizace rtů a Zvuk" --> User 
```

```mermaid
%%{init: {
  'theme': 'base',
  'themeVariables': {
    'doneTaskBkgColor': '#90EE90', 'doneTaskBorderColor': '#228B22',
    'activeTaskBkgColor': '#87CEEB', 'activeTaskBorderColor': '#4682B4',
    'critTaskBkgColor': '#FF6347', 'critTaskBorderColor': '#B22222',
    'taskBkgColor': '#DDA0DD', 'taskBorderColor': '#8B008B',
    'milestoneBkgColor': '#DDA0DD', 'milestoneBorderColor': '#8B008B',
    'taskTextDarkColor': '#000000', 'taskTextLightColor': '#000000'
  },
  'gantt': {
    'barHeight': 35, 'barGap': 15, 'fontSize': 14
  }
}}%%
gantt
    title Paralelní zpracování dotazu (Pipeline)
    dateFormat  s
    axisFormat  %S s
    
    section LLM (Ollama)
    Start (TTFT ~0.6s)     :done, llm1, 0, 1
    Dokončení 1. věty      :done, llm2, 1, 2
    Generování 2. věty     :active, llm3, 2, 4
    Generování zbytku...   :llm4, 4, 10
    
    section TTS (XTTS)
    Čeká na text           :milestone, 0, 2
    Syntéza 1. věty (~1.6s):done, tts1, 2, 4
    Syntéza 2. věty        :active, tts2, 4, 6
    Syntéza zbytku...      :tts3, 6, 10
    
    section Unity (Hráč)
    Ticho (Zpracování)     :crit, u1, 0, 4
    Přehrává 1. větu       :done, u2, 4, 6
    Přehrává 2. větu       :active, u3, 6, 8
    Přehrává zbytek...     :u4, 8, 10
```
## Ukázka 
![Ukázka konverzace](images/UnityShowcase.gif)
    
---

## Spuštění


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
│   │
│   ├── docker/                   # Dockerfiles
│   │
│   ├── makefile                  # Příkazy pro snadné spouštění
│   ├── pyproject.toml            # Definice projektu a závislostí
│   └── uv.lock                   # Uzamčené verze python balíčků (uv)
│
└── UnityClient/                  # Frontend (Unity 3D projekt)
```

V kořenovém adresáři `Backend` vytvořte soubor `.env`. 

PowerShell:
```powershell
cd .\Backend\
Copy-Item .env_example .env
```


**env konfigurace:**

- `OLLAMA_HOST` – URL na Ollama server 
- `OLLAMA_MODEL` – název modelu v Ollama 
- `TTS_API_URL` – URL na XTTS server
- `XTTS_LANGUAGE` – jazyk, typicky `cs`
- `VOICE_ID_AI` – cesta k referenčnímu audio souboru pro AI hlas
- `VOICE_ID_PLAYER` – cesta k referenčnímu audio souboru pro hlas hráče
- `WHISPER_API_URL` – URL na Faster-Whisper server
- `WHISPER_MODEL_SIZE` – velikost modelu 


#### Spuštění backendu
```{bash}
cd .\Backend\
make dev
```

Tím se spustí

- **API** na portu `8000`

#### API Endpoints

- `POST /tts` - Dostane text, který následně vrátí jako audio stream (WAV).
- `POST /stt_file` - Převod nahraného audio souboru na text (Whisper/Faster-Whisper).
- `GET /get_history` - Výpis historie konverzace.  
- `DELETE /delete_history` - Smazání historie.  
- `POST /chat_realtime` - Realtime komunikace mezi uživatelem a AI pomocí binárního audio streamu (Unity klient).  
- `POST /stop_chat` - Zastaví AI generaci.
