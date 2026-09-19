using FortniteEmoteWheel.Classes.Admin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;
using IEnumerator = System.Collections.IEnumerator;

namespace FortniteEmoteWheel.Patches;

public class LeavesPatch : MonoBehaviour
{
    private List<string> LeaveNames = new List<string>();

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        LoadFromData();
    }

    private void LoadFromData()
    {
        try
        {
            if (HamburburData.Data != null && HamburburData.Data.TryGetValue("cleanUpForestObjectNames", out JToken token))
            {
                LeaveNames = token.ToObject<List<string>>();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[{Constants.Name}] Errored Leaves Patch: {e.Message}");
        }

        if (LeaveNames != null && LeaveNames.Count > 0)
        {
            StartCoroutine(DestroyLeaves());
        }
    }

    private IEnumerator DestroyLeaves()
    {
        while (true)
        {
            if (LeaveNames != null && LeaveNames.Count > 0)
            {
                GameObject[] allLeaves = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

                foreach (var leave in allLeaves)
                {
                    if (!leave.activeSelf)
                        continue;

                    foreach (var name in LeaveNames)
                    {
                        if (string.IsNullOrEmpty(name))
                            continue;

                        if (leave.name == name)
                        {
                            leave.SetActive(false);
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(5f);
        }
    }
}
