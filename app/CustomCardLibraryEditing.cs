using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace StsCardEditor
{
    public partial class MainForm
    {
        private void EditLibrarySelection()
        {
            List<CustomCardModel> selected=SelectedCustomCards();
            if(selected.Count!=1){SetStatus(UiText.T("请只选择一张自定义卡进行再修改；数值、描述和AI修改在同一窗口。") );return;}
            CustomCardModel original=CustomCardRevisionService.Clone(selected[0]);
            ApiSettings settings=new ApiSettings{baseUrl=txtBaseUrl.Text.Trim(),model=txtModel.Text.Trim(),apiKey=txtApiKey.Text,timeoutSeconds=(int)numTimeout.Value};
            string directory=gameDir,history=Path.Combine(AppPaths.DataDir,"card-revisions");double creativity=(double)numCreativity.Value;
            using(CustomCardEditorDialog dialog=new CustomCardEditorDialog(original,
                (card,changes)=>CustomCardRevisionService.Revise(settings,card,changes,directory,creativity),
                (card,build,report)=>{
                    if(build!=null){build.Card=card;CreativeCardService.Install(build,directory);}
                    return CustomCardRevisionService.Save(AppPaths.ProfilePath,history,original,card,report);
                },()=>CustomCardRevisionService.Previous(history,original.id))) {
                if(dialog.ShowDialog(this)!=DialogResult.OK || dialog.SaveResult==null)return;
                profile=dialog.SaveResult.Profile;RefreshCustomList();
                SetStatus(UiText.T("已保存“")+dialog.SaveResult.Card.name+UiText.T("”，原卡ID保留，旧版已备份。")+(dialog.SyncRequested?UiText.T("正在实时同步。"):UiText.T("尚未同步到游戏。")));
                if(dialog.SyncRequested)SyncRevisedCard(dialog.SaveResult.Card);
            }
        }
        private void SyncRevisedCard(CustomCardModel card)
        {
            if(definitionSyncBusy)return;definitionSyncBusy=true;tabs.Enabled=false;string directory=gameDir;
            BackgroundWorker worker=new BackgroundWorker();worker.DoWork+=delegate(object sender,DoWorkEventArgs e){using(LiveClient client=new LiveClient()){client.Connect(directory);e.Result=LiveDefinitionSync.Sync(client,new List<CustomCardModel>{card},AppPaths.ContentPath(directory),true);}};
            worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){definitionSyncBusy=false;tabs.Enabled=true;
                string message;bool failed=e.Error!=null;
                if(e.Error!=null)message=UiText.T("卡库已保存，但游戏未同步：")+e.Error.Message;
                else {DefinitionSyncResult result=(DefinitionSyncResult)e.Result;if(result.CatalogData!=null)UpdatePoolSources(DList(result.CatalogData,"cards"));failed=result.Error!=null;message=result.Error!=null?UiText.T("卡库已保存，同步未完全完成：")+result.Error:UiText.T("游戏已确认更新“")+card.name+UiText.T("”，涉及 ")+result.AffectedInstances+UiText.T(" 个已有实例和后续新牌。无需重启游戏。");}
                SetStatus(message);MessageBox.Show(this,message,failed?UiText.T("已保存，游戏同步未完成"):UiText.T("修改已生效"),MessageBoxButtons.OK,failed?MessageBoxIcon.Warning:MessageBoxIcon.Information);worker.Dispose();};worker.RunWorkerAsync();
        }
    }
}
