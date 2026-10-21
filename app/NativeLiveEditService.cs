using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
namespace StsCardEditor
{
    public class NativeEditPlan { public string Id; public CardOverrideModel Values; }
    public class NativeEditResult { public readonly List<NativeEditPlan> Applied=new List<NativeEditPlan>(); public string Error; public int Instances; }
    public static class NativeLiveEditService
    {
        public static List<NativeEditPlan> Plan(IEnumerable<CatalogCard> selected, CardOverrideModel input, Dictionary<string,CardOverrideModel> previous, bool reset)
        {
            List<NativeEditPlan> result=new List<NativeEditPlan>();
            foreach(CatalogCard card in selected) {
                if(!Professions.NativeColors.Contains(card.color)) throw new InvalidOperationException("请选择四职业的原生卡牌。");
                CardOverrideModel old; previous.TryGetValue(card.id,out old);
                CardOverrideModel merged=old==null?new CardOverrideModel():JsonUtil.FromJson<CardOverrideModel>(JsonUtil.ToJson(old));
                if(reset) {
                    merged.cost=card.cost; merged.upgradeCost=card.upgradeCost ?? card.cost;
                    merged.damage=card.damage; merged.upgradeDamage=card.upgradeDamage ?? card.damage;
                    merged.block=card.block; merged.upgradeBlock=card.upgradeBlock ?? card.block;
                    merged.magicNumber=card.magicNumber; merged.upgradeMagicNumber=card.upgradeMagicNumber ?? card.magicNumber;
                } else {
                    if((input.damage.HasValue||input.upgradeDamage.HasValue) && !card.damage.HasValue) throw new InvalidOperationException(card.name+"没有伤害字段，请按卡牌类型筛选后批量修改。");
                    if((input.block.HasValue||input.upgradeBlock.HasValue) && !card.block.HasValue) throw new InvalidOperationException(card.name+"没有格挡字段，请按卡牌类型筛选后批量修改。");
                    if((input.magicNumber.HasValue||input.upgradeMagicNumber.HasValue) && !card.magicNumber.HasValue) throw new InvalidOperationException(card.name+"没有附加数值字段。");
                    if(input.cost.HasValue) { merged.cost=input.cost; merged.upgradeCost=input.upgradeCost ?? (input.cost.Value<0?input.cost.Value:Math.Max(0,input.cost.Value+(card.upgradeCost??card.cost)-card.cost)); }
                    if(input.upgradeCost.HasValue) merged.upgradeCost=input.upgradeCost;
                    if(input.damage.HasValue) { merged.damage=input.damage; merged.upgradeDamage=input.upgradeDamage ?? input.damage.Value+(card.upgradeDamage??card.damage.Value)-card.damage.Value; }
                    if(input.upgradeDamage.HasValue) merged.upgradeDamage=input.upgradeDamage;
                    if(input.block.HasValue) { merged.block=input.block; merged.upgradeBlock=input.upgradeBlock ?? input.block.Value+(card.upgradeBlock??card.block.Value)-card.block.Value; }
                    if(input.upgradeBlock.HasValue) merged.upgradeBlock=input.upgradeBlock;
                    if(input.magicNumber.HasValue) { merged.magicNumber=input.magicNumber; merged.upgradeMagicNumber=input.upgradeMagicNumber ?? input.magicNumber.Value+(card.upgradeMagicNumber??card.magicNumber.Value)-card.magicNumber.Value; }
                    if(input.upgradeMagicNumber.HasValue) merged.upgradeMagicNumber=input.upgradeMagicNumber;
                }
                result.Add(new NativeEditPlan{Id=card.id,Values=merged});
            }
            if(result.Count==0) throw new InvalidOperationException("请先选择至少一张原生卡牌。");
            return result;
        }
        static bool Flag(Dictionary<string,object> data,string key) { return data.ContainsKey(key) && Convert.ToBoolean(data[key]); }
        static string Error(LiveResponse r) { object raw; return r.Error ?? (r.Data.TryGetValue("errors",out raw)?JsonUtil.ToJson(raw):r.Data.TryGetValue("error",out raw)?Convert.ToString(raw):"游戏未确认生效。"); }
        public static NativeEditResult Apply(LiveClient client,List<NativeEditPlan> plans)
        {
            LiveResponse status=client.Status();
            if(!status.Ok || !Flag(status.Data,"inRun")) throw new InvalidOperationException("请先进入本局，地图或可操作战斗回合均可修改。未写入离线配置。");
            List<string> scopes=new List<string>{"MASTER_ID","DEFINITION"}; if(Flag(status.Data,"inCombat")) scopes.Add("COMBAT_ID");
            foreach(NativeEditPlan plan in plans) {
                LiveResponse valid=client.Validate("override",plan.Id,"",null,plan.Values,scopes,status.Revision+1);
                if(!valid.Ok || !Flag(valid.Data,"valid")) throw new InvalidOperationException(plan.Id+" 校验失败："+Error(valid));
            }
            NativeEditResult result=new NativeEditResult();
            foreach(NativeEditPlan plan in plans) {
                try {
                    status=client.Status(); if(!status.Ok || !Flag(status.Data,"inRun")) throw new InvalidOperationException("游戏局已退出。");
                    bool combat=Flag(status.Data,"inCombat"); List<string> current=new List<string>{"MASTER_ID","DEFINITION"}; if(combat) current.Add("COMBAT_ID");
                    LiveResponse applied=client.Apply("override",plan.Id,"",null,plan.Values,current,status.Revision+1);
                    if(!applied.Ok || !Flag(applied.Data,"applied")) throw new InvalidOperationException(Error(applied));
                    result.Applied.Add(plan); object instances; if(applied.Data.TryGetValue("affectedInstances",out instances)) result.Instances+=Convert.ToInt32(instances);
                } catch(Exception error) { result.Error=plan.Id+"："+error.Message; break; }
            }
            return result;
        }
    }
}
