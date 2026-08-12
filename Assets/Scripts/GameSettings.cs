using UnityEngine;

/// <summary>
/// Spielweit gültige Optionen, die kein Szenen-Objekt brauchen.
///
/// Liegt auf PlayerPrefs, genau wie Lautstärke und Anzeigemodus im MainMenuController —
/// dadurch überlebt die Einstellung Szenenwechsel und Neustart, und ein Häkchen im
/// Options-Menü muss später nur noch die Property setzen.
/// </summary>
public static class GameSettings
{
    private const string QueueingKey = "cozy.settings.actionQueueing";

    private static bool? cachedQueueing;

    /// <summary>
    /// Dürfen Tool-Aktionen eingereiht werden, während schon eine läuft?
    /// Aus = klassisches Verhalten, ein Cast blockiert bis er durch ist.
    /// </summary>
    public static bool ActionQueueingEnabled
    {
        get
        {
            // PlayerPrefs-Zugriffe sind nicht gratis und das hier wird pro Klick abgefragt.
            cachedQueueing ??= PlayerPrefs.GetInt(QueueingKey, 1) == 1;
            return cachedQueueing.Value;
        }
        set
        {
            cachedQueueing = value;
            PlayerPrefs.SetInt(QueueingKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Für Tests/Optionsmenü: Cache verwerfen und neu aus den Prefs lesen.</summary>
    public static void Reload() => cachedQueueing = null;
}
