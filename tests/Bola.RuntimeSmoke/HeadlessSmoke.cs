using System;
using System.Collections;
using System.IO;
using BepInEx;
using UnityEngine;
namespace Bola.RuntimeSmoke;

[BepInPlugin("norskit_bola_headless_smoke","Bola headless smoke","0.1.0")]
[BepInDependency(Plugin.Id)]
public sealed class HeadlessSmoke : BaseUnityPlugin
{
    private IEnumerator Start()
    {
        string output=Environment.GetEnvironmentVariable("BOLA_HEADLESS_OUTPUT")??"";
        if(output=="") yield break;
        float until=Time.realtimeSinceStartup+120;
        while((!ZNetScene.instance || !ZNetScene.instance.GetPrefab(Plugin.ItemName)) && Time.realtimeSinceStartup<until) yield return new WaitForSecondsRealtime(1);
        bool success=ZNetScene.instance && ZNetScene.instance.GetPrefab(Plugin.ItemName) && !Plugin.Instance.Visual && !Plugin.Instance.ThrowClip;
        File.WriteAllText(Path.Combine(output,success?"verified.txt":"failure.txt"),success
            ? "Dedicated Windows server registered the logical Bola item without a cosmetic bundle or graphics. No gameplay/RPC validation claim."
            : "Logical prefab was unavailable or a cosmetic asset unexpectedly loaded.");
        Application.Quit();
    }
}
