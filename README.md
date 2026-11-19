# TreeScan

TreeScan is een Nederlandstalige Windows-app in C# die laat zien welke mappen en bestanden de meeste ruimte innemen.

**Supervised AI project.** TreeScan is gemaakt met Codex, onder menselijke begeleiding.

- Bekijk een uitklapbare boom, met de grootste items bovenaan.
- Zie per rij de grootte, het aantal bestanden en het aandeel in de totale scan.
- Open de bijbehorende locatie vanuit TreeScan in Windows Verkenner.

## Installeren en gebruiken

1. Download de Windows x64-installer (`.msi`) bij de releases van dit project en voer die uit. TreeScan wordt geïnstalleerd voor de huidige Windows-gebruiker.
2. Rechtsklik in Verkenner op een schijf of map en kies **Scannen met TreeScan**. Op Windows 11 staat de optie in het klassieke menu onder **Meer opties weergeven**.
3. Je kunt TreeScan ook openen via Start en daar een schijf kiezen.

TreeScan vereist Windows x64 met .NET Framework 4.8 of nieuwer. Een `*` in de boom geeft aan dat onderdelen zijn overgeslagen, bijvoorbeeld door ontbrekende leesrechten of doordat het om een bestandssysteemkoppeling gaat. De getoonde totalen kunnen daardoor lager zijn.

Verwijder TreeScan via **Instellingen → Apps → Geïnstalleerde apps**. Windows Installer ruimt ook de Verkenner-menuopties op.

## Zelf bouwen

Gebruik PowerShell op Windows met de .NET SDK 9 en het .NET Framework 4.8 Developer Pack.

```powershell
dotnet build -c Release
./installer/Build-Msi.ps1
```

De executable staat in `bin/Release/net48/`. Het installatiescript gebruikt WiX 6.0.2 en schrijft de MSI naar `bin/installer/`.
