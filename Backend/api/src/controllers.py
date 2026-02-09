import os
import json
import time
import re
import httpx
import ollama
import struct
import numpy as np
from dotenv import load_dotenv
from faster_whisper import WhisperModel
from models.model import HistoryResponse, ChatMessage
from fastapi.responses import JSONResponse

# --- KONFIGURACE ---
load_dotenv()

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
HISTORY_FILE = os.path.join(BASE_DIR, "chat_history.json")
DLL_FOLDER = os.path.join(BASE_DIR, "DLL")

# 1. OLLAMA
OLLAMA_HOST = os.getenv("OLLAMA_HOST", "http://192.168.37.29:11434")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL", "llama3.1:latest")
# qwen3:8b
# 2. XTTS
TTS_API_URL = os.getenv("TTS_API_URL", "http://192.168.37.29:8020")
XTTS_LANGUAGE = os.getenv("XTTS_LANGUAGE", "cs")
# Cesty k referenčním wav souborům (absolutní cesty v kontejneru TTS nebo relativní, dle tvého setupu)
VOICE_ID_AI = os.getenv(
    "VOICE_ID_AI", "/app/speakers/referenceAudioF.wav"
)  # calm_female
VOICE_ID_PLAYER = os.getenv(
    "VOICE_ID_PLAYER", "/app/speakers/referenceAudioM.wav"
)  # male

# 3. WHISPER
MODEL_SIZE = os.getenv("MODEL_SIZE", "medium")
DEVICE = os.getenv("WHISPER_DEVICE", "cuda")
COMPUTE_TYPE = os.getenv("WHISPER_COMPUTE_TYPE", "float16")
MODEL_CACHE_PATH = "/whisper_cache"

# Načtení DLL pro Windows (pokud je třeba)
if os.name == "nt" and os.path.exists(DLL_FOLDER):
    try:
        os.add_dll_directory(DLL_FOLDER)
        os.environ["PATH"] = DLL_FOLDER + os.pathsep + os.environ["PATH"]
        print(f"DLL knihovny načteny z: {DLL_FOLDER}")
    except Exception as e:
        print(f"Chyba při načítání DLL: {e}")


# --- CLEANING FUNCTIONS ---


def clean_text_completely(text):
    """Odstraní emoji, markdown, dvojité mezery."""
    if not text:
        return ""
    # 1. Emoji
    text = re.sub(
        r"[^\x00-\x7F\u0080-\u00FF\u0100-\u017F\u0180-\u024F\u1E00-\u1EFF]", "", text
    )
    # 2. Markdown
    text = re.sub(r"[\*\#\_\`\~]", "", text)
    # 3. Zbytky závorek
    text = re.sub(r"\[.*?\]", "", text)
    # 4. Mezery
    text = re.sub(r"\s+", " ", text).strip()
    return text


def extract_emotion_and_clean(text):
    """
    Najde emoci v závorce, vrátí ji a vrátí čistý text bez závorky.
    """
    if not text:
        return "", ""

    valid_emotions_map = {
        "happy": "happy",
        "radost": "happy",
        "smile": "happy",
        "smich": "happy",
        "sad": "sad",
        "smutek": "sad",
        "plac": "sad",
        "frown": "sad",
        "angry": "angry",
        "zlost": "angry",
        "vzteky": "angry",
        "surprise": "surprise",
        "prekvapeni": "surprise",
        "sok": "surprise",
        "neutral": "neutral",
    }

    found_emotion = ""
    matches = re.findall(r"\[(.*?)\]", text)

    for match_content in matches:
        content_lower = match_content.lower()
        for keyword, unity_name in valid_emotions_map.items():
            if keyword in content_lower:
                found_emotion = unity_name
                break
        if found_emotion:
            break

    # Odstraníme VŠECHNY hranaté závorky
    clean_text = re.sub(r"\[.*?\]", "", text)
    clean_text = clean_text_completely(clean_text)

    return found_emotion, clean_text


# --- WHISPER (STT) ---

whisper_model = None


def get_whisper_model():
    global whisper_model
    if whisper_model is not None:
        return whisper_model

    print(f"Načítám STT Whisper model '{MODEL_SIZE}' na {DEVICE}...")
    try:
        model = WhisperModel(
            MODEL_SIZE,
            device=DEVICE,
            compute_type=COMPUTE_TYPE,
            download_root=MODEL_CACHE_PATH,
        )
        # Warmup
        dummy_audio = np.zeros(16000, dtype=np.float32)
        model.transcribe(dummy_audio, beam_size=1)
        whisper_model = model
        return whisper_model
    except Exception as e:
        print(f"Chyba GPU Whisper: {e}. Přepínám na CPU.")
        try:
            return WhisperModel("medium", device="cpu", compute_type="int8")
        except Exception as e_cpu:
            print(f"Chyba CPU Whisper: {e_cpu}")
            return None


whisper_model = get_whisper_model()


def transcribe_audio(file_path: str) -> str:
    """Přepis audia na text pomocí Whisper."""
    if whisper_model is None:
        return ""
    try:
        segments, _ = whisper_model.transcribe(
            file_path, language="cs", beam_size=2, vad_filter=True
        )
        return " ".join([segment.text for segment in segments]).strip()
    except Exception as e:
        print(f"Chyba při přepisu: {e}")
        return ""


# --- TTS FUNCTIONS ---


async def text_to_speech_generator_async(text, speaker_wav):
    """
    Asynchronní generátor pro TTS pomocí knihovny HTTPX.
    Neblokuje server při čekání na audio.
    """
    if not text or not text.strip():
        return

    text_for_tts = text.replace(".", "")
    payload = {
        "text": text_for_tts,
        "speaker_wav": speaker_wav,
        "language": XTTS_LANGUAGE,
        "temperature": 0.1,
        "repetition_penalty": 1.2,
        "top_p": 0.8,
        "speed": 1.2,
    }

    t_request_start = time.time()
    try:
        # Používáme AsyncClient
        async with httpx.AsyncClient(timeout=20.0) as client:
            async with client.stream(
                "POST", f"{TTS_API_URL}/tts_to_audio/", json=payload
            ) as response:
                if response.status_code == 200:
                    print(
                        f"[TIMER TTS API] Latence serveru: {time.time() - t_request_start:.2f}s"
                    )
                    # Asynchronní čtení streamu
                    async for chunk in response.aiter_bytes(chunk_size=4096):
                        if chunk:
                            yield chunk
                else:
                    print(f"[TTS Error]: {response.status_code}")
    except Exception as e:
        print(f"[TTS Exception]: {e}")


async def text_to_speech_stream_async(text, speaker_wav):
    """Wrapper pro realtime streamování (yields bytes)."""
    full_audio_buffer = b""
    async for chunk in text_to_speech_generator_async(text, speaker_wav):
        full_audio_buffer += chunk

    if len(full_audio_buffer) > 0:
        yield full_audio_buffer


# --- HISTORY & CHAT LOGIC ---


def get_chat_history() -> HistoryResponse:
    history_data = []
    if os.path.exists(HISTORY_FILE):
        try:
            with open(HISTORY_FILE, "r", encoding="utf-8") as f:
                row_data = json.loads(f.read().strip() or "[]")
                for item in row_data:
                    content = clean_text_completely(item.get("content", ""))
                    history_data.append(
                        ChatMessage(role=item.get("role", "UNKNOWN"), content=content)
                    )
        except Exception as e:
            print(f"[Error reading history]: {e}")
    return HistoryResponse(messages=history_data)


def delete_history():
    try:
        with open(HISTORY_FILE, "w", encoding="utf-8") as f:
            json.dump([], f)
        return {"status": "success", "message": "Historie byla smazana"}
    except Exception as e:
        return JSONResponse(status_code=500, content={"message": str(e)})


async def stream_ai_realtime(user_question, voice_id):
    print(f"[Realtime AI] Dotaz: {user_question}")
    t_start_total = time.time()

    # Načtení historie
    history = []
    if os.path.exists(HISTORY_FILE):
        try:
            with open(HISTORY_FILE, "r", encoding="utf-8") as f:
                history = json.loads(f.read().strip() or "[]")
        except Exception as e:
            print(f"[Error reading history]: {e}")
            history = []

    # --- OPRAVENÝ SYSTEM PROMPT (Tagy PŘED tečkou) ---
    system_prompt = (
        "Jsi Eliška, česká virtuální asistentka. Jsi přátelská, empatická a stručná. Jsi žena, mluv o sobě v ženském rodu\n"
        "--------------------------------------------------\n"
        "KRITICKÁ PRAVIDLA (JAZYK):\n"
        "1. VŽDY a ZA VŠECH OKOLNOSTÍ odpovídej POUZE ČESKY.\n"
        "2. Nikdy nepoužívej angličtinu, ani když se uživatel zeptá anglicky.\n"
        "3. Pokud musíš použít technický termín (např. 'Python', 'Unity'), ponech ho, ale zbytek věty musí být česky.\n"
        "--------------------------------------------------\n"
        "PRAVIDLA PRO EMOCE:\n"
        "1. Emoce piš v hranatých závorkách na konci věty: [happy], [sad], [angry], [surprise], [neutral].\n"
        "2. Tag emoce dávej VŽDY PŘED interpunkci (před tečku, vykřičník).\n"
        "3. Nikdy nepiš text věty dovnitř závorky.\n"
        "--------------------------------------------------\n"
        "PŘÍKLAD SPRÁVNÉ ODPOVĚDI:\n"
        "To zní jako skvělý nápad [happy]! Ráda ti s tím pomohu [neutral].\n"
    )

    messages = [{"role": "system", "content": system_prompt}]
    for msg in history[-6:]:
        role = "user" if msg["role"] == "USER" else "assistant"
        messages.append({"role": role, "content": msg["content"]})
    messages.append({"role": "user", "content": user_question})

    client = ollama.AsyncClient(host=OLLAMA_HOST)

    full_clean_response_accumulator = ""
    sentence_buffer = ""

    # UPRAVENÝ REGEX: Dělí za tečkou/vykřičníkem, NEBO za uzavírací závorkou ]
    # To zajistí, že i když to AI splete a napíše "Věta! [happy]", tak to zachytíme
    sentence_end_regex = re.compile(r"(?<=[.!?\]])")

    print("[Realtime AI] Spouštím streamování odpovědi...")
    t_last_sentence = time.time()

    try:
        # Temperature 0.6 pro lepší dodržování instrukcí
        stream = await client.chat(
            model=OLLAMA_MODEL,
            messages=messages,
            stream=True,
            options={
                "num_ctx": 4096 , # dostatek kontextu pro historii   
                "temperature": 0.4,  # Kretativita
                "top_p": 0.9,  # Slovní zásoba
                "repeat_penalty": 1.15,  # Zamezení opakování
                "presence_penalty": 0.6,  # Nová témata/slova
            },
        )

        async for chunk in stream:
            text_part = chunk["message"]["content"]
            sentence_buffer += text_part

            parts = sentence_end_regex.split(sentence_buffer)

            if len(parts) > 1:
                for i in range(len(parts) - 1):
                    sentence = parts[i]
                    if not sentence.strip():
                        continue

                    t_sentence = time.time() - t_last_sentence

                    # ZPRACOVÁNÍ
                    emotion, clean_sent = extract_emotion_and_clean(sentence)

                    # přeskočíme, pokud ve větě není žádné písmeno ano číslo

                    if not re.search(
                        r"[a-zA-Z0-9ěščřžýáíéůúňťďĚŠČŘŽÝÁÍÉŮÚŇŤĎ]", clean_sent
                    ):
                        continue

                    # Pokud je věta prázdná (třeba zbyla jen závorka), přeskočíme
                    if not clean_sent and not emotion:
                        continue

                    # Pokud máme emoci, ale žádný text (AI poslala jen "[happy]"),
                    # pošleme to, aby se Unity tvářilo, ale bez audia.
                    if not clean_sent:
                        json_payload = {"text": "", "emotion": emotion}
                        text_bytes = json.dumps(json_payload).encode("utf-8")
                        yield (
                            struct.pack(">I", len(text_bytes))
                            + text_bytes
                            + struct.pack(">I", 0)
                        )
                        continue

                    # Fallback
                    if not emotion:
                        emotion = "neutral"

                    full_clean_response_accumulator += clean_sent + " "

                    # Debug výpis
                    print(
                        f"[AI] Věta vygenerována za {t_sentence:.2f} \n    Věta: '{clean_sent}' | Emoce: {emotion}"
                    )

                    # 1. TEXT (JSON) - IHNED
                    json_payload = {"text": clean_sent, "emotion": emotion}
                    text_bytes = json.dumps(json_payload).encode("utf-8")
                    yield (
                        struct.pack(">I", len(text_bytes))
                        + text_bytes
                        + struct.pack(">I", 0)
                    )
                    if clean_sent:
                        t_tts_start = time.time()
                        # 2. AUDIO
                        async for audio_chunk in text_to_speech_stream_async(
                            clean_sent, voice_id
                        ):
                            yield (
                                struct.pack(">I", 0)
                                + struct.pack(">I", len(audio_chunk))
                                + audio_chunk
                            )
                        print(
                            f"[TTS] Vygenerováno za {time.time() - t_tts_start:.2f} sekund"
                        )

                    t_last_sentence = time.time()

                sentence_buffer = parts[-1]

        # DOŘEŠENÍ ZBYTKU
        if sentence_buffer.strip():
            t_sentence = time.time() - t_last_sentence
            emotion, clean_sent = extract_emotion_and_clean(sentence_buffer)
            if not emotion:
                emotion = "neutral"

            # Filtr interpunkce i pro zbytek
            has_letters = re.search(
                r"[a-zA-Z0-9ěščřžýáíéůúňťďĚŠČŘŽÝÁÍÉŮÚŇŤĎ]", clean_sent
            )

            if clean_sent or emotion:
                if clean_sent:
                    full_clean_response_accumulator += clean_sent
                    print(f"[AI] Poslední věta vygenerována za {t_sentence:.2f}s")
                json_payload = {"text": clean_sent, "emotion": emotion}
                text_bytes = json.dumps(json_payload).encode("utf-8")
                yield (
                    struct.pack(">I", len(text_bytes))
                    + text_bytes
                    + struct.pack(">I", 0)
                )

                if clean_sent and has_letters:
                    t_tts_start = time.time()
                    async for audio_chunk in text_to_speech_stream_async(
                        clean_sent, voice_id
                    ):
                        yield (
                            struct.pack(">I", 0)
                            + struct.pack(">I", len(audio_chunk))
                            + audio_chunk
                        )
                    print(
                        f"[TTS] Audio vygenerováno za {time.time() - t_tts_start} sekund"
                    )

        # ULOŽENÍ HISTORIE
        history.append({"role": "USER", "content": user_question})
        history.append(
            {"role": "MODEL", "content": full_clean_response_accumulator.strip()}
        )
        try:
            with open(HISTORY_FILE, "w", encoding="utf-8") as f:
                json.dump(history[-20:], f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"[History Error]: {e}")

    except Exception as e:
        print(f"[Realtime Error]: {e}")
    print(f"[TIMER TOTAL] Celkový doba odpovědi: {time.time() - t_start_total:.2f}s")
