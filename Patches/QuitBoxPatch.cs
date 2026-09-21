using HarmonyLib;

namespace FortniteEmoteWheel.Patches;

[HarmonyPatch(typeof(GorillaQuitBox), nameof(GorillaQuitBox.OnBoxTriggered))]
public static class QuitBoxPatch
{
    private static bool Prefix()
    {
        if (GorillaLocomotion.GTPlayer.Instance == null)
            return true;

        ZoneManagement.SetActiveZone(GTZone.forest);
        GorillaLocomotion.GTPlayer.Instance.transform.position = new UnityEngine.Vector3(-76f, 7f, -80f);

        if (GorillaLocomotion.GTPlayer.Instance != null)
            GorillaLocomotion.GTPlayer.Instance.playerRigidBody.linearVelocity = UnityEngine.Vector3.zero;

        return false;
    }
}