import os
import json
import uuid
import httpx
import re
import glob
from datetime import datetime
from dotenv import load_dotenv
from models.model import ChatMessage, HistoryResponse

# Cloud API
from groq import Groq
from elevenlabs.client import ElevenLabs

load_dotenv()

# --- KONFIGURACE ---
HISTORY_FILE = "chat_history.json"
TEMP_DIR = "temp"
MAX_TEMP_FILES = 2
MAX_HISTORY_LENGTH = 20

# --- API Klienti a Diagnostika ---
groq_api_key = os.getenv("GROQ_API_KEY")
eleven_api_key = os.getenv("ELEVENLABS_API_KEY")

print(" DIAGNOSTIKA KLÍČŮ:")
print(f"   Groq Key: {'Nalezen' if groq_api_key else ' CHYBÍ!'}")
print(f"   ElevenLabs Key: {'Nalezen' if eleven_api_key else ' CHYBÍ!'}")

if not groq_api_key or not eleven_api_key:
    print("VAROVÁNÍ: Chybí API klíče v .env souboru! Server nebude fungovat správně.")

groq_client = Groq(api_key=groq_api_key)
eleven_client = ElevenLabs(api_key=eleven_api_key)

# Hlasy
VOICE_NPC = os.getenv("ELEVENLABS_VOICE_ID_NPC")
VOICE_PLAYER = os.getenv("ELEVENLABS_VOICE_ID_PLAYER")

# Whisper URL
WHISPER_API_URL = os.getenv("WHISPER_API_URL", "http://whisper_gpu:8000/transcribe")

os.makedirs(TEMP_DIR, exist_ok=True)

# --- SYSTEM PROMPT ---
SYSTEM_PROMPT = (
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


def _read_history_file():
    """
    Načte syrová data historie z JSON souboru.
    V případě chyby vrací prázdný seznam a zaloguje chybu.
    """
    if not os.path.exists(HISTORY_FILE):
        return []
    try:
        with open(HISTORY_FILE, "r", encoding="utf-8") as f:
            content = f.read().strip()
            return json.loads(content) if content else []
    except Exception as e:
        print(f"[Error reading history]: {e}")
        return []


def extract_emotion(text: str, default: str = "neutral") -> str:
    """
    Vrátí emoci zapsanou v hranatých závorkách, např. [happy].
    Pokud žádná emoce není nalezena, vrátí default.
    """
    match = re.search(r"\[(.*?)\]", text)
    return match.group(1) if match else default


def cleanup_temp_folder(keep_last=MAX_TEMP_FILES):
    """
    Ponechá ve složce temp jen posledních N souborů. Zbytek smaže.
    """
    try:
        files = glob.glob(os.path.join(TEMP_DIR, "*"))

        if len(files) <= keep_last:
            return
        files.sort(key=os.path.getctime)

        files_to_delete = files[:-keep_last]

        for f in files_to_delete:
            try:
                os.remove(f)
            except Exception as e:
                print(f"Nešlo smazat {f}: {e}")

    except Exception as e:
        print(f"Chyba při čištění temp složky: {e}")


def clean_text_completely(text: str) -> str:
    """Odstraní závorky s emocemi a ořízne text."""
    if not text:
        return ""
    return re.sub(r"\[.*?\]", "", text).strip()


def get_chat_history() -> HistoryResponse:
    history_data = []
    row_data = _read_history_file()

    for item in row_data:
        clean_content = clean_text_completely(item.get("content", ""))
        history_data.append(
            ChatMessage(
                role=item.get("role", "UNKNOWN"),
                content=clean_content,
            )
        )

    return HistoryResponse(messages=history_data)


def delete_history():
    try:
        with open(HISTORY_FILE, "w", encoding="utf-8") as f:
            json.dump([], f)
        return {"status": "success", "message": "Historie byla smazana"}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def save_to_history(role: str, content: str):
    data = _read_history_file()

    data.append(
        {"role": role, "content": content, "timestamp": datetime.now().isoformat()}
    )

    if len(data) > MAX_HISTORY_LENGTH:
        data = data[-MAX_HISTORY_LENGTH:]

    with open(HISTORY_FILE, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=4)


def generate_audio_elevenlabs(text: str, voice_id: str, prefix: str = "tts") -> str:

    cleanup_temp_folder()

    clean_text = clean_text_completely(text)
    if not clean_text:
        return ""

    if not voice_id:
        print("Chybí voice_id pro TTS, audio nebude vygenerováno.")
        return ""

    print(f"TTS ({prefix}): {clean_text[:30]}...")
    filename = f"{prefix}_{uuid.uuid4().hex}.mp3"
    output_path = os.path.join(TEMP_DIR, filename)

    try:
        audio_generator = eleven_client.text_to_speech.convert(
            text=clean_text,
            voice_id=voice_id,
            model_id="eleven_multilingual_v2",
            output_format="mp3_44100_128",
        )

        with open(output_path, "wb") as f:
            for chunk in audio_generator:
                f.write(chunk)

        print(f"Audio uloženo: {output_path}")
        return output_path

    except Exception as e:
        print(f"Chyba TTS: {e}")
        if hasattr(e, "body"):
            print(f"   Detail chyby: {e.body}")
        return ""


def process_player_tts(text: str) -> str:
    save_to_history("user", text)
    return generate_audio_elevenlabs(text, VOICE_PLAYER, prefix="player")


async def process_npc_chat(user_text: str):
    messages = [{"role": "system", "content": SYSTEM_PROMPT}]

    raw_history = _read_history_file()

    for item in raw_history[-10:]:
        role = (
            "assistant"
            if item.get("sender") == "ai" or item.get("role") == "ai"
            else "user"
        )
        messages.append(
            {
                "role": role,
                "content": item.get("content", "") or item.get("message", ""),
            }
        )

    messages.append({"role": "user", "content": user_text})

    print("AI Groq přemýšlí")
    try:
        completion = groq_client.chat.completions.create(
            model="llama-3.3-70b-versatile",
            messages=messages,
            temperature=0.7,
            max_tokens=256,
        )
        ai_text = completion.choices[0].message.content
    except Exception as e:
        print(f"Chyba Groq: {e}")
        ai_text = "Omlouvám se, mám výpadek spojení [sad]."

    save_to_history("ai", ai_text)

    emotion = extract_emotion(ai_text, default="neutral")

    audio_path = generate_audio_elevenlabs(ai_text, VOICE_NPC, prefix="npc")

    clean_text_for_unity = clean_text_completely(ai_text)

    return {
        "text": clean_text_for_unity,
        "audio_url": audio_path,
        "emotion": emotion,
    }


async def transcribe_audio_remote(file_path: str) -> str:
    if not os.path.exists(file_path):
        return ""

    print(f"📡 STT: Posílám na {WHISPER_API_URL}")
    try:
        async with httpx.AsyncClient() as client:
            with open(file_path, "rb") as f:
                files = {"file": (os.path.basename(file_path), f, "audio/wav")}
                resp = await client.post(
                    WHISPER_API_URL,
                    files=files,
                    params={"model_size": "medium"},
                    timeout=60,
                )
                return resp.json().get("text", "")
    except Exception as e:
        print(f"Chyba STT: {e}")
        return ""
