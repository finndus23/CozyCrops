/// <summary>
/// Wie ein Quest-Ziel hervorgehoben wird. Trennt das WAS (welches Objekt ist gerade
/// wichtig — entscheidet <see cref="HighlightTarget"/> / <see cref="MissionHighlightDirector"/>)
/// vom WIE (Weltobjekt-Kontur, UI-Rahmen, …).
///
/// Nötig, weil Welt und UI technisch nichts gemeinsam haben: Die Screen-Space-Kontur
/// entsteht aus einer Maske, die eine Kamera aus Weltgeometrie rendert. Ein
/// Canvas-Overlay taucht in dieser Maske gar nicht auf — Hotbar-Slots brauchen deshalb
/// zwingend einen eigenen Weg, obwohl sie über dieselbe Missions-Logik ausgewählt werden.
/// </summary>
public interface IHighlightVisual
{
    void SetHighlighted(bool on);
}
