using System;
using BepInEx.Bootstrap;

namespace Bola;

public static class NoRangedBridge
{
    public static bool AllowsNewAction(out string reason)
    {
        reason="";
        if(!Chainloader.PluginInfos.TryGetValue("norskit_noranged_plugin",out var plugin)) return true;
        var type=plugin.Instance.GetType();
        var protocol=type.GetField("BolaCompatibilityProtocol");
        var allow=type.GetProperty("AllowBola");
        if(protocol==null || !Equals(protocol.GetRawConstantValue(),1) || allow==null)
        { reason="Installed No Ranged lacks Bola compatibility protocol 1. Use the matching No Ranged test build."; return false; }
        try
        {
            if(allow.GetValue(null,null) is bool enabled && enabled) return true;
            reason="Bola is disabled by the server's No Ranged compatibility setting."; return false;
        }
        catch(Exception) { reason="Unable to read No Ranged's Bola policy; new actions are disabled."; return false; }
    }
}
