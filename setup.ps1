# start-all.ps1 - Uruchamia cały projekt
Write-Host "=== Discord Automation Platform - Full Startup ===" -ForegroundColor Cyan

# 1. Uruchom kontenery Docker (jeśli istnieje docker-compose)
if (Test-Path "docker-compose.yml") {
    Write-Host "Uruchamianie kontenerów Docker..." -ForegroundColor Yellow
    docker-compose up -d
    Start-Sleep -Seconds 3
}

# 2. Uruchom backend API (w nowym oknie)
Write-Host "Uruchamianie Backend API..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\backend\DiscordAutomation.API'; dotnet run" -WindowStyle Normal

# 3. Uruchom dashboard (w nowym oknie)
Write-Host "Uruchamianie Dashboard (Next.js)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\dashboard\app'; npm run dev" -WindowStyle Normal

# 4. Poczekaj chwilę na uruchomienie serwisów
Start-Sleep -Seconds 5

# 5. Uruchom bota (w bieżącym oknie)
Write-Host "`n=== URUCHAMIANIE BOTA DISCORD ===" -ForegroundColor Green
cd "bot\DiscordBot"
dotnet run

# 6. Po zatrzymaniu bota, zatrzymaj kontenery
Write-Host "`nZatrzymywanie kontenerów Docker..." -ForegroundColor Yellow
cd ..\..
docker-compose down