using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// i got permission from ZlothY: https://git.hamburbur.org/ZlothY/ZlothYNametag/src/branch/main/EzVersionChecking
namespace FortniteEmoteWheel.Classes.EzVersionChecking;

public class ContinousVersionChecking : MonoBehaviour
{
    private const float CheckIntervalSeconds = 60f;

    private IEnumerator Start()
    {
        float lastCheckTime = 0f;

        while (!VersionCheckingInitializer.VersionOutdated)
        {
            yield return null;

            if (Time.time - lastCheckTime < CheckIntervalSeconds)
                continue;

            lastCheckTime = Time.time;

            using UnityWebRequest request = UnityWebRequest.Get(Constants.DeezUrl + "/data");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                continue;

            try
            {
                JObject data = JObject.Parse(request.downloadHandler.text);

                JToken modVersionInfo = ((JArray)data["modVersionInfo"])!
                       .FirstOrDefault(token => (string)token["modName"] == Constants.Name);

                if (modVersionInfo == null)
                    continue;

                Version latestVersion = new((string)modVersionInfo["latestVersion"]!);
                Version localVersion = new(Constants.Version);

                VersionCheckingInitializer.LatestVersion = latestVersion;
                VersionCheckingInitializer.OutdatedMessage = (string)modVersionInfo["outdatedMessage"];

                if (localVersion < latestVersion)
                {
                    VersionCheckingInitializer.VersionOutdated = true;
                    VersionCheckingInitializer.VersionOutdatedDetected?.Invoke();
                    Destroy(gameObject);

                    yield break;
                }
            }
            catch
            {
                // ignore
            }
        }
    }
}
