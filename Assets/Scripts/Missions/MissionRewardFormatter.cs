/// <summary>
/// Lesbarer deutscher Text für eine Missions-Belohnung — Gegenstück zum
/// MissionObjectiveFormatter, damit die Abhol-Karte ohne manuell gepflegte Texte auskommt.
/// </summary>
public static class MissionRewardFormatter
{
    public static string Format(MissionReward reward)
    {
        if (reward == null) return null;

        return reward.type switch
        {
            MissionReward.RewardType.Money =>
                $"+{reward.amount} Gold",

            MissionReward.RewardType.Seed =>
                reward.plant != null
                    ? $"+{reward.amount}x {reward.plant.plantName} Samen"
                    : $"+{reward.amount} Samen",

            MissionReward.RewardType.Tool =>
                ToolName(reward.tool) is string t ? $"Neues Werkzeug: {t}" : "Neues Werkzeug",

            MissionReward.RewardType.UnlockZone =>
                "Neue Fläche freigeschaltet",

            MissionReward.RewardType.License =>
                LicenseName(reward.licenseId) is string l ? $"Lizenz: {l}" : "Neue Lizenz",

            _ => null
        };
    }

    /// <summary>
    /// Wie viele Münzen fliegen sollen — Optik, nicht der Betrag.
    /// Skaliert flach mit dem Gold: 30 G sollen sich nicht anfühlen wie 300 G,
    /// aber 300 G auch nicht den Bildschirm zuschütten.
    /// </summary>
    public static int CoinCountFor(MissionData data, CoinFlightSettings settings = null)
    {
        settings ??= new CoinFlightSettings();

        int min = UnityEngine.Mathf.Max(1, settings.minCoins);
        int max = UnityEngine.Mathf.Max(min, settings.maxCoins);

        if (data?.rewards == null) return min;

        int gold = 0;
        foreach (var reward in data.rewards)
            if (reward != null && reward.type == MissionReward.RewardType.Money)
                gold += reward.amount;

        if (gold <= 0) return min;

        int perCoin = UnityEngine.Mathf.Max(1, settings.goldPerExtraCoin);
        return UnityEngine.Mathf.Clamp(min + gold / perCoin, min, max);
    }

    /// <summary>Anzeigename der Lizenz, oder null wenn sie nicht auffindbar ist.</summary>
    private static string LicenseName(string licenseId)
    {
        if (string.IsNullOrWhiteSpace(licenseId)) return null;

        var license = LicenseRegistry.Instance != null
            ? LicenseRegistry.Instance.Find(licenseId)
            : null;

        if (license == null) return null;
        return string.IsNullOrWhiteSpace(license.displayName) ? license.licenseId : license.displayName;
    }

    private static string ToolName(ToolType tool) => tool switch
    {
        ToolType.Hoe => "Hacke",
        ToolType.WateringCan => "Gießkanne",
        ToolType.Seed => "Seeder",
        ToolType.Scythe => "Sichel",
        ToolType.Fertilize => "Dünger",
        _ => null
    };
}
