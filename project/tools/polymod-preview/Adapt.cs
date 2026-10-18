using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Offline, exact-input adaptation. Never loads PolyMod or invokes Harmony/Unity.
internal static class Adapt
{
    const string Accepted="a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016";
    const string Owner="PolyMod.Multiplayer.Client", Method="Deserialize_Postfix";
    const string TargetBlob="01005347616d6553746174652c2047616d654c6f676963417373656d626c792c2056657273696f6e3d302e302e302e302c2043756c747572653d6e65757472616c2c205075626c69634b6579546f6b656e3d6e756c6c0b446573657269616c697a650000";
    static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
    static IEnumerable<TypeDefinition> Types(ModuleDefinition m)=>m.Types.SelectMany(Descendants);
    static IEnumerable<TypeDefinition> Descendants(TypeDefinition t)=>new[]{t}.Concat(t.NestedTypes.SelectMany(Descendants));
    static bool Removed(CustomAttribute a)=>a.AttributeType.FullName is "HarmonyLib.HarmonyPatch" or "HarmonyLib.HarmonyPostfix";
    static MethodDefinition Selected(ModuleDefinition m)=>Types(m).Single(t=>t.FullName==Owner).Methods.Single(x=>x.Name==Method);
    static string TypeKey(TypeReference? t)=>t is null?"null":t.FullName+"@"+t.Scope;
    static string Attr(CustomAttribute a)=>a.Constructor.FullName+"@"+a.Constructor.DeclaringType.Scope+":"+Convert.ToHexString(a.GetBlob());
    static string Attrs(ICustomAttributeProvider p)=>string.Join("|",p.CustomAttributes.Select(Attr));
    static string Constant(object? o)=>o is null?"null":o.GetType().FullName+":"+Convert.ToString(o,CultureInfo.InvariantCulture);
    static string Body(MethodDefinition m)
    {
        if(!m.HasBody)return "no-body";
        var b=m.Body;var ins=b.Instructions;int Index(Instruction? x)=>x is null?-1:ins.IndexOf(x);
        string Operand(object? o)=>o switch
        {
            Instruction x=>"branch:"+Index(x),Instruction[] xs=>"switch:"+string.Join(",",xs.Select(Index)),
            VariableDefinition v=>"local:"+v.Index,ParameterDefinition p=>"arg:"+p.Index,
            TypeReference t=>"type:"+TypeKey(t),
            MemberReference r=>"member:"+r.FullName+"@"+r.DeclaringType?.Scope,
            CallSite s=>"callsite:"+s.FullName,byte[] bytes=>Convert.ToHexString(bytes),_=>Constant(o)
        };
        return JsonSerializer.Serialize(new{b.InitLocals,Variables=b.Variables.Select(v=>TypeKey(v.VariableType)).ToArray(),
            Instructions=ins.Select(i=>i.OpCode.Code+":"+Operand(i.Operand)).ToArray(),
            Handlers=b.ExceptionHandlers.Select(e=>new{Kind=e.HandlerType.ToString(),TryStart=Index(e.TryStart),TryEnd=Index(e.TryEnd),HandlerStart=Index(e.HandlerStart),HandlerEnd=Index(e.HandlerEnd),Filter=Index(e.FilterStart),Catch=TypeKey(e.CatchType)}).ToArray()});
    }
    static SortedDictionary<string,string> Bodies(ModuleDefinition m)=>new(Types(m).SelectMany(t=>t.Methods).ToDictionary(x=>x.FullName,Body),StringComparer.Ordinal);
    static SortedDictionary<string,string> Metadata(AssemblyDefinition a,bool excludeSelected)
    {
        var d=new SortedDictionary<string,string>(StringComparer.Ordinal);var m=a.MainModule;
        d["assembly"]=a.Name.FullName+":"+a.Name.Attributes+":"+Attrs(a);
        d["module"]=m.Name+":"+m.Kind+":"+m.RuntimeVersion+":"+m.Architecture+":"+m.Attributes+":"+m.Mvid+":"+Attrs(m);
        d["references"]=string.Join("|",m.AssemblyReferences.Select(r=>r.FullName+":"+r.Attributes+":"+Convert.ToHexString(r.Hash)));
        d["module-references"]=string.Join("|",m.ModuleReferences.Select(r=>r.Name));
        d["entry"]=m.EntryPoint?.FullName??"none";
        foreach(var t in Types(m))
        {
            d["type:"+t.FullName]=t.Attributes+":"+TypeKey(t.BaseType)+":"+t.PackingSize+":"+t.ClassSize+":"+Attrs(t);
            d["interfaces:"+t.FullName]=string.Join("|",t.Interfaces.Select(i=>TypeKey(i.InterfaceType)+":"+Attrs(i)));
            foreach(var f in t.Fields)d["field:"+f.FullName]=f.Attributes+":"+f.Offset+":"+Constant(f.Constant)+":"+Convert.ToHexString(f.InitialValue)+":"+Attrs(f);
            foreach(var p in t.Properties)d["property:"+p.FullName]=p.Attributes+":"+Constant(p.Constant)+":"+p.GetMethod?.FullName+":"+p.SetMethod?.FullName+":"+Attrs(p);
            foreach(var e in t.Events)d["event:"+e.FullName]=e.Attributes+":"+e.AddMethod?.FullName+":"+e.RemoveMethod?.FullName+":"+e.InvokeMethod?.FullName+":"+Attrs(e);
            foreach(var method in t.Methods)
            {
                string attrs=excludeSelected&&t.FullName==Owner&&method.Name==Method?string.Join("|",method.CustomAttributes.Where(x=>!Removed(x)).Select(Attr)):Attrs(method);
                d["method:"+method.FullName]=method.Attributes+":"+method.ImplAttributes+":"+method.CallingConvention+":"+attrs;
                d["return:"+method.FullName]=Attrs(method.MethodReturnType);
                foreach(var p in method.Parameters)d["param:"+method.FullName+":"+p.Index]=p.Name+":"+p.Attributes+":"+Constant(p.Constant)+":"+Attrs(p);
                foreach(var g in method.GenericParameters)d["generic:"+method.FullName+":"+g.Position]=g.Name+":"+g.Attributes+":"+Attrs(g)+":"+string.Join("|",g.Constraints.Select(x=>TypeKey(x.ConstraintType)+":"+Attrs(x)));
            }
            foreach(var g in t.GenericParameters)d["generic-type:"+t.FullName+":"+g.Position]=g.Name+":"+g.Attributes+":"+Attrs(g)+":"+string.Join("|",g.Constraints.Select(x=>TypeKey(x.ConstraintType)+":"+Attrs(x)));
        }
        return d;
    }
    static SortedDictionary<string,string> Resources(ModuleDefinition m)
    {
        var d=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var r in m.Resources)
        {
            Require(r is EmbeddedResource,"Unexpected nonembedded resource; adaptation not authorized.");
            d[r.Name]=r.Attributes+":"+Hash(((EmbeddedResource)r).GetResourceData());
        }
        return d;
    }
    static string Digest(object o)=>Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(o)));
    static int PatchCount(ModuleDefinition m)=>Types(m).SelectMany(t=>t.Methods).Sum(x=>x.CustomAttributes.Count(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch"));
    static void Equal(SortedDictionary<string,string> a,SortedDictionary<string,string>b,string kind)
    {
        Require(a.Count==b.Count,kind+" count changed.");
        foreach(var p in a)
        {
            Require(b.TryGetValue(p.Key,out var v),kind+" missing: "+p.Key);
            if(v!=p.Value){int at=0;while(at<Math.Min(v!.Length,p.Value.Length)&&v[at]==p.Value[at])at++;
                throw new InvalidDataException(kind+" changed: "+p.Key+" at "+at+" BEFORE "+p.Value.Substring(Math.Max(0,at-90),Math.Min(p.Value.Length-Math.Max(0,at-90),240))+" AFTER "+v.Substring(Math.Max(0,at-90),Math.Min(v.Length-Math.Max(0,at-90),240)));}
        }
    }
    static void ValidatePolicy(AssemblyDefinition a)
    {
        Require(a.Modules.Count==1&&!a.Name.HasPublicKey,"Unexpected module/signing structure.");
        var t=Types(a.MainModule).Single(x=>x.FullName==Owner);var m=Selected(a.MainModule);
        Require(!t.CustomAttributes.Any(x=>x.AttributeType.Namespace=="HarmonyLib"),"Unexpected class Harmony attributes.");
        Require(m.IsPrivate&&m.IsStatic&&m.ReturnType.FullName=="System.Void"&&m.Parameters.Select(x=>x.ParameterType.FullName).SequenceEqual(new[]{"GameState","System.IO.BinaryReader"})&&m.Parameters.Select(x=>x.Name).SequenceEqual(new[]{"__instance","__0"}),"Unexpected selected method contract.");
        Require(m.CustomAttributes.Count(Removed)==2&&m.CustomAttributes.Count(x=>x.AttributeType.FullName=="HarmonyLib.HarmonyPatch")==1&&m.CustomAttributes.Count(x=>x.AttributeType.FullName=="HarmonyLib.HarmonyPostfix")==1,"Expected exactly two selected Harmony attributes.");
        Require(Convert.ToHexString(m.CustomAttributes.Single(x=>x.AttributeType.FullName=="HarmonyLib.HarmonyPatch").GetBlob()).ToLowerInvariant()==TargetBlob,"Unexpected selected target attribute.");
        Require(Convert.ToHexString(m.CustomAttributes.Single(x=>x.AttributeType.FullName=="HarmonyLib.HarmonyPostfix").GetBlob())=="01000000","Unexpected postfix attribute.");
        var field=t.Fields.Single(x=>x.Name=="allowGldMods");Require(field.IsAssembly&&field.IsStatic&&field.FieldType.FullName=="System.Boolean","Dormant flag changed.");
        var writes=Types(a.MainModule).SelectMany(x=>x.Methods).Where(x=>x.HasBody).SelectMany(x=>x.Body.Instructions.Select((i,n)=>new{Method=x,Instruction=i,Index=n})).Where(x=>x.Instruction.OpCode==OpCodes.Stsfld&&x.Instruction.Operand is FieldReference f&&f.FullName==field.FullName).ToArray();
        Require(writes.Length==1&&writes[0].Method.DeclaringType==t&&writes[0].Method.Name==".cctor"&&writes[0].Index>0&&writes[0].Method.Body.Instructions[writes[0].Index-1].OpCode==OpCodes.Ldc_I4_0,"Dormant flag is no longer initialized exclusively false.");
        Require(PatchCount(a.MainModule)==91,"Unexpected patch inventory.");
    }
    public static int Main(string[] args)
    {
        try
        {
            Require(args.Length==3,"Usage: Adapt input.dll output.dll receipt.json");
            string input=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]),receipt=Path.GetFullPath(args[2]);
            Require(input!=output&&input!=receipt&&output!=receipt&&!File.Exists(output)&&!File.Exists(receipt),"Refusing replacement or colliding paths.");
            byte[] raw=File.ReadAllBytes(input);Require(Hash(raw)==Accepted,"Unknown PolyMod input hash; no output written.");
            using var a=AssemblyDefinition.ReadAssembly(new MemoryStream(raw),new ReaderParameters{ReadingMode=ReadingMode.Deferred,ReadSymbols=false,InMemory=true});
            ValidatePolicy(a);var bodies=Bodies(a.MainModule);var metadata=Metadata(a,true);var resources=Resources(a.MainModule);int count=PatchCount(a.MainModule);
            var originalStacks=Types(a.MainModule).SelectMany(t=>t.Methods).Where(x=>x.HasBody).ToDictionary(x=>x.FullName,x=>x.Body.MaxStackSize);
            var m=Selected(a.MainModule);var removed=m.CustomAttributes.Where(Removed).Select(Attr).ToArray();
            foreach(var x in m.CustomAttributes.Where(Removed).ToArray())m.CustomAttributes.Remove(x);
            using var ms=new MemoryStream();a.Write(ms,new WriterParameters{WriteSymbols=false});byte[] adapted=ms.ToArray();
            using var b=AssemblyDefinition.ReadAssembly(new MemoryStream(adapted),new ReaderParameters{ReadingMode=ReadingMode.Deferred,ReadSymbols=false,InMemory=true});
            var stackChanges=Types(b.MainModule).SelectMany(t=>t.Methods).Where(x=>x.HasBody&&x.Body.MaxStackSize!=originalStacks[x.FullName]).Select(x=>new{method=x.FullName,before=originalStacks[x.FullName],after=x.Body.MaxStackSize}).ToArray();
            Require(stackChanges.All(x=>x.after>=x.before),"Writer reduced an original max-stack allowance.");
            Equal(bodies,Bodies(b.MainModule),"Method IL semantic fingerprint");Equal(metadata,Metadata(b,false),"Metadata outside approved two attributes");Equal(resources,Resources(b.MainModule),"Assembly resources");
            Require(Selected(b.MainModule).CustomAttributes.All(x=>!Removed(x))&&PatchCount(b.MainModule)==count-1,"Adapted patch inventory mismatch.");
            var result=new{passed=true,inputSha256=Accepted,outputSha256=Hash(adapted),assembly=a.Name.FullName,removedAttributes=removed,
                methods=bodies.Count,methodInstructionsLocalsAndExceptionRegionsUnchanged=true,writerMaxStackIncreases=stackChanges,methodSemanticSha256=Digest(bodies),metadataExceptTwoAttributesUnchanged=true,metadataSha256=Digest(metadata),resourcesUnchanged=true,resources,
                harmonyTargetsBefore=count,harmonyTargetsAfter=count-1,clientInitUnchanged=true,dormantServerGldUnsupported=true,nativeExecution="NOT RUN"};
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
            // FileMode.CreateNew prevents replacing a file created after the preflight.
            using(var f=new FileStream(output,FileMode.CreateNew,FileAccess.Write))f.Write(adapted);
            using(var f=new FileStream(receipt,FileMode.CreateNew,FileAccess.Write)){byte[] json=Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true})+"\n");f.Write(json);}
            Console.WriteLine(JsonSerializer.Serialize(result));return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}
    }
}
