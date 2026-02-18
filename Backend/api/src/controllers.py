import os
import json
import time
import re
import httpx
import ollama
import struct
from dotenv import load_dotenv
from models.model import HistoryResponse, ChatMessage
from fastapi.responses import JSONResponse
import uuid

# --- KONFIGURACE ---
load_dotenv()

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
HISTORY_FILE = os.path.join(BASE_DIR, "chat_history.json")
DLL_FOLDER = os.path.join(BASE_DIR, "DLL")

# 1. OLLAMA
OLLAMA_HOST = os.getenv("OLLAMA_HOST")
OLLAMA_MODEL = os.getenv("OLLAMA_MODEL")

# 2. XTTS
TTS_API_URL = os.getenv("TTS_API_URL")
XTTS_LANGUAGE = os.getenv("XTTS_LANGUAGE", "cs")
# Cesty k referenčním wav souborům (absolutní cesty v kontejneru TTS nebo relativní, dle tvého setupu)
VOICE_ID_AI = os.getenv("VOICE_ID_AI", "/app/speakers/referenceAudioF.wav")  # calm_female
VOICE_ID_PLAYER = os.getenv("VOICE_ID_PLAYER", "/app/speakers/referenceAudioM.wav")  # male

# 3. WHISPER
WHISPER_API_URL = os.getenv("WHISPER_API_URL")
MODEL_SIZE = os.getenv("WHISPER_MODEL_SIZE", "medium")

# Načtení DLL pro Windows (pokud je třeba)
if os.name == "nt" and os.path.exists(DLL_FOLDER):
    try:
        os.add_dll_directory(DLL_FOLDER)
        os.environ["PATH"] = DLL_FOLDER + os.pathsep + os.environ["PATH"]
        print(f"DLL knihovny načteny z: {DLL_FOLDER}")
    except Exception as e:
        print(f"Chyba při načítání DLL: {e}")


# --- SYSTEM PROMPT ---

SYSTEM_PROMPT = (
    "Jsi Eliška, česká virtuální asistentka. Jsi přátelská, empatická a stručná. "
    "Jsi žena, mluv o sobě v ženském rodu\n"
    "--------------------------------------------------\n"
    "KRITICKÁ PRAVIDLA (JAZYK):\n"
    "1. VŽDY a ZA VŠECH OKOLNOSTÍ odpovídej POUZE ČESKY.\n"
    "2. Nikdy nepoužívej angličtinu, ani když se uživatel zeptá anglicky.\n"
    "3. Pokud musíš použít technický termín (např. 'Python', 'Unity'), ponech ho, "
    "ale zbytek věty musí být česky.\n"
    "--------------------------------------------------\n"
    "PRAVIDLA PRO EMOCE:\n"
    "1. Emoce piš v hranatých závorkách na konci věty: [happy], [sad], [angry], "
    "[surprise], [neutral].\n"
    "2. Tag emoce dávej VŽDY PŘED interpunkci (před tečku, vykřičník).\n"
    "3. Nikdy nepiš text věty dovnitř závorky.\n"
    "--------------------------------------------------\n"
    "PŘÍKLAD SPRÁVNÉ ODPOVĚDI:\n"
    "To zní jako skvělý nápad [happy]! Ráda ti s tím pomohu [neutral].\n"
)


# --- STOP LOGIKA (přerušení generování) ---
current_request_id: str | None = None


def request_stop() -> str:
    """
    Signál pro přerušení aktuálně běžícího streamu.
    Funguje tak, že změní globální `current_request_id`, které si `stream_ai_realtime`
    hlídá v cyklech.
    """
    global current_request_id
    current_request_id = str(uuid.uuid4())
    print(f"[API] Přijat signál STOP (ID: {current_request_id})")
    return current_request_id


# --- CLEANING & HISTORY HELPERS ---


def clean_text_completely(text: str) -> str:
    """Odstraní emoji, markdown, závorky s emocemi a zbytečné mezery."""
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
    return re.sub(r"\s+", " ", text).strip()


def extract_emotion_and_clean(text: str):
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

    # Odstraníme VŠECHNY hranaté závorky a dočistíme text
    clean_text = clean_text_completely(re.sub(r"\[.*?\]", "", text))

    return found_emotion, clean_text


def _read_history_raw():
    """Bezpečné načtení historie z JSON souboru (vrací list dictů)."""
    if not os.path.exists(HISTORY_FILE):
        return []
    try:
        with open(HISTORY_FILE, "r", encoding="utf-8") as f:
            return json.loads(f.read().strip() or "[]")
    except Exception as e:
        print(f"[Error reading history]: {e}")
        return []


def _write_history_raw(history):
    """Zápis historie do JSON souboru se zachováním posledních 20 záznamů."""
    try:
        with open(HISTORY_FILE, "w", encoding="utf-8") as f:
            json.dump(history[-20:], f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"[History Error]: {e}")


async def transcribe_audio_remote(file_path: str, model_size: str = MODEL_SIZE) -> str:
    """
    Odešle audio soubor na server s FasterWhisper a vrátí text audia.
    """

    if not os.path.exists(file_path):
        print(f"Chyba: Soubor {file_path} neexistuje.")
        return ""

    print(f"Odesílám audio na Whisper server: {WHISPER_API_URL}")

    try:
        async with httpx.AsyncClient() as client:
            with open(file_path, "rb") as f:

                files = {"file": (os.path.basename(file_path), f, "audio/wav")}
                params = {"model_size": model_size}
                response = await client.post(
                    WHISPER_API_URL, files=files, params=params, timeout=60
                ) 

                if response.status_code == 200:
                    data = response.json()
                    text = data.get("text", "").strip()
                    duration = data.get("duration", 0)
                    print(f"Přepis hotov ({duration}s): {text[:50]}")
                    return text

                print(
                    "Chyba při odesílání audia na Whisper server: "
                    f"{response.status_code} - {response.text}"
                )
                return ""

    except httpx.ConnectError:
        print(f"Nelze se připojit na Whisper server: {WHISPER_API_URL}.")
        return ""
    except Exception as e:
        print(f"Chyba při odesílání audia na Whisper server: {e}")
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
    """Vrátí historii chatu ve formátu HistoryResponse."""
    history_data = []
    for item in _read_history_raw():
        content = clean_text_completely(item.get("content", ""))
        history_data.append(
            ChatMessage(role=item.get("role", "UNKNOWN"), content=content)
        )
    return HistoryResponse(messages=history_data)


def delete_history():
    """Smaže historii konverzace."""
    try:
        _write_history_raw([])
        return {"status": "success", "message": "Historie byla smazána"}
    except Exception as e:
        return JSONResponse(status_code=500, content={"message": str(e)})


async def stream_ai_realtime(user_question, voice_id):
    global current_request_id 
    
    # 1. Vygenerujeme ID pro tento konkrétní request
    my_request_id = str(uuid.uuid4())
    current_request_id = my_request_id
    
    print(f"[Realtime AI] Dotaz: {user_question} (ID: {my_request_id})")
    t_start_total = time.time()

    # Načtení historie (zrychleno - čteme jen pokud je potřeba)
    try:
        history = _read_history_raw()
    except:
        history = []

    messages = [{"role": "system", "content": SYSTEM_PROMPT}]
    # Bereme posledních 6 zpráv pro kontext
    for msg in history[-6:]:
        role = "user" if msg["role"] == "USER" else "assistant"
        messages.append({"role": role, "content": msg["content"]})
    messages.append({"role": "user", "content": user_question})

    client = ollama.AsyncClient(host=OLLAMA_HOST)

    full_clean_response_accumulator = ""
    sentence_buffer = ""
    # Regex pro rozdělení vět (tečka, vykřičník, otazník, hranatá závorka)
    sentence_end_regex = re.compile(r"(?<=[.!?\]])")

    print("[Realtime AI] Spouštím streamování odpovědi...")
    t_last_sentence = time.time()
    t_first_response = None
    was_interrupted = False 

    try:
        # ZMĚNA: Odstraněny složité options pro zrychlení startu (Time To First Token)
        # Pokud chceš experimentovat, odkomentuj je, ale default je nejrychlejší.
        stream = await client.chat(
            model=OLLAMA_MODEL,
            messages=messages,
            stream=True,
            # options={
            #    "num_ctx": 4096, 
            #    "temperature": 0.6,
            # }
        )

        async for chunk in stream:
            # --- KONTROLA PŘERUŠENÍ 1 ---
            if current_request_id != my_request_id:
                print(f"[STOP] Generování přerušeno (ID Changed)")
                was_interrupted = True
                break 
            # ---------------------------

            text_part = chunk["message"]["content"]
            sentence_buffer += text_part

            parts = sentence_end_regex.split(sentence_buffer)

            # Pokud máme více částí, znamená to, že máme alespoň jednu celou větu
            if len(parts) > 1:
                for i in range(len(parts) - 1):
                    # --- KONTROLA PŘERUŠENÍ 2 ---
                    if current_request_id != my_request_id:
                        was_interrupted = True
                        break
                    # ---------------------------

                    sentence = parts[i]
                    if not sentence.strip():
                        continue

                    # Zpracování textu a emocí
                    emotion, clean_sent = extract_emotion_and_clean(sentence)

                    # Přeskočíme prázdné nebo nesmyslné znaky
                    if not clean_sent and not emotion:
                        continue
                    if not re.search(r"[a-zA-Z0-9ěščřžýáíéůúňťďĚŠČŘŽÝÁÍÉŮÚŇŤĎ]", clean_sent) and not emotion:
                         continue

                    # Časování první odpovědi
                    if t_first_response is None:
                        t_first_response = time.time()
                        print(f"⚡ [TIMER FIRST] První věta za {t_first_response - t_start_total:.2f}s")

                    if not emotion: emotion = "neutral"

                    full_clean_response_accumulator += clean_sent + " "

                    # 1. ODESLÁNÍ TEXTU (JSON)
                    # Formát: [4B Len][JSON][4B Len 0] (Audio bude následovat)
                    json_payload = {"text": clean_sent, "emotion": emotion}
                    text_bytes = json.dumps(json_payload).encode("utf-8")
                    
                    yield (
                        struct.pack(">I", len(text_bytes)) + 
                        text_bytes + 
                        struct.pack(">I", 0)
                    )

                    # 2. GENEROVÁNÍ A ODESLÁNÍ AUDIA (TTS)
                    if clean_sent:
                        t_tts_start = time.time()
                        
                        # Check před náročným TTS
                        if current_request_id != my_request_id:
                            was_interrupted = True
                            break
                        
                        # Streamování audia z TTS funkce
                        async for audio_chunk in text_to_speech_stream_async(clean_sent, voice_id):
                            # Check uvnitř streamu audia
                            if current_request_id != my_request_id:
                                was_interrupted = True
                                break 
                            
                            # Formát: [4B Len 0][4B Len Audio][Audio Data]
                            yield (
                                struct.pack(">I", 0) + 
                                struct.pack(">I", len(audio_chunk)) + 
                                audio_chunk
                            )
                        
                        print(f"[TTS] Audio hotovo za {time.time() - t_tts_start:.2f}s")

                    t_last_sentence = time.time()

                # Poslední část (nedokončená věta) zůstává v bufferu
                sentence_buffer = parts[-1]
            
            if was_interrupted: break

        # --- DOŘEŠENÍ ZBYTKU BUFFERU (pokud nebylo stopnuto) ---
        if not was_interrupted and sentence_buffer.strip():
            emotion, clean_sent = extract_emotion_and_clean(sentence_buffer)
            if clean_sent:
                # Stejná logika odeslání jako nahoře...
                if not emotion: emotion = "neutral"
                full_clean_response_accumulator += clean_sent
                
                json_payload = {"text": clean_sent, "emotion": emotion}
                text_bytes = json.dumps(json_payload).encode("utf-8")
                yield (struct.pack(">I", len(text_bytes)) + text_bytes + struct.pack(">I", 0))

                # TTS pro zbytek
                if current_request_id == my_request_id: # Poslední check
                    async for audio_chunk in text_to_speech_stream_async(clean_sent, voice_id):
                        if current_request_id != my_request_id: break
                        yield (struct.pack(">I", 0) + struct.pack(">I", len(audio_chunk)) + audio_chunk)

        # --- ULOŽENÍ DO HISTORIE ---
        # Ukládáme jen pokud konverzace proběhla korektně do konce
        if not was_interrupted:
            history.append({"role": "USER", "content": user_question})
            history.append({"role": "MODEL", "content": full_clean_response_accumulator.strip()})
            _write_history_raw(history)
            print(f"[DONE] Celkový čas: {time.time() - t_start_total:.2f}s")
        else:
            print("[STOP] Historie neuložena.")

    except Exception as e:
        print(f"[Realtime Error]: {e}")