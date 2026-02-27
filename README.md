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
    'primaryColor': '#1f6feb',
    'primaryTextColor': '#fff',
    'primaryBorderColor': '#1f6feb',
    'lineColor': '#8b949e',
    'doneTaskBkgColor': '#238636',
    'doneTaskBorderColor': '#2ea043',
    'activeTaskBkgColor': '#1f6feb',
    'activeTaskBorderColor': '#388bfd',
    'critTaskBkgColor': '#da3633',
    'critTaskBorderColor': '#f85149',
    'taskBkgColor': '#8957e5',
    'taskBorderColor': '#a371f7',
    'milestoneBkgColor': '#d29922',
    'milestoneBorderColor': '#e3b341',
    'taskTextLightColor': '#ffffff',
    'taskTextDarkColor': '#ffffff',
    'taskTextOutsideColor': '#8b949e',
    'titleColor': '#58a6ff'
  },
  'gantt': {
    'barHeight': 40,
    'barGap': 10,
    'fontSize': 16,
    'sectionFontSize': 18
  }
}}%%
gantt
    title Paralelní zpracování dotazu (Pipeline)
    dateFormat  s
    axisFormat  %S s
    
    section LLM (Ollama)
    Start                   :done, llm1, 0, 1
    1. věta                 :done, llm2, 1, 2
    Generování 2. věty      :active, llm3, 2, 4
    Generování zbytku...    :llm4, 4, 10
    
    section TTS (XTTS)
    Čeká na text            :milestone, 0, 2
    Syntéza 1. věty (~1.6s) :done, tts1, 2, 4
    Syntéza 2. věty         :active, tts2, 4, 6
    Syntéza zbytku...       :tts3, 6, 10
    
    section Unity (Hráč)
    Ticho (Zpracování)      :crit, u1, 0, 4
    Přehrává 1. větu        :done, u2, 4, 6
    Přehrává 2. větu        :active, u3, 6, 8
    Přehrává zbytek...      :u4, 8, 10
```
## Ukázka 
![Ukázka konverzace](images/UnityShowcase.gif)
    
---

## Spuštění


## Struktura projektu
```
├── Backend/
│   ├── api/
│   │   └── src/                  
│   │       ├── models/           # Pydantic schémata a datové modely
│   │       ├── controllers.py    # Hlavní logika (propojení TTS, STT, LLM)
│   │       ├── main.py           # Vstupní bod serveru (FastAPI app)
│   │       ├── routers.py        # Definice API endpointů
│   │       └── chat_history.json # Ukládání historie konverzace
│   │
│   ├── docker/                   # Dockerfiles a docker-compose
│   ├── .dockerignore
│   ├── .env_example              # Šablona pro proměnné prostředí
│   ├── .python-version           # Definice verze Pythonu
│   ├── makefile                  # Příkazy pro snadné spouštění
│   ├── pyproject.toml            # Definice projektu a závislostí
│   └── uv.lock                   # Uzamčené verze balíčků (uv)
│
├── docs/                         # Dokumentace projektu
│   ├── Showcase_videos/          # Video ukázky 
│   ├── images/                   # Obrázky do README 
│   ├── api.md                    # Dokumentace backendu
│   ├── unity_client.md           # Dokumentace frontendu
│   └── README.md                 # Dokumentace uvnitř složky docs
│
├── UnityClient/                  # Frontend (Unity 3D projekt)
│
├── .gitattributes                # Nastavení pro Git (např. LFS pro videa)
├── .gitignore                    # Definice ignorovaných souborů
├── LICENSE                       # Licence projektu
└── README.md                     # Hlavní přehled projektu
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
