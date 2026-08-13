using UnityEngine;

/// <summary>
/// Spieltempo, einmalig beim Anlegen eines Spielstands gewählt.
///
/// Wirkt bewusst <b>nur auf Verkaufserlöse</b>: Samenpreise, Upgrade- und Lizenzkosten
/// bleiben in beiden Modi gleich. Dadurch dauert das Sparen auf einen Meilenstein länger,
/// ohne dass im Shop andere Zahlen stehen als im Wiki oder beim Mitspieler.
/// </summary>
public enum GamePace
{
    /// <summary>Balanciertes Tempo, Zieldurchlauf ~1 Stunde.</summary>
    Normal = 0,

    /// <summary>Entschleunigt — Erlöse ×0,6, Durchlauf eher 1,5–2 Stunden.</summary>
    Relaxed = 1
}

public static class GamePaceExtensions
{
    /// <summary>Faktor auf jeden Verkaufserlös.</summary>
    public static float SellMultiplier(this GamePace pace) => pace switch
    {
        GamePace.Relaxed => 0.6f,
        _ => 1f
    };

    public static string DisplayName(this GamePace pace) => pace switch
    {
        GamePace.Relaxed => "Entspannt",
        _ => "Normal"
    };

    public static string Description(this GamePace pace) => pace switch
    {
        GamePace.Relaxed => "Ernten bringen weniger Gold. Mehr Zeit auf der Farm,\nlängerer Weg zu den großen Freischaltungen.",
        _ => "Ausgewogenes Tempo. Etwa eine Stunde bis zur vollen Farm."
    };

    /// <summary>
    /// Wendet den Faktor auf einen Erlös an. Rundet auf, damit billige Ware nicht auf
    /// 0 Gold fällt — eine Ernte muss immer etwas einbringen.
    /// </summary>
    public static int ApplyToSale(this GamePace pace, int amount)
    {
        if (amount <= 0) return amount;
        return Mathf.Max(1, Mathf.CeilToInt(amount * pace.SellMultiplier()));
    }
}
