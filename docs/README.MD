# Docs


##  Obsah dokumentace

Pro snadnou orientaci jsme dokumentaci rozdělili do dvou hlavních částí podle architektury projektu:

###  Backend (Python / FastAPI)
* **[Dokumentace k API (api_reference.md)](api.md)**
  * Obsahuje detailní rozpis všech dostupných REST endpointů.
  * **Klíčová část:** Detailní popis našeho unikátního **binárního streamu** pro `/chat_realtime`. 
  * Popis datových struktur (Pydantic modely).

### Frontend (Unity / C#)
*  **[Dokumentace k Unity Klientu (unity_client.md)](unity_client.md)**
  * Vysvětluje architekturu hlavních C# skriptů .
  * Detailně popisuje stavový automat (`ParseState`) pro čtení binárního streamu v reálném čase.
  * Mapuje hierarchii hlavní scény a integraci nástrojů třetích stran.

