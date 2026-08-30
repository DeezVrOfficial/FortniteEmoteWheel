using System;

namespace FortniteEmoteWheel.Tools;

public static class Extensions
{
    public static Action<VRRig> OnPlayerCosmeticsLoaded;

    private static bool HasOwnedCosmetic(VRRig rig, string cosmeticName)
    {
        if (rig._playerOwnedCosmetics == null)
            return false;

        foreach (string ownedCosemtic in rig._playerOwnedCosmetics)
        {
            if (string.IsNullOrEmpty(ownedCosemtic))
                continue;

            if (ownedCosemtic.IndexOf(cosmeticName, StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }

    public static string IsOnSteam(this VRRig Player)
    {
        string platformProperty = (string)Player.creator.GetPlayerRef().CustomProperties["platform"];

        if (!string.IsNullOrEmpty(platformProperty))
            return platformProperty;

        if (HasOwnedCosmetic(Player, "S. FIRST LOGIN"))
            return "S. FIRST LOGIN";

        if (HasOwnedCosmetic(Player, "FIRST LOGIN") ||
            Player.creator != null && Player.creator.GetPlayerRef().CustomProperties.Count >= 3 ||
            Player.currentRankedSubTierPC > 0)
            return "FIRST LOGIN";

        return null;
    }
}
