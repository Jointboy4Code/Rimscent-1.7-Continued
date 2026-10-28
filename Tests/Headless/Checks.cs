using System; using System.IO; using System.Linq; using System.Collections.Generic; using System.Reflection; using System.Text.Json; using Verse; using RimWorld; using RimScentReworked;
[StaticConstructorOnStartup] public static class RimScentRuntimeChecks {
static List<object> results=new();
static void Check(string name,Func<bool> test) { try { bool ok=test();results.Add(new {name,passed=ok});Log.Message("RIMSCENT_TEST "+(ok?"PASS ":"FAIL ")+name); }catch(Exception e){results.Add(new {name,passed=false,error=e.ToString()});Log.Message("RIMSCENT_TEST FAIL "+name+" "+e);}}
static bool Eval(string method, ModExtension_Scent ext, HashSet<string> traits=null,HashSet<string> genes=null,List<Trait> degrees=null)=>(bool)typeof(Pawn_ScentTracker).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{traits,genes,ext,degrees});
static RimScentRuntimeChecks(){
Check("settings initialized",()=>RimScentReworkedMod.Settings!=null);
Check("new settings use balanced scent defaults",()=>!new RimScentReworkedSettings().uncappedScents && !new RimScentReworkedSettings().allowMoodStacking);
Check("human scent tracker attached once",()=>ThingDefOf.Human.comps.Count(c=>c.compClass==typeof(Pawn_ScentTracker))==1);
Check("smell capacity registered",()=>DefDatabase<PawnCapacityDef>.GetNamedSilentFail("RimScent_Smell")?.workerClass==typeof(PawnCapacityWorker_Smell));
Check("sensitivity stat registered",()=>DefDatabase<StatDef>.GetNamedSilentFail("RimScent_SmellSensitivity")!=null);
Check("blood scent patch resolved",()=>DefDatabase<ThingDef>.GetNamed("Filth_Blood").GetModExtension<ModExtension_Scent>()?.thought?.defName=="RimScent_BloodScent");
Check("all loaded scent extensions reference thoughts",()=>DefDatabase<ThingDef>.AllDefs.Where(d=>d.HasModExtension<ModExtension_Scent>()).All(d=>d.GetModExtension<ModExtension_Scent>().thought!=null));
Check("null extension grants no anosmia",()=>!Eval("ScentIsAnosmic",null));
Check("matching anosmic trait recognized",()=>Eval("ScentIsAnosmic",new ModExtension_Scent{anosmicTraits=new(){"Example"}},new(){"Example"}));
Check("unmatched anosmic trait rejected",()=>!Eval("ScentIsAnosmic",new ModExtension_Scent{anosmicTraits=new(){"Example"}},new(){"Other"}));
Check("matching dysosmic gene recognized",()=>Eval("ScentIsDysosmic",new ModExtension_Scent{dysosmicGenes=new(){"Example"}},null,new(){"Example"}));
Check("empty anosmic degree requirements grant no anosmia",()=>!Eval("ScentIsAnosmic",new ModExtension_Scent{anosmicTraitDegrees=new()}));
Check("empty dysosmic degree requirements grant no dysosmia",()=>!Eval("ScentIsDysosmic",new ModExtension_Scent{dysosmicTraitDegrees=new()}));
foreach(var mode in new[]{"Anosmic","Dysosmic"}) {
var neurotic=DefDatabase<TraitDef>.GetNamed("Neurotic");
ModExtension_Scent Ext(int? degree){ var e=new ModExtension_Scent();var req=new List<TraitRequirement>{new TraitRequirement{def=neurotic,degree=degree}};if(mode=="Anosmic")e.anosmicTraitDegrees=req;else e.dysosmicTraitDegrees=req;return e; }
bool Test(int? wanted,List<Trait> traits)=>Eval("ScentIs"+mode,Ext(wanted),null,null,traits);
Check(mode+" matching degree accepted",()=>Test(2,new(){new Trait(neurotic,2)}));
Check(mode+" wrong degree rejected",()=>!Test(2,new(){new Trait(neurotic,1)}));
Check(mode+" missing trait rejected",()=>!Test(2,new()));
Check(mode+" no pawn traits rejected",()=>!Test(2,null));
Check(mode+" unspecified degree accepts trait",()=>Test(null,new(){new Trait(neurotic,1)}));
Check(mode+" any matching requirement accepted",()=>Test(2,new(){new Trait(DefDatabase<TraitDef>.GetNamed("Kind")),new Trait(neurotic,2)}));
}
File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"rimscent-checks.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"rimscent-coverage.json"),JsonSerializer.Serialize(new {
 loadedMods=LoadedModManager.RunningMods.Select(m=>m.PackageIdPlayerFacing).ToArray(),
 humanlikeWithoutTracker=DefDatabase<ThingDef>.AllDefs.Where(d=>typeof(Pawn).IsAssignableFrom(d.thingClass) && d.race?.Humanlike==true && !(d.comps?.Any(c=>c.compClass==typeof(Pawn_ScentTracker))??false)).Select(d=>d.defName).ToArray(),
 thingSources=DefDatabase<ThingDef>.AllDefs.Count(d=>d.GetModExtension<ModExtension_Scent>()?.thought!=null),
 hediffSources=DefDatabase<HediffDef>.AllDefs.Count(d=>d.GetModExtension<ModExtension_Scent>()?.thought!=null)
},new JsonSerializerOptions{WriteIndented=true}));
}}
