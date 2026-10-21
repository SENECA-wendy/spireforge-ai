using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace StsCardEditor
{
    public static class CreativeBehavior
    {
        public static string Check(CreativeBuildResult result,CreativeContract contract,string gameDir,string jdk)
        {
            if(contract.probes.Count==0)return "简化环境行为检查：未运行（此设计没有可可靠模拟的即时指标）";
            string sources=Path.Combine(AppPaths.AppDir,"tools","creative-probe-src");
            if(!Directory.Exists(sources))throw new FileNotFoundException("缺少行为检查工具，请使用完整发行目录。");
            string root=Path.GetDirectoryName(result.JarPath),classes=Path.Combine(root,"behavior-classes");Directory.CreateDirectory(classes);
            string jar=Path.Combine(gameDir,"desktop-1.0-original.jar");if(!File.Exists(jar))jar=Path.Combine(gameDir,"desktop-1.0.jar");
            Run(Path.Combine(jdk,"bin","javac.exe"),"-J-Dfile.encoding=UTF-8 -J-Duser.language=en --release 8 -encoding UTF-8 -cp "+Quote(jar)+" -d "+Quote(classes)+" "+string.Join(" ",Directory.GetFiles(sources,"*.java",SearchOption.AllDirectories).Select(Quote).ToArray()),root,false);
            string config=Path.Combine(root,"behavior-input.json");
            EffectModel effect=result.Card.effects[0];
            JsonUtil.WriteUtf8(config,JsonUtil.ToJson(new {className=ReadClass(result.ManifestPath),card=result.Card,parameters=contract.parameters,probes=contract.probes,allowMasterDeckChange=contract.allowMasterDeckChange,baseAmount=effect.amount??1,upgradedAmount=effect.upgradeAmount??effect.amount??1}));
            string output=Run(Path.Combine(jdk,"bin","java.exe"),"-Dfile.encoding=UTF-8 -Xmx128m -cp "+Quote(classes+";"+result.JarPath+";"+jar)+" CreativeProbeMain "+Quote(config),root,true);
            JsonUtil.WriteUtf8(Path.Combine(root,"behavior-output.txt"),output);
            return output.Trim();
        }
        static string ReadClass(string path){return Convert.ToString(((Dictionary<string,object>)JsonUtil.FromJson(JsonUtil.ReadUtf8(path)))["className"]);}
        static string Quote(string value){if(value.Contains("\"")||value.Contains("\n")||value.Contains("\r"))throw new InvalidDataException("工具路径无效。");return "\""+value+"\"";}
        static string Run(string exe,string args,string directory,bool allowUnsupported)
        {
            using(Process process=Process.Start(new ProcessStartInfo(exe,args){WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8})) {
                var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(20000)){process.Kill();throw new TimeoutException("行为检查超时，不能判定卡牌效果正确。");}
                string output=stdout.Result+stderr.Result;
                if(process.ExitCode!=0 && !(allowUnsupported && process.ExitCode==2))throw new InvalidDataException("行为检查失败：\n"+output);
                return output;
            }
        }
    }
}
