# Uruchomienie wersji webowej

## Start lokalny

W katalogu projektu webowego uruchom:

```powershell
 dotnet run --project C:\Users\micha\source\repos\Michał_Błaż_131406_3\MauiApp1\MauiApp1.Web\MauiApp1.Web.csproj --urls http://0.0.0.0:5055
```

Potem otwórz w przeglądarce:

```text
http://localhost:5055
```

## Udostępnienie kilku osobom

### Opcja 1: Cloudflare Tunnel

Jeśli masz zainstalowany `cloudflared`:

```powershell
cloudflared tunnel --url http://localhost:5055
```

Po chwili dostaniesz publiczny link HTTPS, który możesz wysłać kilku osobom.

### Opcja 2: Tailscale

Jeśli wszyscy mają konto i aplikację `Tailscale`:

1. uruchom stronę lokalnie,
2. udostępnij port przez Tailscale lub użyj adresu urządzenia w sieci Tailscale,
3. daj dostęp tylko wybranym osobom.
