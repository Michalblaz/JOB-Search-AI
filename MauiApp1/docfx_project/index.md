# JOB Search - dokumentacja kodu

Ta dokumentacja jest przygotowana pod DocFX i opiera się na komentarzach XML dodanych do reprezentatywnych klas projektu.

Najważniejsze obszary opisane w kodzie:

- wyszukiwanie, filtrowanie i ranking ofert pracy,
- profil użytkownika oraz magazyny kont,
- integracja z PostgreSQL,
- opcjonalne dopasowanie ofert przez Gemini,
- importer normalizujący dane z wielu źródeł.

## Jak wygenerować stronę

```powershell
dotnet tool install -g docfx
docfx docfx_project/docfx.json --serve
```

Po uruchomieniu serwera dokumentacja będzie dostępna pod adresem:

```text
http://localhost:8080
```
