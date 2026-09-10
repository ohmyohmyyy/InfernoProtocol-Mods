using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using InfernoProtocol.Renovator;

int checks=0;
void Check(bool result,string message) { checks++; if(!result) throw new Exception(message); }
var signatures=new HashSet<string>();
foreach(int p in RenovatorPatterns.Catalog)
{
    double signature=0;
    for(int y=0;y<64;y++) for(int x=0;x<64;x++)
    {
        float u=x/64f,v=y/64f;
        var c=RenovatorPatterns.Sample(p,u,v);
        Check(float.IsFinite(c.R)&&c.R>=0&&c.R<=1&&float.IsFinite(c.G)&&c.G>=0&&c.G<=1&&float.IsFinite(c.B)&&c.B>=0&&c.B<=1,"RGB bounds");
        Check(c==RenovatorPatterns.Sample(p,u,v),"Determinism");
        Check(c==RenovatorPatterns.Sample(p,u+2,v-3),"Repeating coordinates");
        signature+=c.R+3*c.G+7*c.B;
    }
    Check(signatures.Add(signature.ToString("R")),"Duplicate pattern");
}
foreach(string item in new[]{"Wall","WallWithWindow","Wooden_Wall_2x2","Stone_Floor_1x1","Wooden_Roof_Curved_26"}) Check(RenovatorPatterns.Eligible(item),"Supported building piece");
foreach(string item in new[]{"WallTorch","WallSign","PrimitiveSpikeWall","BuildingHammer","Log","StorageBox"}) Check(!RenovatorPatterns.Eligible(item),"Unrelated object excluded");
for(int p=-1;p<=RenovatorPatterns.Names.Length;p++) for(int s=-1;s<=4;s++) Check(RenovatorPatterns.Valid(p,s)==(RenovatorPatterns.Catalog.Contains(p)&&s>=0&&s<4),"Choice validation");
Check(RenovatorMapping.Surface(1,2,3,0,0,1)==(1f,2f),"Wall mapping");
Check(RenovatorMapping.Surface(1,2,3,0,0,-1)==(1f,2f),"Back face orientation");
Check(RenovatorMapping.Surface(1,2,3,0,1,0)==(1f,3f),"Floor mapping");
Check(RenovatorMapping.Surface(1,2,3,1,0,0)==(3f,2f),"Side wall mapping");
var roof=RenovatorMapping.Surface(0,1,1,0,1,-1);
Check(MathF.Abs(MathF.Abs(roof.V)-MathF.Sqrt(2))<.00001f,"Sloped roof physical scale");
for(int i=0;i<100;i++)
{
    float x=i*.01f;
    var front=RenovatorMapping.Surface(x,2,.1f,0,0,1);
    var otherPlank=RenovatorMapping.Surface(x,2,.15f,0,0,1);
    Check(front==otherPlank,"Independent plank depths do not restart pattern");
}
Console.WriteLine($"Renovator tests passed: {checks} assertions.");
int patternCount=RenovatorPatterns.Catalog.Length;
Check(patternCount==27,"Curated Renovator catalog");
Check(RenovatorPatterns.Names.Length==30,"Persistent pattern ID range remains stable");
Check(!RenovatorPatterns.Available(18)&&!RenovatorPatterns.Available(19)&&!RenovatorPatterns.Available(27),"Retired finishes stay out of the catalog");
Check(new HashSet<string>(RenovatorPatterns.Catalog.Select(i=>RenovatorPatterns.Names[i]),StringComparer.OrdinalIgnoreCase).Count==patternCount,"Unique finish names");
for(int i=0;i<patternCount;i++) foreach(int direction in new[]{-3,-1,0,1,3})
{
    int result=RenovatorNavigation.Move(i,direction,patternCount);
    Check(result>=0&&result<patternCount,"Navigation bounds");
    if(direction==1&&i%3==2||direction==-1&&i%3==0) Check(result==i,"No horizontal row wrapping");
}
Check(RenovatorNavigation.Move(6,3,patternCount)==9,"Next page via D-pad");
Check(RenovatorNavigation.Move(9,-3,patternCount)==6,"Previous page via D-pad");
Check(RenovatorNavigation.Move(25,3,patternCount)==26,"Final catalog row clamps safely");
Console.WriteLine("Renovator navigation checks passed.");
string gameAssembly=@"C:\Program Files (x86)\Steam\steamapps\common\Inferno Protocol\BepInEx\interop\Assembly-CSharp.dll";
Check(File.Exists(gameAssembly),"Installed game interop assembly available");
using(ModuleDefMD game=ModuleDefMD.Load(gameAssembly))
{
    TypeDef player=game.GetTypes().Single(t=>t.FullName=="Game.PlayerOperations.Player");
    TypeDef inputs=game.GetTypes().Single(t=>t.FullName=="Game.PlayerOperations.PlayerInputDispatcher/PlayerActionContainer");
    TypeDef saved=game.GetTypes().Single(t=>t.FullName=="SaveSystem.SimplePlacableData");
    string[] nativePlacement={"BeginPreviewPlacable","UpdatePlacablePreview","EndPreviewPlacable","GridSnap","EvaluateSnapHelpers","OnPlacableRotateBegin","OnPlace","TryRemovePlacableObject"};
    foreach(string method in nativePlacement) Check(player.Methods.Any(m=>m.Name==method),"Native placement API: "+method);
    foreach(string property in new[]{"_placablePreviewTransform","_placablePreviewRotation","_rawRotationY","_isSnapped","_canPlace"})
        Check(player.Properties.Any(p=>p.Name==property),"Native placement state: "+property);
    Check(!player.Methods.Any(m=>m.Name.String is "MovePlacable" or "BeginMovePlacable" or "EditPlacableTransform" or "RepositionPlacable"),"No native post-placement move method");
    Check(!inputs.Properties.Any(p=>p.Name.String.Contains("placableMove",StringComparison.OrdinalIgnoreCase)||p.Name.String.Contains("movePlacable",StringComparison.OrdinalIgnoreCase)),"No native post-placement move input");
    Check(saved.Fields.Any(f=>f.Name=="position")&&saved.Fields.Any(f=>f.Name=="rotation"),"Native saves capture transformed pose");
}
Console.WriteLine("Installed game API audit passed: build-preview placement, rotation and removal exist; post-placement movement does not.");
string pluginAssembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../Renovator/bin/Release/net6.0/InfernoProtocol.Renovator.dll"));
Check(File.Exists(pluginAssembly),"Built Renovator plugin available");
using(ModuleDefMD plugin=ModuleDefMD.Load(pluginAssembly))
{
    TypeDef controller=plugin.GetTypes().Single(t=>t.FullName=="InfernoProtocol.Renovator.RenovatorController");
    foreach(MethodDef method in controller.Methods.Where(m=>!m.IsStatic&&!m.IsConstructor&&!m.CustomAttributes.Any(a=>a.AttributeType.FullName=="Il2CppInterop.Runtime.Attributes.HideFromIl2CppAttribute")))
    {
        IEnumerable<TypeSig> exposedTypes=method.MethodSig.Params.Append(method.MethodSig.RetType);
        Check(!exposedTypes.Any(t=>t.FullName.StartsWith("InfernoProtocol.Renovator.",StringComparison.Ordinal)),"Managed-only signature must be hidden from IL2CPP: "+method.Name);
    }
}
Console.WriteLine("Renovator IL2CPP registration audit passed.");
if(args.Length==2&&args[0]=="--preview") PatternPreview.Write(args[1]);
