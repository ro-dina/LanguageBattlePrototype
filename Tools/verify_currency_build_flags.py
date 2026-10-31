"""Compile and execute the exact development-funds gate with release/editor/development defines."""
import argparse,pathlib,subprocess
p=argparse.ArgumentParser();p.add_argument('output',type=pathlib.Path);p.add_argument('--unity-contents',type=pathlib.Path,default=pathlib.Path('/Applications/Unity/Hub/Editor/6000.4.8f1/Unity.app/Contents'));a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
root=pathlib.Path(__file__).resolve().parents[1];scripts=a.unity_contents/'Resources/Scripting';dotnet=scripts/'NetCoreRuntime/dotnet';compiler=scripts/'DotNetSdkRoslyn/csc.dll';mono=scripts/'MonoBleedingEdge/bin/mono';reference=scripts/'UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll'
probe=a.output/'CurrencyGateProbe.cs';probe.write_text('''using System;
public static class CurrencyGateProbe {
 public static int Main(string[] args) {
  bool expected=args[0]=="true";int grants=0;bool result=DevelopmentCurrencyAccess.Run(()=>grants++);
  if(DevelopmentCurrencyAccess.Enabled!=expected || result!=expected || grants!=(expected?1:0))return 1;
  Console.WriteLine("enabled="+DevelopmentCurrencyAccess.Enabled+" grants="+grants);return 0;
 }
}
''')
lines=[]
for name,define,expected in [('Release',None,False),('Development','DEVELOPMENT_BUILD',True),('Editor','UNITY_EDITOR',True)]:
 assembly=a.output/(name+'.exe');cmd=[str(dotnet),str(compiler),'-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(assembly),'-reference:'+str(reference),str(root/'Assets/Scripts/DevelopmentCurrencyAccess.cs'),str(probe)]
 if define:cmd.append('-define:'+define)
 subprocess.run(cmd,check=True,capture_output=True,text=True)
 run=subprocess.run([str(mono),str(assembly),'true' if expected else 'false'],check=True,capture_output=True,text=True);line=name+': '+run.stdout.strip();lines.append(line);print(line)
(a.output/'results.txt').write_text('\n'.join(lines)+'\n')
