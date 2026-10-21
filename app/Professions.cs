using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
namespace StsCardEditor
{
    public sealed class ProfessionChoice
    {
        public string Color,Name;public ProfessionChoice(string color,string name){Color=color;Name=name;}public override string ToString(){return UiText.T(Name);}
    }
    public static class Professions
    {
        public static readonly string[] NativeColors={"RED","GREEN","BLUE","PURPLE"};
        public static readonly string[] CustomColors={"RED","GREEN","BLUE","PURPLE","COLORLESS"};
        public static string Name(string color){switch(color){case "RED":return UiText.T("铁甲战士");case "GREEN":return UiText.T("静默猎手");case "BLUE":return UiText.T("故障机器人");case "PURPLE":return UiText.T("观者");case "COLORLESS":return UiText.T("无色");default:return color??UiText.T("未知");}}
        public static string ColorForClass(string character){switch(character){case "IRONCLAD":return "RED";case "THE_SILENT":return "GREEN";case "DEFECT":return "BLUE";case "WATCHER":return "PURPLE";default:return null;}}
        public static string Strike(string color){switch(color){case "GREEN":return "Strike_G";case "BLUE":return "Strike_B";case "PURPLE":return "Strike_P";default:return "Strike_R";}}
        public static ComboBox Filter(bool custom)
        {
            ComboBox box=new ComboBox{Width=155,DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=UiText.T("职业筛选")};box.Items.Add(new ProfessionChoice("AUTO",UiText.T("当前职业（自动）")));box.Items.Add(new ProfessionChoice("ALL",UiText.T("全部职业")));foreach(string color in NativeColors)box.Items.Add(new ProfessionChoice(color,Name(color)));if(custom)box.Items.Add(new ProfessionChoice("CUSTOM",UiText.T("仅自定义卡")));box.SelectedIndex=0;return box;
        }
        public static string Choice(ComboBox box){ProfessionChoice choice=box==null?null:box.SelectedItem as ProfessionChoice;return choice==null?"AUTO":choice.Color;}
        public static string Resolve(ComboBox box,string detected){string selected=Choice(box);return selected=="AUTO"?detected:selected;}
        public static bool Matches(ComboBox box,string color,bool custom,string detected)
        {
            string choice=Resolve(box,detected);return choice=="CUSTOM"?custom:choice=="ALL"?true:color==choice || (custom && color=="COLORLESS");
        }
        public static void FormatColors(ComboBox box){box.FormattingEnabled=true;box.Format+=delegate(object sender,ListControlConvertEventArgs e){e.Value=Name(Convert.ToString(e.ListItem));};}
    }
    public partial class MainForm
    {
        private ComboBox cmbNativeProfession,cmbDeckProfession,cmbPoolProfession,cmbAiProfession;
        private string detectedProfession="RED";
        private bool aiProfessionChosen;
        private string SourceProfession(string id){CustomCardModel custom=profile.customCards.FirstOrDefault(c=>c.id==id);if(custom!=null)return custom.color;CatalogCard card=catalog==null?null:catalog.cards.FirstOrDefault(c=>c.id==id);return card==null?null:card.color;}
        private void DetectProfession(Dictionary<string,object> data)
        {
            if(data==null)return;object raw;string color=data.TryGetValue("cardColor",out raw)?Convert.ToString(raw):data.TryGetValue("playerClass",out raw)?Professions.ColorForClass(Convert.ToString(raw)):null;
            if(!Professions.NativeColors.Contains(color)||color==detectedProfession)return;detectedProfession=color;
            RefreshNativeList();FilterDeckSources();RenderPoolDraft();if(cmbAiProfession!=null&&!aiProfessionChosen)cmbAiProfession.SelectedItem=color;
        }
        private string SelectedCreationColor(){return cmbAiProfession==null||cmbAiProfession.SelectedItem==null?detectedProfession:Convert.ToString(cmbAiProfession.SelectedItem);}
    }
}
