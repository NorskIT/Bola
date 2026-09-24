using System.Collections;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bola;

// Added after other HUD patches so their name/star layout remains intact.
[HarmonyPatch(typeof(EnemyHud),"UpdateHuds")]
internal static class BindingHudPatch
{
    private static readonly System.Reflection.FieldInfo Huds=AccessTools.Field(typeof(EnemyHud),"m_huds");
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(EnemyHud __instance)
    {
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        foreach(DictionaryEntry entry in (IDictionary)Huds.GetValue(__instance))
        {
            var character=entry.Key as Character;
            if(!character) continue;
            var data=entry.Value;
            var gui=(GameObject)AccessTools.Field(data.GetType(),"m_gui").GetValue(data);
            if(!gui) continue;
            var display=gui.GetComponent<BindingHud>();
            var view=character!.GetComponent<ZNetView>();
            string seconds="";
            bool visible=!character.IsDead() && view && view.IsValid() && BindingPresentation.Read(view.GetZDO(),out seconds);
            if(!visible) { if(display) display.Hide(); continue; }
            if(!display)
            {
                display=gui.AddComponent<BindingHud>();
                display.Initialize((TextMeshProUGUI)AccessTools.Field(data.GetType(),"m_name").GetValue(data));
            }
            display.Show(seconds);
        }
    }
}

internal sealed class BindingHud : MonoBehaviour
{
    private RectTransform row=null!;
    private TextMeshProUGUI label=null!;
    private readonly Vector3[] corners=new Vector3[4];
    public void Initialize(TextMeshProUGUI name)
    {
        row=new GameObject("BolaBinding",typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(transform,false); row.sizeDelta=new Vector2(88,36);
        row.anchorMin=row.anchorMax=new Vector2(.5f,.5f);
        var icon=new GameObject("Icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(row,false); icon.rectTransform.sizeDelta=new Vector2(32,32);
        icon.rectTransform.anchoredPosition=new Vector2(-22,0); icon.raycastTarget=false;
        icon.sprite=ObjectDB.instance.GetItemPrefab(Plugin.ItemName).GetComponent<ItemDrop>().m_itemData.m_shared.m_icons[0];
        icon.preserveAspect=true;
        label=Instantiate(name,row); label.name="Seconds"; label.text="";
        label.rectTransform.anchorMin=label.rectTransform.anchorMax=new Vector2(.5f,.5f);
        label.rectTransform.pivot=new Vector2(.5f,.5f);
        label.rectTransform.sizeDelta=new Vector2(48,36); label.rectTransform.anchoredPosition=new Vector2(20,0);
        label.enableAutoSizing=false; label.fontSize=24; label.color=Color.white;
        label.fontStyle=FontStyles.Bold; label.alignment=TextAlignmentOptions.MidlineLeft;
        label.outlineColor=Color.black; label.outlineWidth=.25f; label.raycastTarget=false;
    }
    public void Hide() { if(row) row.gameObject.SetActive(false); }
    public void Show(string seconds)
    {
        if(!row) return;
        float top=0;
        foreach(var rect in GetComponentsInChildren<RectTransform>(false))
        {
            if(rect==transform || rect==row || rect.IsChildOf(row)) continue;
            rect.GetWorldCorners(corners);
            foreach(var corner in corners) top=Mathf.Max(top,transform.InverseTransformPoint(corner).y);
        }
        row.localPosition=new Vector3(0,top+24,0);
        label.text=seconds; row.gameObject.SetActive(true);
    }
}
