# Opis dokumentacji

W projekcie nie dokumentowano każdego prostego pola ani każdej pomocniczej klasy DTO. Zamiast tego komentarze XML skupiają się na klasach, które najlepiej pokazują architekturę aplikacji i decyzje projektowe.

Wybrane klasy i obszary:

- `JobSearchService` - główny stan aplikacji, pobieranie ofert, konta, filtry i ranking.
- `JobOffer` - wspólny model ofert z wielu źródeł.
- `UserProfile` i `UserSettings` - preferencje wpływające na filtrowanie i dopasowanie.
- `IUserStore`, `LocalUserStore`, `PostgresUserStore` - zamienny kontrakt trwałości kont.
- `PostgresJobReader` - odczyt ofert zaimportowanych do bazy.
- `GeminiMatchService` - opcjonalny scoring ofert z użyciem modelu AI.
- `JobImportCoordinator`, `PostgresJobRepository`, `ImporterHelpers` - proces importu i normalizacji danych.

Komentarze wykorzystują między innymi znaczniki `summary`, `remarks`, `param`, `returns`, `exception`, `example`, `see`, `seealso` i `value`.
