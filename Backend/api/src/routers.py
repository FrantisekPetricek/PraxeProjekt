from fastapi import APIRouter, UploadFile, File
from fastapi.responses import StreamingResponse
from models.model import TTSRequest, ChatRequest, HistoryResponse
import shutil
import os
import uuid

from controllers import (
    transcribe_audio_remote,
    get_chat_history,
    delete_history,
    stream_ai_realtime,
    text_to_speech_generator_async,
    VOICE_ID_AI,
    VOICE_ID_PLAYER,
)

router = APIRouter()


@router.post("/tts", tags=["TTS"])
async def endpoint_tts(request: TTSRequest) -> StreamingResponse:
    """
    Endpoint pro hráče: Přečte text a vrátí rovnou Audio stream (WAV).
    """
    print(f"[TTS Player]: {request.text}")

    audio_stream = text_to_speech_generator_async(request.text, VOICE_ID_PLAYER)

    return StreamingResponse(audio_stream, media_type="audio/wav")


@router.post("/stt_file", tags=["STT"])
async def endpoint_stt_file(file: UploadFile = File(...)):
    """
    Přepis audia ze souboru (Whisper).
    """
    temp_filename = f"temp_{uuid.uuid4()}.wav"
    
    try:
        with open(temp_filename, "wb") as buffer:
            shutil.copyfileobj(file.file, buffer)

        text = await transcribe_audio_remote(temp_filename)
        return {"text": text}
    finally:
        if os.path.exists(temp_filename):
            os.remove(temp_filename)


@router.get("/get_history", tags=["History"])
async def get_chat_history_edp() -> HistoryResponse:
    return get_chat_history()


@router.delete("/delete_history", tags=["History"])
async def delete_history_edp() -> dict:
    return delete_history()


@router.post("/chat_realtime", tags=["Chat"])
async def endpoint_chat_realtime(request: ChatRequest):
    """
    Ultra-rychlý stream pro Unity (Text + Audio pakety).
    """
    print(f"[REALTIME CHAT]: {request.user_question}")

    # Voláme generátor
    audio_generator = stream_ai_realtime(request.user_question, VOICE_ID_AI)

    # Vracíme stream
    return StreamingResponse(audio_generator, media_type="application/octet-stream")
