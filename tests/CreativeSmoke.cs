using System;
using System.IO;
using StsCardEditor;
class CreativeSmoke
{
    static int Main()
    {
        try {
            ApiSettings settings = ProfileStore.LoadSettings(); settings.timeoutSeconds = Math.Max(settings.timeoutSeconds,180);
            if (string.IsNullOrEmpty(settings.apiKey)) throw new InvalidOperationException("没有可用的已配置模型凭据，跳过真实服务测试。");
            CreativeBuildResult result = CreativeCardService.Generate(settings,
                "设计一张0费能力测试卡：本场战斗，每回合首次打出攻击牌后，获得等于当时格挡一半的能量（向下取整，每次最多3点）；升级后每次最多4点。使用自定义能力记录本回合是否触发，并在下回合重置。不要用一次获得能量等替代。",
                "回声壁垒（联调测试）", GameLocator.FindGameDir(),1.0);
            string output = Path.Combine(AppPaths.AppDir,"creative-smoke-result.json");
            JsonUtil.WriteUtf8(output, JsonUtil.ToJson(new { success=true, result.Card.name, result.DesignSummary, result.SourceDirectory, result.JarPath, installed=false }));
            Console.WriteLine("PASS real model generated a custom event-driven Java card and compilation succeeded; not installed into game.");
            Console.WriteLine(output);
            return 0;
        } catch (Exception e) { Console.WriteLine(e.Message); return 1; }
    }
}
