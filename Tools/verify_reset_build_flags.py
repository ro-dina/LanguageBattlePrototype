"""Compile the actual reset-access code under product/editor/development flags."""
import argparse, pathlib, subprocess
parser=argparse.ArgumentParser();parser.add_argument('output',type=pathlib.Path);parser.add_argument('--unity-contents',type=pathlib.Path,default=pathlib.Path('/Applications/Unity/Hub/Editor/6000.4.8f1/Unity.app/Contents'));args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
root=pathlib.Path(__file__).resolve().parents[1];scripting=args.unity_contents/'Resources/Scripting';compiler=scripting/'DotNetSdkRoslyn/csc.dll';dotnet=scripting/'NetCoreRuntime/dotnet';mono=scripting/'MonoBleedingEdge/bin/mono';reference=scripting/'UnityReferenceAssemblies/unity-4.8-api/mscorlib.dll'
service=(root/'Assets/Scripts/ProgressResetService.cs').read_text();assert 'ProgressResetAccess.Allows(kind)' in service and 'CanReset(request.Kind)' in service
probe=args.output/'ResetGateProbe.cs';probe.write_text('''using System;
public static class ResetGateProbe {
 public static int Main(string[] args) {
  bool development=args[0]=="true";int calls=0;
  if(ProgressResetAccess.DevelopmentEnabled!=development)return 1;
  foreach(ProgressResetKind kind in Enum.GetValues(typeof(ProgressResetKind))) {
   bool expected=kind==ProgressResetKind.NewGame || development;int before=calls;
   if(ProgressResetAccess.Allows(kind)!=expected)return 2;
   bool result=ProgressResetAccess.Run(kind,()=>{calls++;return true;});
   if(result!=expected || calls-before!=(expected?1:0))return 3;
  }
  if(ProgressResetAccess.Run((ProgressResetKind)999,()=>{calls++;return true;}))return 4;
  Console.WriteLine("new-game=True partial="+development+" callbacks="+calls+" unknown=False");return 0;
 }
}''')
lines=[]
for name,define,enabled in [('Release',None,False),('Development','DEVELOPMENT_BUILD',True),('Editor','UNITY_EDITOR',True)]:
 assembly=args.output/(name+'.exe');command=[str(dotnet),str(compiler),'-nologo','-noconfig','-nostdlib','-target:exe','-out:'+str(assembly),'-reference:'+str(reference),str(root/'Assets/Scripts/ProgressResetKind.cs'),str(root/'Assets/Scripts/ProgressResetAccess.cs'),str(probe)]
 if define:command.append('-define:'+define)
 subprocess.run(command,check=True,capture_output=True,text=True)
 result=subprocess.run([str(mono),str(assembly),'true' if enabled else 'false'],check=True,capture_output=True,text=True);line=name+': '+result.stdout.strip();lines.append(line);print(line)
(args.output/'results.txt').write_text('\n'.join(lines)+'\n')
