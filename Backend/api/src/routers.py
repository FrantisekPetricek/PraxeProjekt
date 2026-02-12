from fastapi import APIRouter, UploadFile, File, HTTPException
from fastapi.responses import FileResponse, JSONResponse
from models.model import ChatRequest, TTSRequest
import shutil
import os

# Import funkcí z controllers
from controllers import (
    process_player_tts, 
    process_npc_chat, 
    transcribe_audio_remote,
    get_chat_history, 
    delete_history    
)

router = APIRouter()

# --- 1. ENDPOINTY PRO AI & ZVUK ---

@router.post("/tts")
async def player_tts_endpoint(request: TTSRequest):
    """Hráčův TTS (vrací soubor)"""
    audio_path = process_player_tts(request.text)
    if os.path.exists(audio_path):
        return FileResponse(audio_path, media_type="audio/mpeg", filename="player.mp3")
    raise HTTPException(status_code=500, detail="Audio generation failed")

@router.post("/chat")
async def chat_endpoint(request: ChatRequest):
    """NPC Chat (vrací JSON s URL audia)"""
    response_data = await process_npc_chat(request.user_question)
    return JSONResponse(content=response_data)

@router.post("/transcribe")
async def stt_endpoint(file: UploadFile = File(...)):
    """STT přes Remote Whisper"""
    temp_filename = f"temp_{file.filename}"
    try:
        with open(temp_filename, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)
        text = await transcribe_audio_remote(temp_filename)
        return {"text": text}
    finally:
        if os.path.exists(temp_filename):
            os.remove(temp_filename)

# --- 2. ENDPOINTY PRO HISTORII (Podle tvého přání) ---

@router.get("/get_history")
async def get_history_endpoint():
    """Vrátí historii chatu"""
    return get_chat_history()

@router.delete("/delete_history")
async def delete_history_endpoint():
    """Smaže historii"""
    result = delete_history()
    if isinstance(result, JSONResponse):
        return result
    return result