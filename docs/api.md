# 📚 Dokumentace k API (Voice Backend)

Tento dokument popisuje REST API lokálního backendu poháněného FastAPI. Backend propojuje AI modely pro generování textu (Ollama), převod řeči na text (Faster-Whisper) a syntézu hlasu (XTTS v2).

**Swagger UI (Testování):** `http://localhost:8000`
**Základní URL (Výchozí):** `http://localhost:8000/docs`  

---

##  Real-time Konverzace 

Toto je stěžejní endpoint celého projektu, který zajišťuje ultra-rychlou komunikaci s Unity klientem.

### `POST /chat_realtime`
Přijímá dotaz uživatele a vrací asynchronní **kombinovaný binární stream**, který obsahuje text (po větách) i vygenerované audio (WAV chunky). To umožňuje klientovi přehrávat zvuk a lip-sync ještě předtím, než AI dokončí celou odpověď.

* **Tag:** `Chat`
* **Content-Type:** `application/json`
* **Request Body (JSON):**
  ```json
  {
    "user_question": "Na jakých principech fungují elektrárny?"
  }
  ```
* **Odpověď:** `application/octet-stream` (Binární data)

#### Struktura binárního paketu (Pro C# / Unity Klienta)
Stream je rozdělen do bloků. Každý blok (text nebo audio) začíná 4 bajty (`Int32`, Big-Endian), které určují délku následujících dat.

1. **Textový paket:**
   * `[4 byty]` - Velikost JSON dat.
   * `[N bytů]` - UTF-8 JSON obsahující text a detekovanou emoci.
     * *Příklad obsahu:* `{"text": "Dobrý den!", "emotion": "happy"}`
2. **Oddělovač (Konec textu):**
   * `[4 byty]` - Hodnota `0`. Označuje, že textová část pro danou větu skončila.
3. **Zvukový paket:**
   * `[4 byty]` - Velikost Audio dat.
   * `[M bytů]` - Samotná data `.wav` souboru.

*(Tento cyklus se opakuje pro každou větu, dokud stream neskončí.)*

---

##  Převod řeči na text (STT)

### `POST /stt_file`
Nahraje audio soubor na server a pomocí modelu Faster-Whisper jej přepíše do textu.

* **Tag:** `STT`
* **Content-Type:** `multipart/form-data`
* **Parametry (Form Data):**
  * `file`: Audio soubor (`.wav`).
* **Odpověď (JSON):**
  ```json
  {
    "text": "Přepsaný text od uživatele"
  }
  ```

---

##  Text to Speech (TTS)

### `POST /tts`
Vygeneruje zvukovou stopu z textu pomocí XTTS. 

* **Tag:** `TTS`
* **Content-Type:** `application/json`
* **Request Body (JSON):**
  ```json
  {
    "text": "Zpráva, kterou chci přečíst."
  }
  ```
* **Odpověď:** `audio/wav` (Streamovaný audio soubor)

---

## 4. Přerušení a Kontrola

### `POST /stop_chat`
Odešle signál pro okamžité přerušení právě probíhajícího generování (Ollamy i TTS). Využívá se, když chce uživatel přeskočit odpověď AI nebo začne znovu mluvit.

* **Tag:** `Chat`
* **Odpověď (JSON):**
  ```json
  {
    "status": "stopped",
    "request_id": "3f050839-3247-40ed-a0bf-974b865ae2d5"
  }
  ```

---

## 5. Správa Historie (Paměť)

Backend si interně udržuje kontext posledních 20 zpráv (v souboru `chat_history.json`), aby AI asistentka věděla, o čem se s uživatelem bavila.

### `GET /get_history`
Vrátí historii konverzace. Používá se pro inicializaci chatu po zapnutí Unity klienta.

* **Tag:** `History`
* **Odpověď (JSON):**
  ```json
  {
    "messages": [
      {
        "role": "USER",
        "content": "Ahoj!"
      },
      {
        "role": "MODEL",
        "content": "Ahoj, jak ti mohu pomoci?"
      }
    ]
  }
  ```

### `DELETE /delete_history`
Vymaže kompletní historii konverzace (jak ze souboru, tak z paměti).

* **Tag:** `History`
* **Odpověď (JSON):**
  ```json
  {
    "status": "success",
    "message": "Historie byla smazána"
  }
  ```

---

## Datové Modely (Pydantic)

Při komunikaci s API se využívají následující struktury:

```python
class ChatRequest(BaseModel):
    user_question: str

class TTSRequest(BaseModel):
    text: str

class ChatMessage(BaseModel):
    role: str      # "USER" nebo "MODEL"
    content: str   # Čistý text bez emocí a markdownu

class HistoryResponse(BaseModel):
    messages: List[ChatMessage]
```