import re,pathlib,json
ROOT=pathlib.Path(__file__).resolve().parents[1]
guidmap={}
for base in ['Assets','Library/PackageCache']:
 for p in (ROOT/base).rglob('*.meta'):
  m=re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)
  if m: guidmap[m[1]]=str(p.relative_to(ROOT))[:-5]
def value(b,k,default=''):
 m=re.search(r'^  '+re.escape(k)+r': (.*)$',b,re.M);return m[1] if m else default
def ref(b,k):
 m=re.search(r'fileID: (-?\d+)',value(b,k));return int(m[1]) if m else 0
class Asset:
 def __init__(self,p):
  self.p=p;self.raw=p.read_text();self.blocks={};self.types={};self.headers={}
  for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)([^\n]*)\n(.*?)(?=^--- !u!|\Z)',self.raw,re.M|re.S):
   i=int(m[2]);self.blocks[i]=m[4];self.types[i]=int(m[1]);self.headers[i]=m[0][:-len(m[4])]
  self.gos={i:b for i,b in self.blocks.items() if self.types[i]==1}
  self.transforms={ref(b,'m_GameObject'):i for i,b in self.blocks.items() if self.types[i] in [4,224]}
 def compname(self,i):
  b=self.blocks[i]
  if self.types[i]!=114:return b.split(':',1)[0]
  m=re.search('guid: (\w+)',value(b,'m_Script'));return pathlib.Path(guidmap.get(m[1],'MISSING:'+m[1])).stem if m else 'MISSING SCRIPT'
 def components(self,go):return [int(x) for x in re.findall(r'component: {fileID: (\d+)}',self.gos[go])]
 def path(self,go):
  name=value(self.gos[go],'m_Name');t=self.transforms.get(go);par=ref(self.blocks.get(t,''),'m_Father');pgo=ref(self.blocks.get(par,''),'m_GameObject')
  return (self.path(pgo)+'/' if pgo in self.gos else '')+name
 def dump(self):
  print('\n###',self.p.relative_to(ROOT))
  for go in sorted(self.gos,key=self.path):
   comps=self.components(go);names=[self.compname(i) for i in comps];b=self.blocks.get(self.transforms.get(go),'')
   print(self.path(go),'[OFF]' if value(self.gos[go],'m_IsActive')=='0' else '', ' | '.join(n for n in names if n not in ['CanvasRenderer','RectTransform','Transform']))
   if 'RectTransform' in names:
    print('  rect',*[k+'='+value(b,k) for k in ['m_AnchorMin','m_AnchorMax','m_AnchoredPosition','m_SizeDelta','m_Pivot','m_LocalScale']])
   for i,n in zip(comps,names):
    cb=self.blocks[i]
    if n in ['CanvasScaler','GridLayoutGroup','VerticalLayoutGroup','HorizontalLayoutGroup','ContentSizeFitter','LayoutElement','ScrollRect','AspectRatioFitter'] or n.endswith('Manager'):
     print(' ',n, re.sub(r'^.*?m_EditorClassIdentifier:.*?\n','',cb,flags=re.S).strip().replace('\n','; '))
    if n=='TextMeshProUGUI': print('  text',value(cb,'m_text')[:100], 'font='+value(cb,'m_fontSize'),'auto='+value(cb,'m_enableAutoSizing'),'color='+value(cb,'m_fontColor'))
    if n=='Button':print('  onClick:',re.findall(r'm_MethodName: (.*)',cb))
if __name__=='__main__':
 import sys
 failures=[]; assets=[]
 for p in sorted((ROOT/'Assets/Scenes').glob('*.unity'))+sorted((ROOT/'Assets/Prefabs').glob('*.prefab'))+sorted((ROOT/'Assets/Resources/UI').glob('*.prefab')):
  a=Asset(p);assets.append(a)
  if '--dump' in sys.argv:a.dump()
  for i,b in a.blocks.items():
   if a.types[i]==114 and a.compname(i).startswith('MISSING'):failures.append(f'{p.name}: missing script {i}')
   for m in re.finditer(r'\{fileID: (-?\d+)(?:, guid: (\w+), type: \d+)?\}',b):
    target=int(m[1]);guid=m[2]
    if target==0:continue
    if guid:
     if guid not in guidmap and not guid.startswith('0000000000000000'):failures.append(f'{p.name}: unknown asset {guid}')
    elif target not in a.blocks:failures.append(f'{p.name}: missing local reference {target} in {i}')
   if a.types[i]==114 and a.compname(i).endswith('Manager'):
    for prop in re.findall(r'^  (\w+): \{fileID: 0\}$',b,re.M):
     if not prop.startswith('m_'):failures.append(f'{p.name}: unassigned {a.compname(i)}.{prop}')
   for target,method in re.findall(r'm_Target: \{fileID: (\d+)\}.*?m_MethodName: (\w+)',b,re.S):
    cb=a.blocks.get(int(target),'');m=re.search('guid: (\w+)',value(cb,'m_Script'))
    script=ROOT/guidmap[m[1]] if m and m[1] in guidmap else None
    if script and not re.search(r'\b'+method+r'\s*\(',script.read_text()):failures.append(f'{p.name}: missing callback {method}')
 if failures:
  print('\n'.join(sorted(set(failures))));sys.exit(1)
 print(f'PASS: {len(assets)} scenes/prefabs; script GUIDs, local/asset references, manager fields and persistent callback methods resolved.')
