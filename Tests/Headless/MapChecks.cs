using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using RimWorld;
using Verse;

public sealed class RimScentMapChecks : GameComponent
{
    private bool ran;
    private bool reloadVerification;
    private int savedPawnId;
    private bool loaded;
    private readonly List<object> results = new();
    private Map map;
    private IntVec3 center;
    private Pawn pawn;
    private readonly List<Thing> sources = new();
    public RimScentMapChecks(Game game) { }
    public override void ExposeData()
    {
        Scribe_Values.Look(ref reloadVerification, "rimscentReloadVerification");
        Scribe_Values.Look(ref savedPawnId, "rimscentTestPawnId");
    }
    public override void LoadedGame() { loaded = true; }
    private void Check(string name, Func<bool> test)
    {
        try
        {
            bool passed = test();
            results.Add(new { name, passed });
            Log.Message("RIMSCENT_MAP " + (passed ? "PASS " : "FAIL ") + name);
        }
        catch (Exception e)
        {
            results.Add(new { name, passed = false, error = e.ToString() });
            Log.Message("RIMSCENT_MAP FAIL " + name + " " + e);
        }
    }
    private void Evaluate()
    {
        typeof(RimScentReworked.Pawn_ScentTracker).GetMethod("UpdateScent", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(pawn.GetComp<RimScentReworked.Pawn_ScentTracker>(), new object[] { pawn });
    }
    private List<Thought_Memory> Scents => pawn.needs.mood.thoughts.memories.Memories.Where(m => m.def.defName.StartsWith("RimScent_")).ToList();
    private float Mood(string def) => Scents.Where(m => m.def.defName == def).Sum(m => m.MoodOffset());
    private void Trait(string name) => pawn.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed(name)));
    private Thing Source(string name, int x = 1)
    {
        Thing source = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));
        GenSpawn.Spawn(source, center + new IntVec3(x, 0, 0), map);
        sources.Add(source);
        return source;
    }
    private void Reset()
    {
        foreach (Thing thing in sources) if (!thing.Destroyed) thing.Destroy();
        sources.Clear();
        if (pawn != null && !pawn.Destroyed) pawn.Destroy();
        RimScentReworked.RimScentReworkedMod.Settings.uncappedScents = false;
        RimScentReworked.RimScentReworkedMod.Settings.allowMoodStacking = false;
        RimScentReworked.RimScentReworkedMod.Settings.homeOnly = false;
        RimScentReworked.RimScentReworkedMod.Settings.scentRadius = 8;
        RimScentReworked.RimScentReworkedMod.Settings.scentTickInterval = 500;
        RimScentReworked.RimScentReworkedMod.Settings.colonistsCanSmell = true;
        RimScentReworked.RimScentReworkedMod.Settings.slavesCanSmell = true;
        pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        pawn.story.traits.allTraits.Clear();
        foreach (var gene in pawn.genes.GenesListForReading.ToList()) pawn.genes.RemoveGene(gene);
        GenSpawn.Spawn(pawn, center, map);
    }
    public override void GameComponentUpdate()
    {
        if (ran || Current.ProgramState != ProgramState.Playing || Find.Maps.Count == 0 || LongEventHandler.AnyEventNowOrWaiting) return;
        ran = true;
        Find.TickManager.Pause();
        try
        {
            map = Find.CurrentMap;
            if (loaded && reloadVerification)
            {
                pawn = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == savedPawnId);
                Check("saved pawn and scent memory restored", () => pawn != null && Mood("RimScent_BloodScent") == -2);
                Check("restored tracker updates without duplication", () => { Evaluate(); return Scents.Count(m => m.def.defName == "RimScent_BloodScent") == 1; });
                Check("restored tracker clears removed source", () =>
                {
                    foreach (Thing t in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("Filth_Blood")).ToList()) t.Destroy();
                    Evaluate(); return Scents.Count == 0;
                });
                Finish("reload-checks.json");
                return;
            }
            center = map.Center;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 14, true))
            {
                if (!cell.InBounds(map)) continue;
                foreach (Thing t in cell.GetThingList(map).ToList())
                    if (t is Pawn) t.DeSpawn(); else t.Destroy();
                map.roofGrid.SetRoof(cell, null);
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            }
            map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            map.weatherManager.TransitionTo(WeatherDefOf.Clear);
            Check("nearby blood creates negative scent", () => { Reset(); Source("Filth_Blood"); Evaluate(); return Mood("RimScent_BloodScent") == -2; });
            Check("same source remains single memory", () => { Evaluate(); Evaluate(); return Scents.Count == 1; });
            Check("removed source clears scent", () => { sources[0].Destroy(); Evaluate(); return Scents.Count == 0; });
            Check("source outside radius has no effect", () => { Reset(); Source("Filth_Blood", 10); Evaluate(); return Scents.Count == 0; });
            Check("strongest nearby smell wins", () => { Reset(); Source("Filth_Blood"); Source("Filth_Vomit", 2); Evaluate(); return Mood("RimScent_VomitScent") == -4 && Scents.Count == 1; });
            Check("weaker smell takes over when stronger source removed", () => { sources[1].Destroy(); Evaluate(); return Mood("RimScent_BloodScent") == -2 && Scents.Count == 1; });
            Check("keen nose doubles smell", () => { Reset(); Trait("RimScent_KeenNose"); Source("Filth_Blood"); Evaluate(); return Mood("RimScent_BloodScent") == -4; });
            Check("dysosmic trait reverses smell", () => { Reset(); Trait("RimScent_Dysosmic"); Source("Filth_Blood"); Evaluate(); return Mood("RimScent_BloodScent") == 2; });
            Check("keen dysosmic nose reverses full doubled effect", () => { Reset(); Trait("RimScent_Dysosmic"); Trait("RimScent_KeenNose"); Source("Filth_Blood"); Evaluate(); return Mood("RimScent_BloodScent") == 4; });
            Check("anosmic trait blocks smells", () => { Reset(); Trait("RimScent_Anosmic"); Source("Filth_Blood"); Evaluate(); return Scents.Count == 0; });
            Check("missing nose blocks smells", () => { Reset(); var nose = pawn.health.hediffSet.GetNotMissingParts().First(p => p.def.defName == "Nose"); pawn.health.AddHediff(HediffDefOf.MissingBodyPart, nose); Source("Filth_Blood"); Evaluate(); return Scents.Count == 0; });
            Check("cloth mask halves scent effect", () => { Reset(); var mask = (Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_ClothMask"), ThingDefOf.Cloth); pawn.apparel.Wear(mask); Source("Filth_Blood"); Evaluate(); return Mood("RimScent_BloodScent") == -1; });
            Check("changed sensitivity updates existing smell", () => { Reset(); Source("Filth_Blood"); Evaluate(); Trait("RimScent_KeenNose"); Evaluate(); return Mood("RimScent_BloodScent") == -4; });
            Check("uncapped mode allows different scents", () => { Reset(); RimScentReworked.RimScentReworkedMod.Settings.uncappedScents = true; Source("Filth_Blood"); Source("Filth_Vomit", 2); Evaluate(); return Scents.Count == 2; });
            Check("uncapped removal clears both scents", () => { foreach (Thing s in sources) s.Destroy(); Evaluate(); return Scents.Count == 0; });
            Check("uncapped anosmia clears previous scents", () => { Reset(); RimScentReworked.RimScentReworkedMod.Settings.uncappedScents = true; Source("Filth_Blood"); Source("Filth_Vomit", 2); Evaluate(); Trait("RimScent_Anosmic"); Evaluate(); return Scents.Count == 0; });
            Check("source-specific anosmia grants immunity", () =>
            {
                Reset(); Trait("Kind"); var def = DefDatabase<ThingDef>.GetNamed("Filth_Blood"); var ext = def.GetModExtension<RimScentReworked.ModExtension_Scent>(); var before = ext.anosmicTraits;
                try { ext.anosmicTraits = new List<string> { "Kind" }; Source("Filth_Blood"); Evaluate(); return Scents.All(m => m.MoodOffset() == 0); }
                finally { ext.anosmicTraits = before; }
            });
            Check("stack counts decrease with sources", () => { Reset(); RimScentReworked.RimScentReworkedMod.Settings.allowMoodStacking = true; Source("Filth_Blood"); Source("Filth_Blood", 2); Evaluate(); bool two = Scents.Count == 2; sources[1].Destroy(); Evaluate(); return two && Scents.Count == 1; });
            Check("uncapped to capped removes obsolete smells", () => { Reset(); RimScentReworked.RimScentReworkedMod.Settings.uncappedScents = true; Source("Filth_Blood"); Source("Filth_Vomit", 2); Evaluate(); RimScentReworked.RimScentReworkedMod.Settings.uncappedScents = false; Evaluate(); return Scents.Count == 1 && Mood("RimScent_VomitScent") == -4; });
            Check("ongoing source refreshes memory before expiration", () => { Reset(); Source("Filth_Blood"); Evaluate(); Scents[0].age = 5000; Evaluate(); return Scents.Count == 1 && Scents[0].age == 0; });
            Check("home-only excludes source outside home area", () => { Reset(); RimScentReworked.RimScentReworkedMod.Settings.homeOnly = true; Source("Filth_Blood"); map.areaManager.Home[center + new IntVec3(1, 0, 0)] = false; Evaluate(); return Scents.Count == 0; });
            Check("home-only includes source in home area", () => { map.areaManager.Home[center + new IntVec3(1, 0, 0)] = true; Evaluate(); return Mood("RimScent_BloodScent") == -2; });
            Check("scheduled comp tick evaluates scent", () => { Reset(); Source("Filth_Blood"); var tracker = pawn.GetComp<RimScentReworked.Pawn_ScentTracker>(); int offset = 500 - Find.TickManager.TicksGame % 500; typeof(RimScentReworked.Pawn_ScentTracker).GetField("scentTickOffset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracker, offset); tracker.CompTick(); return Mood("RimScent_BloodScent") == -2; });
            Check("disabled colonist eligibility clears scent", () => { Reset(); Source("Filth_Blood"); Evaluate(); var settings = RimScentReworked.RimScentReworkedMod.Settings; settings.colonistsCanSmell = false; try { var tracker = pawn.GetComp<RimScentReworked.Pawn_ScentTracker>(); typeof(RimScentReworked.Pawn_ScentTracker).GetField("scentTickOffset", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracker, 500 - Find.TickManager.TicksGame % 500); tracker.CompTick(); return Scents.Count == 0; } finally { settings.colonistsCanSmell = true; } });
            Check("slave eligibility can be disabled independently of colonists", () => { Reset(); pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave); RimScentReworked.RimScentReworkedMod.Settings.slavesCanSmell = false; try { return pawn.IsSlave && !(bool)typeof(RimScentReworked.Pawn_ScentTracker).GetMethod("PawnAllowedToSmell", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pawn.GetComp<RimScentReworked.Pawn_ScentTracker>(), new object[]{pawn}); } finally { RimScentReworked.RimScentReworkedMod.Settings.slavesCanSmell = true; } });
            Check("slave eligibility can be enabled while colonists disabled", () => { Reset(); pawn.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Slave); RimScentReworked.RimScentReworkedMod.Settings.colonistsCanSmell = false; try { return pawn.IsSlave && (bool)typeof(RimScentReworked.Pawn_ScentTracker).GetMethod("PawnAllowedToSmell", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pawn.GetComp<RimScentReworked.Pawn_ScentTracker>(), new object[]{pawn}); } finally { RimScentReworked.RimScentReworkedMod.Settings.colonistsCanSmell = true; } });
            Check("overridden dysosmic gene does not reverse scent", () => { Reset(); var gene = pawn.genes.AddGene(DefDatabase<GeneDef>.GetNamed("RimScent_Gene_Dysosmic"), false); var replacement = pawn.genes.AddGene(DefDatabase<GeneDef>.GetNamed("RimScent_Gene_KeenNose"), true); gene.OverrideBy(replacement); Source("Filth_Blood"); Evaluate(); return !gene.Active && Mood("RimScent_BloodScent") == -4; });
            Check("overridden anosmic gene does not block scent", () => { Reset(); var gene = pawn.genes.AddGene(DefDatabase<GeneDef>.GetNamed("RimScent_Gene_Anosmic"), false); var replacement = pawn.genes.AddGene(DefDatabase<GeneDef>.GetNamed("RimScent_Gene_KeenNose"), true); gene.OverrideBy(replacement); Source("Filth_Blood"); Evaluate(); return !gene.Active && Mood("RimScent_BloodScent") == -4; });
            Check("rain scent disappears when weather clears", () => { Reset(); try { map.weatherManager.TransitionTo(DefDatabase<WeatherDef>.GetNamed("Rain")); Evaluate(); bool rain = Mood("RimScent_PetrichorScent") > 0; map.weatherManager.TransitionTo(WeatherDefOf.Clear); Evaluate(); return rain && Scents.Count == 0; } finally { map.weatherManager.TransitionTo(WeatherDefOf.Clear); } });
            Check("wall blocks line of sight to scent", () => { Reset(); Source("Filth_Blood", 2); var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite); GenSpawn.Spawn(wall, center + new IntVec3(1, 0, 0), map); try { Evaluate(); return Scents.Count == 0; } finally { wall.Destroy(); map.regionAndRoomUpdater.RebuildAllRegionsAndRooms(); } });
            if (DefDatabase<ThingDef>.GetNamedSilentFail("AC_Lavender") != null)
            {
                Check("Alpha Crafts harvested lavender emits floral scent", () => { Reset(); Source("AC_Lavender"); Evaluate(); return Mood("RimScent_FloweryScent") == 2; });
                Check("Alpha Crafts perfume wearer emits scent to neighbor", () => { Reset(); var other = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer); GenSpawn.Spawn(other, center + new IntVec3(1,0,0), map); sources.Add(other); other.health.AddHediff(DefDatabase<HediffDef>.GetNamed("AC_PerfumeHediff")); Evaluate(); return Mood("RimScent_FloweryScent") == 2; });
            }
            if (DefDatabase<ThingDef>.GetNamedSilentFail("lim_Filth_LoveJuice") != null)
                Check("Lovin Is Messy filth emits added scent", () => { Reset(); Source("lim_Filth_LoveJuice"); Evaluate(); return pawn.needs.mood.thoughts.memories.Memories.Any(m=>m.def.defName=="RimScentContinued_BodilyFluids" && m.MoodOffset()==-2); });
            if (DefDatabase<ThingDef>.GetNamedSilentFail("VBE_HotCoffee") != null) {
                Check("brewed coffee emits coffee aroma", () => { Reset(); Source("VBE_HotCoffee"); Evaluate(); return Mood("RimScent_CoffeeAroma") == 2; });
                Check("rotting coffee beans lose pleasant aroma", () => { Reset(); var beans=Source("VBE_RawCoffee"); Evaluate(); bool fresh=Mood("RimScent_CoffeeAroma")==2; var rot=beans.TryGetComp<CompRottable>(); rot.RotImmediately(); Evaluate(); return fresh && rot.Stage!=RotStage.Fresh && Scents.Count==0; });
            }
            if (DefDatabase<ThingDef>.GetNamedSilentFail("VBE_MasalaChai") != null)
                Check("masala chai emits tea aroma", () => { Reset(); Source("VBE_MasalaChai"); Evaluate(); return Mood("RimScent_TeaAroma")==1; });
            if (DefDatabase<ThingDef>.GetNamedSilentFail("VBE_MintLeaves") != null)
                Check("mint leaves emit mint aroma", () => { Reset(); Source("VBE_MintLeaves"); Evaluate(); return Mood("RimScent_MintAroma")==1; });
            Reset(); Source("Filth_Blood"); Evaluate();
            savedPawnId = pawn.thingIDNumber;
            reloadVerification = true;
            GameDataSaveLoader.SaveGame("rimscent-behavior");
            Check("test colony saved", () => File.Exists(GenFilePaths.FilePathForSavedGame("rimscent-behavior")));
            Finish("map-checks.json");
        }
        catch (Exception e) { Check("harness setup", () => throw e); Finish("map-checks.json"); }
    }
    private void Finish(string file)
    {
        File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, file), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Root.Shutdown();
    }
}
