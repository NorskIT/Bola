using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
namespace Bola;
internal static class Text
{
    internal static void Register()
    {
        var localization=LocalizationManager.Instance.GetLocalization();
        localization.AddTranslation("English",new Dictionary<string,string> {
            ["bola_name"]="Bola",["bola_description"]="Hold attack to charge, release to throw. Block cancels. Recover the bola after use.",
            ["bola_bound"]="Entangled",["bola_immune"]="Bola immunity",
            ["bola_disabled"]="Bola is disabled by the server.",["bola_range"]="Target beyond ballistic range"
        });
        localization.AddTranslation("Norwegian",new Dictionary<string,string> {
            ["bola_name"]="Bola",["bola_description"]="Hold angrep for å lade, slipp for å kaste. Blokkering avbryter. Plukk opp bolaen etter bruk.",
            ["bola_bound"]="Fastbundet",["bola_immune"]="Immun mot bola",
            ["bola_disabled"]="Bola er deaktivert av serveren.",["bola_range"]="Målet er utenfor kastets rekkevidde"
        });
    }
}
