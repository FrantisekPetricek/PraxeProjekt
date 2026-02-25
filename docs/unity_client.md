# Dokumentace k Unity Klientu (Frontend)

Tato část dokumentace popisuje architekturu Unity klienta, který slouží jako frontend pro AI Voice Assistanta. Klient zaznamenává hlas hráče, komunikuje s lokálním FastAPI backendem, rozbaluje vlastní binární stream a ovládá 3D avatara (lip-sync a emoce).



##  Požadavky a Externí balíčky
Pro správné fungování projektu je nutné mít v Unity nainstalované následující závislosti:

* **TextMeshPro:** Pro vykreslování UI a titulků.
* **Crazy Minnow SALSA LipSync Suite:** Využíváno **pouze pro emoce** (komponenta `Emoter`).
* **Oculus (OVR) LipSync:** Využíváno pro synchronizaci rtů na platformě Windows.
* **uLipSync:** Využíváno jako alternativa pro synchronizaci rtů na platformě Linux.

---

##   Architektura hlavních skriptů

Systém je rozdělen do několika specializovaných manažerů, které spolu komunikují přes události (Events) nebo přímé reference.

### `RuntimeTTS.cs` (Hlavní mozek)
Tento skript řídí celý životní cyklus jedné konverzace.
1.  Přijímá text/hlas od hráče.
2.  Otevírá asynchronní `UnityWebRequest` na endpoint `/chat_realtime`.
3.  V reálném čase stahuje bajty a pomocí stavového automatu (`ParseState`) je dělí na JSON hlavičky a WAV data.
4.  Plní frontu `dialogueQueue` objekty typu `DialogueSegment`.
5.  Přehrává věty přes `AudioSource` jednu po druhé a předává text do `SubtitleManager`u a emoce do `EmotionController`.

### `STTManager.cs` 
Stará se o nahrávání hlasu přes `Microphone.Start`. 
* Po puštění klávesy zvuk ořízne (odstraní prázdné ticho na konci).
* Převede záznam na WAV a odešle jej jako `multipart/form-data` na `/stt_file`.
* Přes event `OnTranscriptionComplete` předá rozpoznaný text zpět do `RuntimeTTS`.
* Obsahuje metodu `GetCurrentVolume()` pro vizualizaci hlasitosti v UI.

### `EmotionController.cs` 
Tento skript funguje jako most mezi JSON odpovědí z AI a systémem SALSA.
* Přijímá jednoduché stringy (např. `"happy"`, `"radost"`, `"sad"`).
* Mapuje je na specifické názvy pro `Emoter` (např. `"Smile"`, `"Frown"`).
* Obsahuje bezpečný `try-catch` blok – pokud modelu chybí nějaký *Blendshape*, hra nespadne, ale pouze vypíše varování.

### `SubtitleManager.cs` 
Řídí zobrazování textu na obrazovce.
* Zobrazuje co hráč řekl a co NPC odpovídá.
* Dokáže dlouhé texty chytře rozdělit na stránky (`SplitTextToPages`) podle maximálního počtu znaků.

---

##  Jak funguje přijímání streamu (Smart Stream Parser)

Srdcem klienta je metoda `ProcessSmartStream` ve skriptu `RuntimeTTS`. Protože backend odesílá data po kouscích (chunks) ještě předtím, než je AI domluvené, Unity musí umět tento nekonečný tok dat za běhu rozkouskovat.

Využívá se k tomu stavový automat (State Machine) se čtyřmi stavy:

1.  **`ReadTextLen`**: Čeká na 4 bajty. Pokud přijdou, převede je na `int` (délka následujícího JSON textu). Pokud je délka `0`, znamená to konec textové části a přechází se na čtení délky audia.
2.  **`ReadText`**: Přečte přesný počet bajtů, převede je na UTF-8 string a přes `JsonUtility` (nebo Regex) z nich vytáhne čistý text a emoci.
3.  **`ReadAudioLen`**: Čeká na 4 bajty, které určují velikost následujícího WAV souboru.
4.  **`ReadAudio`**: Přečte WAV data, pomocí `WavUtility.ToAudioClip` z nich vytvoří `AudioClip` v paměti a vloží jej spolu s textem do přehrávací fronty (`dialogueQueue`).

*Jakmile je první věta ve frontě, Coroutina `PlayDialogueQueue` ji okamžitě začne přehrávat, zatímco streamování dalších dat běží nerušeně dál.*

---

##   Přerušení konverzace (Stop Logic)

Protože je backend asynchronní, hráč může AI kdykoliv přerušit klávesou **Escape**. Systém to řeší pomocí inkrementálního ID (`currentGenerationId`):

1.  Při stisku klávesy se zavolá `StopAI()`.
2.  Zvýší se `currentGenerationId`. 
3.  Všechny aktuálně běžící smyčky `while(!operation.isDone)` a `while(npcAudioSource.isPlaying)` zjistí, že ID nesouhlasí, a samy se okamžitě ukončí (tzv. "Graceful shutdown").
4.  Odešle se asynchronní (prázdný) HTTP POST požadavek na endpoint `/stop_chat`, aby přestal generovat i vzdálený server a šetřil výkon GPU.

---

##  Struktura hlavní scény

Scéna je již kompletně sestavena. Pro snadnější orientaci v projektu je zde popis klíčových objektů (GameObjects) v hierarchii:

### Logika a Manažeři
* **`Managers`**: Hlavní řídící uzel. Obsahuje klíčové skripty jako `RuntimeTTS`, `STTManager`, `SubtitleManager` a `EmotionController`.
* **`DialogManager`**: Oddělený systém pro přehrávání offline (předem nahraných) dialogů.
* **`LipSyncManager`**: Centrální objekt pro správu synchronizace rtů (OVR / uLipSync).

### Uživatelské rozhraní (`Canvas`)
Obsahuje veškeré 2D prvky obrazovky:
* `TitlesOutput`: Textová pole pro vykreslování titulků.
* `UserInput` / `InputButton`: Prvky pro manuální psaní dotazů.
* `ChatHistory` / `HistoryButton`: Tlačítko a okno pro zobrazení historie konverzace.
* `Settings` / `SettingsButton`: Konfigurační menu (nastavení mikrofonu, API endpointů).
* `ErrorPopup` / `PousePanel`: Vyskakovací chybová hlášení a menu pro pozastavení hry (Quit popup).

### Postavy (Avataři)
3D modely, které interagují ve scéně:
* **`Player`**: Objekt hráče. Může obsahovat vlastní `AudioSource` pro zpětné přehrávání hlasu.
* **`NPC` / `NPC2`**: Samotní AI asistenti. Každé NPC obsahuje svou kostru (`Bip01`), 3D mesh (`hipoly_bones`), skript pro ovládání očí/hlavy, komponentu pro emoce (SALSA) a podelement **`lipSync`** pro animaci úst. Tváře jsou dodatečně nasvíceny objektem **`faceLight`**.