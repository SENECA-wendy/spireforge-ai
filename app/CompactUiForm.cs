using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace StsCardEditor
{
    public partial class MainForm
    {
        private TabControl customSections;
        private TabPage customLibraryPage;
        private void BuildCompactNavigation()
        {
            BuildCardsTab();BuildDeckManagerTab();BuildCardPoolTab();
            TabPage custom=NewTab(UiText.T("自定义卡牌"));customSections=new TabControl{Dock=DockStyle.Fill};custom.Controls.Add(customSections);
            BuildCustomTab();customLibraryPage=tabs.TabPages[tabs.TabPages.Count-1];MoveLastPage(customSections,UiText.T("卡库 · 多选管理"));
            BuildAiTab();MoveLastPage(customSections,UiText.T("AI 创作"));
            BuildLiveTab();advancedLivePage=tabs.TabPages[tabs.TabPages.Count-1];MoveLastPage(cardEditorSections,UiText.T("单个实例 / 自定义效果（高级）"));
            TabPage settings=NewTab(UiText.T("设置与维护"));TabControl sections=new TabControl{Dock=DockStyle.Fill};settings.Controls.Add(sections);
            BuildGameTab();MoveLastPage(sections,UiText.T("游戏连接"));BuildApplyTab();btnApply.Text=UiText.T("安装 / 更新适配层");MoveLastPage(sections,UiText.T("适配层安装与恢复"));
            BuildSaveRecoveryTab();MoveLastPage(sections,UiText.T("存档备份恢复"));BuildLogsTab();MoveLastPage(sections,UiText.T("诊断日志"));tabs.SelectedTab=cardEditorPage;
        }
        private void MoveLastPage(TabControl destination,string title){TabPage page=tabs.TabPages[tabs.TabPages.Count-1];tabs.TabPages.Remove(page);page.Text=title;destination.TabPages.Add(page);}
        private void BuildModernCustomLibrary()
        {
            TabPage page=NewTab(UiText.T("自定义卡库"));TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(8)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,72));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));page.Controls.Add(root);
            FlowLayoutPanel header=DeckFlow();header.Controls.Add(DeckButton(UiText.T("编辑选中卡牌"),delegate{EditLibrarySelection();}));header.Controls.Add(DeckButton(UiText.T("同步定义到当前游戏"),delegate{SyncDefinitionsToGame();}));header.Controls.Add(DeckButton(UiText.T("启用/停用选中"),delegate{ToggleCustomSelection();}));header.Controls.Add(DeckButton(UiText.T("删除选中卡牌"),delegate{DeleteCustomSelection();}));header.Controls.Add(DeckButton(UiText.T("导出选中"),delegate{ExportCustomSelection();}));header.Controls.Add(DeckButton(UiText.T("导入 JSON"),delegate{ImportCustom();}));root.Controls.Add(header,0,0);
            TableLayoutPanel columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));root.Controls.Add(columns,0,1);
            lstCustom=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true};MultiSelect(lstCustom);lstCustom.SelectedIndexChanged+=delegate{ShowCustomCardPreview();};lstCustom.DoubleClick+=delegate{EditLibrarySelection();};columns.Controls.Add(lstCustom,0,0);
            GroupBox preview=new GroupBox{Dock=DockStyle.Fill,Text=UiText.T("所选卡牌介绍与升级效果"),Padding=new Padding(10)};txtCustomCardPreview=CustomCardPreview.CreateTextBox(UiText.T("自定义卡描述"));preview.Controls.Add(txtCustomCardPreview);columns.Controls.Add(preview,1,0);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text=UiText.T("双击单张卡可手动/AI再修改。Ctrl / Shift 多选用于启停、删除和导出。保存后可实时同步到已有卡牌。"),Padding=new Padding(4,10,4,0)},0,2);
        }
        private List<CustomCardModel> SelectedCustomCards(){return lstCustom.SelectedItems.Cast<CustomListItem>().Select(i=>i.Card).ToList();}
        private void ToggleCustomSelection()
        {
            var cards=SelectedCustomCards();if(cards.Count==0)return;bool enable=cards.Any(c=>!c.enabled);
            if(!enable && cards.Any(c=>IsCustomCardInAnySave(c.id))){MessageBox.Show(UiText.T("选中卡牌仍被存档使用，请先在本局牌组中处理这些卡。"),UiText.T("不能停用"),MessageBoxButtons.OK,MessageBoxIcon.Warning);return;}
            foreach(var card in cards)card.enabled=enable;ProfileStore.SaveProfile(profile);RefreshCustomList();SetStatus((enable?UiText.T("已启用 "):UiText.T("已停用 "))+cards.Count+UiText.T(" 张自定义卡。"));
        }
        private void DeleteCustomSelection()
        {
            var cards=SelectedCustomCards();if(cards.Count==0)return;
            if(MessageBox.Show(UiText.T("从卡库删除选中的 ")+cards.Count+UiText.T(" 张卡？已有本局牌组和存档需要的游戏定义将继续保留。"),UiText.T("删除自定义卡"),MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
            try{Profile changed=JsonUtil.FromJson<Profile>(JsonUtil.ToJson(profile));foreach(var card in cards)ProfileStore.MarkCustomCardDeleted(changed,card.id);
                if(File.Exists(AppPaths.ProfilePath))File.Copy(AppPaths.ProfilePath,Path.Combine(AppPaths.BackupDir,"profile-before-delete-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+".json"));
                ProfileStore.SaveProfile(changed);profile=changed;RefreshCustomList();SetStatus(UiText.T("已删除 ")+cards.Count+UiText.T(" 张卡，并同步候选列表。"));
            }catch(Exception error){MessageBox.Show(error.Message,UiText.T("删除失败"),MessageBoxButtons.OK,MessageBoxIcon.Error);}
        }
        private void ExportCustomSelection()
        {
            var cards=SelectedCustomCards();if(cards.Count==0)return;if(cards.Count==1){ExportCustom();return;}
            using(FolderBrowserDialog dialog=new FolderBrowserDialog{Description=UiText.T("选择保存多张卡牌 JSON 的文件夹")}){
                if(dialog.ShowDialog()!=DialogResult.OK)return;try{foreach(var card in cards){string name=card.id;foreach(char bad in Path.GetInvalidFileNameChars())name=name.Replace(bad,'_');string path=Path.Combine(dialog.SelectedPath,name+".json");
                    using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))using(var writer=new StreamWriter(stream,new System.Text.UTF8Encoding(false)))writer.Write(JsonUtil.ToJson(card));}SetStatus(UiText.T("已导出 ")+cards.Count+UiText.T(" 张卡。"));
                }catch(Exception error){MessageBox.Show(error.Message,UiText.T("导出未全部完成"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}
            }
        }
        private void BuildSaveRecoveryTab()
        {
            TabPage page=NewTab(UiText.T("备份恢复"));TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(12)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));page.Controls.Add(root);
            root.Controls.Add(new Label{Dock=DockStyle.Fill,Text=UiText.T("保留存档备份恢复用于故障处理。正常加入、移除和升级卡牌都在本局实时牌组中完成。")},0,0);
            lstSaveBackups=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true};root.Controls.Add(lstSaveBackups,0,1);
            FlowLayoutPanel actions=DeckFlow();actions.Controls.Add(DeckButton(UiText.T("刷新备份"),delegate{RefreshSaveBackups();}));actions.Controls.Add(DeckButton(UiText.T("恢复选中备份"),delegate{RestoreSave();}));root.Controls.Add(actions,0,2);
        }
        private string CardIntroduction(string id,string name)
        {
            CustomCardModel custom=profile.customCards.Find(c=>c.id==id);if(custom!=null)return CustomCardPreview.Build(custom);
            CatalogCard card=catalog==null?null:catalog.cards.Find(c=>c.id==id);return card==null?name+" ["+id+UiText.T("]\r\n未找到该卡的介绍；已提供四职业原生卡与自定义卡详情。"):NativeCardPreview.Build(card);
        }
    }
}
