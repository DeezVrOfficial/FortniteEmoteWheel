using GorillaLocomotion;
using UnityEngine;
using HarmonyLib;

namespace FortniteEmoteWheel.Patches;

[HarmonyPatch(typeof(GorillaQuitBox), nameof(GorillaQuitBox.OnBoxTriggered))]
public static class QuitBoxPatch
{
    private static bool Prefix()
    {
        if (GTPlayer.Instance == null)
            return true;

        ZoneManagement.SetActiveZone(GTZone.forest);
        GTPlayer.Instance.transform.position = new Vector3(-76f, 7f, -80f);

        if (GTPlayer.Instance != null)
            GTPlayer.Instance.playerRigidBody.linearVelocity = Vector3.zero;

        return false;
    }
}