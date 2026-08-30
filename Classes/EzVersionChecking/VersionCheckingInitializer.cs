using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using FortniteEmoteWheel.Classes.Admin;

// i got permission from ZlothY: https://git.hamburbur.org/ZlothY/ZlothYNametag/src/branch/main/EzVersionChecking
namespace FortniteEmoteWheel.Classes.EzVersionChecking;

public static class VersionCheckingInitializer
{
    public static Action VersionOutdatedDetected;

    public static bool VersionOutdated;

    public static string OutdatedMessage;

    public static Version LatestVersion;

    public static void StartVersionChecking()
    {
        JObject data = HamburburData.Data;

        JToken modVersionInfo =
                ((JArray)data["modVersionInfo"])!.FirstOrDefault(token => (string)token["modName"] ==
                                                                            Constants.Name);

        if (modVersionInfo != null)
        {
            LatestVersion = new Version(((string)modVersionInfo["latestVersion"])!);

            OutdatedMessage = (string)modVersionInfo["outdatedMessage"];

            Version localVersion = new(Constants.Version);

            if (localVersion < LatestVersion)
                VersionOutdated = true;
        }

        if (VersionOutdated)
            return;

        new GameObject($"{Constants.Name} Version Checking").AddComponent<ContinousVersionChecking>();
    }
}
