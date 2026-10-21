using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StsCardEditor
{
    // Only repair an absent game import when the installed jar has exactly one matching type.
    public static class JavaImportRepair
    {
        static string CodeMask(string code)
        {
            return Regex.Replace(code,@"//[^\r\n]*|/\*[\s\S]*?\*/|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'",
                m=>Regex.Replace(m.Value,@"[^\r\n]"," "));
        }
        public static Dictionary<string,string> Repair(Dictionary<string,string> sources,string gameJar,out List<string> corrections)
        {
            corrections=new List<string>();
            HashSet<string> types=new HashSet<string>(StringComparer.Ordinal);
            using(ZipArchive jar=ZipFile.OpenRead(gameJar))
                foreach(ZipArchiveEntry entry in jar.Entries)
                    if(entry.FullName.StartsWith("com/megacrit/cardcrawl/",StringComparison.Ordinal) && entry.FullName.EndsWith(".class",StringComparison.Ordinal) && !entry.FullName.Contains("$"))
                        types.Add(entry.FullName.Substring(0,entry.FullName.Length-6).Replace('/','.'));
            var names=types.GroupBy(t=>t.Substring(t.LastIndexOf('.')+1)).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
            Dictionary<string,string> result=new Dictionary<string,string>();
            foreach(var file in sources) {
                string code=file.Value;StringBuilder repaired=new StringBuilder(code);
                MatchCollection imports=Regex.Matches(CodeMask(code),@"\bimport\s+(?<type>com\.megacrit\.cardcrawl(?:\.[A-Za-z_$][A-Za-z0-9_$]*)+)\s*;");
                foreach(Match match in imports.Cast<Match>().Reverse()) {
                    Group type=match.Groups["type"];string original=type.Value;
                    if(types.Contains(original))continue;
                    string name=original.Substring(original.LastIndexOf('.')+1);string[] candidates;
                    if(!names.TryGetValue(name,out candidates) || candidates.Length!=1)continue;
                    repaired.Remove(type.Index,type.Length).Insert(type.Index,candidates[0]);
                    corrections.Add(file.Key+": "+original+" → "+candidates[0]);
                }
                result[file.Key]=repaired.ToString();
            }
            return result;
        }
    }
}
