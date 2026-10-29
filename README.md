# Poopsweeper

Minesweeper auf der Hundewiese: Räume alle Haufen, ohne reinzutreten. Godot 4.7 (.NET / C#).

## Aufbau

| Ordner | Inhalt |
|---|---|
| `src/Poopsweeper.Core` | Spiellogik in reinem C# (Board, Level-Generator, Level-Kurve, GameSession, Speicherstand) – keine Godot-Abhängigkeit |
| `tests/Poopsweeper.Core.Tests` | xUnit-Tests für die Logik (`dotnet test`) |
| `game/` | Godot-Projekt (Zeichnen, Eingabe, Menüs). Alles Visuelle ist Vektorgrafik per `_Draw()` |

## Starten

- Godot **4.7.2 .NET** (Mono-Version) installieren: `winget install GodotEngine.GodotEngine.Mono`
- `game/project.godot` im Godot-Editor öffnen und F5 drücken.
- Wichtig: den Editor aus dem Paketordner starten (`...\WinGet\Packages\GodotEngine.GodotEngine.Mono_...\`), nicht über den winget-Alias – sonst findet Godot seine .NET-Dateien nicht.
- Dev-Hilfen: `godot --path game -- --level=12` startet direkt Level 12, `-- --level=12 --demo` spielt ein paar Züge vor, `-- --quick`, `-- --compendium`, `-- --campaign`, `-- --settings`, `-- --tutorial` öffnen den jeweiligen Bildschirm, `-- --lang=en` erzwingt eine Sprache.

## Menü

- **Kampagne:** Level 1, 2, 3 … mit steigender Schwierigkeit, Sterne und Freischaltung.
- **Schnelles Spiel:** Einfach, Mittel, Schwer oder Individuell (Größe, Dichte, Hindernisse, Waldrand, Hund, Pfützen, Doppelhaufen, Sprinkler, Leben). Zählt nicht für die Kampagne.
- **Kompendium:** Erklärt Steuerung und alle Mechaniken.
- **Einstellungen:** Sprache (Deutsch/Englisch/automatisch), Effekt-Lautstärke, Vibration, Farbenblind-Modus, Oberflächengröße, Tutorial wiederholen, Fortschritt zurücksetzen.
- **Tutorial:** Beim ersten Start der Kampagne führt ein kurzes, interaktives Tutorial durch das klassische 1-2-1-Muster.

## Fairness

Die ersten 12 Kampagnenlevel (und im Schnellen Spiel der Schalter „Ohne Raten lösbar“) sind garantiert per Logik lösbar. Dafür gibt es einen Solver im Core
(`Solver.cs`), der auch Pfützen, Doppelhaufen und Sprinkler versteht. Der **Tipp** des Hundes nutzt denselben Solver und zeigt ein garantiert sicheres Feld.

## Steuerung

| | Maus | Touch |
|---|---|---|
| Graben | Linksklick | Tippen |
| Tüte | Rechtsklick | Lang drücken oder Modus „Tüte“ |
| Hund | Modus „Hund“ + Klick | Modus „Hund“ + Tippen |
| Zahl räumen (Chord) | Klick auf aufgedeckte Zahl | Tippen auf Zahl |
| Zoom / Verschieben | Mausrad / Ziehen oder Mitteltaste | Pinch / Wischen |

Tasten: `R` neu, `Esc` zurück, `1`/`2`/`3` Werkzeug, `H` Tipp.

## Level & Mechaniken

Level 1–3 nur größer/dichter, ab 4 Hindernisse (Bäume 2x2, Bänke, Büsche, Steine), ab 6 Schnüffelhund, ab 8 Pfützen (Zahl als Spanne),
ab 12 Doppelhaufen, ab 15 Sprinkler (Zahl „?“ bis der Hund schnüffelt). Jedes 5. Level ist ein Bosslevel. Der Waldrand wird mit höheren Leveln unruhiger.
Sterne: ★ geschafft, ★★ in Par-Zeit, ★★★ zusätzlich ohne Leben zu verlieren. Fortschritt liegt in `user://save.json`.

## Export (noch nicht eingerichtet)

- **Windows:** Editor → Projekt → Exportieren, dazu die Export-Templates (Editor → Verwalten) installieren.
- **Android:** Android SDK + JDK 17, Godot-Editoreinstellungen → Export → Android, Android-Build-Template installieren. Das Projekt ist schon darauf vorbereitet
  (GL-Compatibility-Renderer, Touch-Eingabe, `net9.0` für Android in der `.csproj`). Godot-C#-Export für Android ist noch jünger als GDScript, bitte früh testen.

## Lizenz

MIT, siehe [LICENSE](LICENSE).
