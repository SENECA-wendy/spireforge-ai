"""One-time extraction / repeatable C# resource generation. No translation service calls."""
import json, pathlib, re, sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
FILES = ['MainForm.cs','CompactUiForm.cs','RealtimeCardsForm.cs','DeckManagerForm.cs','CardPoolForm.cs','CustomCardEditorDialog.cs','CustomCardLibraryEditing.cs','LiveDefinitionSync.cs','NativeCardPreview.cs','CustomCardPreview.cs','Professions.cs']
TOKEN = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"')
RESOURCE = ROOT / 'localization/en.json'
def generate():
    entries=json.loads(RESOURCE.read_text(encoding='utf-8'))
    lines=['// Generated from localization/en.json by tools/prepare-localization.py.','using System.Collections.Generic;','namespace StsCardEditor { internal static class UiStrings { internal static Dictionary<string,string> Create() { return new Dictionary<string,string> {']
    for k,v in entries.items(): lines.append('    {'+json.dumps(k,ensure_ascii=False)+','+json.dumps(v,ensure_ascii=False)+'},')
    lines.append('}; } } }')
    (ROOT/'app/UiStrings.cs').write_text('\n'.join(lines)+'\n',encoding='utf-8')
if '--initialize' in sys.argv:
    keys=json.loads((ROOT/'build/ui-strings.json').read_text(encoding='utf-8'))
    translated={}
    for line in (ROOT/'tools/ui-english.tsv').read_text(encoding='utf-8').splitlines():
        number,value=line.split('\t',1);translated[int(number)]=value.replace('\\r','\r').replace('\\n','\n').replace('\\\\','\\')
    if set(translated)!=set(range(len(keys))): raise ValueError('Missing or extra translations')
    entries={key:translated[i] for i,key in enumerate(keys)}
    entries['SpireForge AI｜尖塔铸卡 v1.7.0']=entries.pop(keys[0])
    RESOURCE.parent.mkdir(exist_ok=True)
    RESOURCE.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    # These are stable internal option values. Format events localize their display.
    stable={'全部','主牌组','手牌','抽牌堆','弃牌堆','消耗堆','伤害 6','格挡 5','抽 1 张牌','每打出牌抽 1','易伤 2 层','生成一张牌','疯狂'}
    for filename in FILES:
        path=ROOT/'app'/filename;source=path.read_text(encoding='utf-8-sig')
        def replace(m):
            token=m.group()
            if not token.startswith('"') or not re.search('[\u4e00-\u9fff]',token):return token
            value=json.loads(token)
            if value==keys[0]:return 'UiText.T("SpireForge AI｜尖塔铸卡 v1.7.0")'
            if value in stable:return token
            before=source[max(0,m.start()-65):m.start()]
            if re.search(r'(?:case\s*|==\s*|!=\s*)$',before):return token
            return 'UiText.T('+token+')'
        source=TOKEN.sub(replace,source)
        path.write_text(source,encoding='utf-8')
generate()
print('Generated bilingual resources:',RESOURCE)
