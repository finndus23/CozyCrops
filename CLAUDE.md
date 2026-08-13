# CozyCrops — Claude Kontext

## Projekt
Semesterbegleitendes Unity-Projekt für das Modul **Game Design** an der FH Aachen.
Gruppenarbeit, agile Entwicklung mit Arbeitspaketen + Peer Review.

## Spielkonzept
Entspanntes **Cozy Farming Game** in Unity 6 (URP).
- Spieler baut eine eigene Farm auf, kümmert sich um Pflanzen, kauft/verkauft auf dem Marktplatz
- **Kernkontrast:** Farm (cozy, entspannt) ↔ Marktplatz (voll, laut, unübersichtlich)
- **Perspektive:** Isometrisch / Gitterperspektive
- **Grafik:** Low Poly
- **Story:** Stadtleben zu stressig → Spieler findet eine Farm zu verwalten

## Tech Stack
- **Engine:** Unity 6 (6000.3.13f1)
- **Render Pipeline:** URP (Universal Render Pipeline)
- **Sprache:** C#
- **Versionskontrolle:** Git + GitHub

## Mechaniken
- Pflanzen anbauen, pflegen, düngen
- Neue Felder freischalten
- Felder Tile-Based organisieren (selbst platzieren)
- Marktplatz: kaufen/verkaufen

## Projektstruktur
```
Assets/
├── Scenes/        ← Unity Szenen
├── Scripts/       ← C# Skripte
├── Prefabs/       ← Prefabs
├── Materials/     ← Materialien
├── Models/        ← 3D Modelle
├── Textures/      ← Texturen
├── Audio/         ← Sounds & Musik
└── Settings/      ← URP Render Pipeline Settings
```

## Spielvergnügen & Motivation
- Fertigstellung, Antizipation, Autonomie, Auswahlmöglichkeiten, Entspannung
- Satisfying Sounds, Progress-Gefühl, kreativer Tile-Based Aufbau

## Arbeitsweise — Editor vs. Code

Faustregel, keine harte Regel: **kurz abwägen, bevor du Szenen-/Prefab-Dateien anfasst.**

Wenn eine Aufgabe in ein paar Minuten per Drag & Drop im Unity-Editor erledigt ist, beschreib
mir stattdessen die Schritte, statt sie über Datei-Bearbeitung zu lösen. Typische Fälle:

- Skript auf ein GameObject ziehen / Component hinzufügen
- Referenz im Inspector setzen (AudioClip, Prefab-Feld, …)
- Einfache Transform-/Value-Anpassungen
- Audio Mixer, Animator Controller, Lighting, Tilemap

Alles was komplexer ist, mehrere verknüpfte Objekte betrifft, oder wo ich explizit "mach das"
sage → ganz normal selbst lösen, auch wenn dafür eine Szene/ein Prefab editiert werden muss.
Im Zweifel kurz nachfragen.

## Datei-Zugriff

- `.unity`/`.prefab` nur lesen, wenn es für die Aufgabe wirklich nötig ist (z.B. eine Referenz
  suchen, die ich nicht aus dem Kopf weiß) — nicht prophylaktisch.
- **Nie durchsuchen:** `Library/`, `Temp/`, `obj/`, `Logs/`, `.vs/`
- Bei größeren Aufgaben lieber fragen ("welches GameObject hat X?") als eine ganze Szene zu parsen.

Ziel: keine Tokens für Sachen verbrennen, die per Hand im Editor schneller gehen.

## Antwortformat

Wenn beides zutrifft, am Ende kurz trennen:
1. **Im Code erledigt** — was fertig ist
2. **Im Editor nachziehen** — kurze nummerierte Liste, was ich noch selbst machen muss

## Vault-Notizen
Weitere Projektdetails: `C:\Users\Finn Henning\OneDrive\hirn\01.projects\GD Spiel Unity.md`
