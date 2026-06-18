# Wariant: hostowanie u siebie dla kilku osób

## Najprostsza droga

1. Zachować obecny interfejs Blazor.
2. Przygotować osobny host webowy `ASP.NET Core`, który będzie używał tej samej logiki aplikacji.
3. Wystawić go z domu przez:
   - `Tailscale` jeśli dostęp ma mieć kilka zaufanych osób
   - `Cloudflare Tunnel` jeśli chcesz wygodny link bez przekierowania portów

## Co już jest przygotowane

- Logika użytkowników działa przez `IUserStore`.
- Sesja aplikacji działa przez `IAppSessionStore`.
- Otwieranie linków działa przez `IUrlLauncher`.
- Ścieżka danych i odczyt plików pakietu działają przez:
  - `IAppDataPathProvider`
  - `IAppPackageFileProvider`
- Dzięki temu później można dodać implementacje webowe bez przepisywania całego UI.

## Co będzie kolejnym krokiem

1. Dodać projekt webowy `ASP.NET Core`.
2. Dodać webowe implementacje:
   - sesji
   - magazynu użytkowników
   - otwierania linków
3. Przenieść logowanie i konta do wspólnej bazy danych.

## Rekomendacja praktyczna

- Dla kilku osób testowo: `Tailscale`
- Dla linku dostępnego z przeglądarki: `Cloudflare Tunnel`
- Na tym etapie nie wystawiaj jeszcze lokalnego JSON-a z użytkownikami bezpośrednio do internetu.
