from pydantic import BaseModel
from typing import List


class ChatRequest(BaseModel):
    user_question: str


class TTSRequest(BaseModel):
    text: str


class ChatMessage(BaseModel):
    role: str
    content: str


class HistoryResponse(BaseModel):
    messages: List[ChatMessage]
