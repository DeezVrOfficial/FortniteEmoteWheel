using System;

namespace FortniteEmoteWheel.Tools;

public static class Extensions
{
    public static Action<VRRig> OnPlayerCosmeticsLoaded;

    public static string CleanString(string input, int maxLength, char[] ignoredChars = null)
    {
        input = new string(Array.FindAll(input.ToCharArray(), character =>
                                                                      Utils.IsASCIILetterOrDigit(character) ||
                                                                      ignoredChars != null &&
                                                                      Array.IndexOf(ignoredChars, character) != -1));

        if (input.Length > maxLength)
            input = input[..maxLength];

        return input.ToUpper();
    }

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