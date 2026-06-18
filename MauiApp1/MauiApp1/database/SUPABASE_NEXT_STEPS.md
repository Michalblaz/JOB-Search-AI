# Supabase - kolejne kroki

## 1. Wgraj schemat

Uruchom w SQL Editor:

- `database/postgres_supabase_job_import_schema.sql`

To doda:
- unikalność `source_id + external_id`
- indeksy pod filtrowanie
- trigger `updated_at`
- widok `active_job_offers`

## 2. Używaj importów przez `upsert`

Przykłady są w:

- `database/postgres_supabase_import_examples.sql`

Kluczowa zasada:
- jedna oferta = jeden rekord po `(source_id, external_id)`

## 3. Nie kasuj ofert

Jeśli oferta zniknie z API:
- ustaw `is_active = false`
- zostaw rekord w bazie

## 4. Co powinien robić importer

Każdego dnia:

1. tworzy rekord w `job_import_runs`
2. pobiera oferty z API
3. robi `upsert` do `job_offers`
4. odświeża języki i tagi
5. oznacza stare oferty jako nieaktywne
6. kończy rekord importu statystykami

## 5. Co czyta aplikacja

Aplikacja powinna czytać tylko:

- `active_job_offers`
- albo własne zapytania po `job_offers where is_active = true`

Nie bezpośrednio z zewnętrznych API.
