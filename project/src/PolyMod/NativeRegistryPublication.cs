using System;
using System.Linq;
using System.Reflection;

namespace Deception.PolyModAdapter
{
    // Keep the native parser's root object, publishing every section changed by
    // the staged catalogs. Capture all originals before the first write so the
    // registration caller can also roll back failures after publication.
    internal sealed class NativeRegistryPublication
    {
        static readonly string[] Sections={"techData","unitData","improvementData","tribeData"};
        readonly object Target;
        readonly PropertyInfo Indexer;
        readonly object[] Original,Replacement;
        internal NativeRegistryPublication(object target,object candidate)
        {
            if(target.GetType()!=candidate.GetType())throw new InvalidOperationException("Registry publication type mismatch.");
            Target=target;
            Indexer=target.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)
                .Single(p=>p.Name=="Item"&&p.GetIndexParameters().Length==1&&p.GetIndexParameters()[0].ParameterType==typeof(string));
            Original=new object[Sections.Length];Replacement=new object[Sections.Length];
            for(int i=0;i<Sections.Length;i++)
            {
                Original[i]=Indexer.GetValue(target,new object[]{Sections[i]})??throw new InvalidOperationException("Missing original registry section: "+Sections[i]);
                Replacement[i]=Indexer.GetValue(candidate,new object[]{Sections[i]})??throw new InvalidOperationException("Missing candidate registry section: "+Sections[i]);
            }
        }
        internal void Apply()
        {for(int i=0;i<Sections.Length;i++)Indexer.SetValue(Target,Replacement[i],new object[]{Sections[i]});}
        internal void Rollback()
        {for(int i=0;i<Sections.Length;i++)Indexer.SetValue(Target,Original[i],new object[]{Sections[i]});}
    }
}
