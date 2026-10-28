using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimScentReworked
{
    public class Pawn_ScentTracker : ThingComp
    {
        private static PawnCapacityDef SmellCapacityDef;
        private static StatDef SmellStatDef;
        private static HashSet<ThingDef> ScentThingDefs;
        private static HashSet<HediffDef> ScentHediffDefs;
        private static HashSet<ThoughtDef> ScentThoughtDefs;
        private static bool cacheBuilt;

        private static void EnsureCacheBuilt()
        {
            if (cacheBuilt) return;
            SmellCapacityDef = DefDatabase<PawnCapacityDef>.GetNamedSilentFail("RimScent_Smell");
            SmellStatDef = DefDatabase<StatDef>.GetNamedSilentFail("RimScent_SmellSensitivity");
            ScentThingDefs = new HashSet<ThingDef>();
            foreach (ThingDef d in DefDatabase<ThingDef>.AllDefs)
            {
                var ext = d.GetModExtension<ModExtension_Scent>();
                if (ext?.thought != null) ScentThingDefs.Add(d);
            }
            ScentHediffDefs = new HashSet<HediffDef>();
            foreach (HediffDef d in DefDatabase<HediffDef>.AllDefs)
            {
                var ext = d.GetModExtension<ModExtension_Scent>();
                if (ext?.thought != null) ScentHediffDefs.Add(d);
            }
            ScentThoughtDefs = new HashSet<ThoughtDef>();
            foreach (Def d in DefDatabase<ThingDef>.AllDefs.Cast<Def>()
                .Concat(DefDatabase<HediffDef>.AllDefs)
                .Concat(DefDatabase<WeatherDef>.AllDefs)
                .Concat(DefDatabase<GameConditionDef>.AllDefs))
            {
                var ext = d.GetModExtension<ModExtension_Scent>();
                if (ext?.thought != null) ScentThoughtDefs.Add(ext.thought);
            }
            cacheBuilt = true;
        }
        private int scentTickOffset;
        private ThoughtDef activeThought;
        private Pawn Pawn => parent as Pawn;

        public override void CompTick()
        {
            int interval = RimScentReworkedMod.Settings?.scentTickInterval ?? 500;
            if (interval <= 0) interval = 500;
            if (scentTickOffset == 0)
                scentTickOffset = Rand.Range(0, interval);
            if ((Find.TickManager.TicksGame + scentTickOffset) % interval != 0)
                return;
            Pawn pawn = Pawn;
            if (pawn == null || !pawn.Spawned || pawn.needs?.mood == null || pawn.IsAnimal) return;
            if (!PawnAllowedToSmell(pawn))
            {
                ClearThought(pawn);
                return;
            }
            UpdateScent(pawn);
        }

        private void UpdateScent(Pawn pawn)
        {
            EnsureCacheBuilt();
            float capacityFactor = SmellCapacityDef != null ? pawn.health.capacities.GetLevel(SmellCapacityDef) : 1f;
            float statFactor = SmellStatDef != null ? pawn.GetStatValue(SmellStatDef) : 1f;
            float smellFactor = capacityFactor * statFactor;
            if (smellFactor <= 0f)
            {
                ClearThought(pawn);
                return;
            }
            var storyTraits = pawn.story?.traits?.allTraits?.Where(t => !t.Suppressed).ToList();
            bool pawnHasDysosmicTrait = false;
            if (storyTraits != null)
            {
                foreach (Trait t in storyTraits)
                {
                    if (t.def.GetModExtension<ModExtension_Dysosmic>() != null)
                    {
                        pawnHasDysosmicTrait = true;
                        break;
                    }
                }
            }
            bool pawnHasDysosmicGene = false;
            bool pawnHasAnosmicGene = false;
            HashSet<string> pawnTraitNames = null;
            HashSet<string> pawnGeneNames = null;
            if (pawn.genes != null)
            {
                foreach (Gene g in pawn.genes.GenesListForReading)
                {
                    if (!g.Active) continue;
                    string name = g.def.defName;
                    if (!pawnHasDysosmicGene && (name.StartsWith("Dysosmic") || name.Contains("Dysosmic")))
                        pawnHasDysosmicGene = true;
                    if (!pawnHasAnosmicGene && (name.StartsWith("Anosmic") || name.Contains("Anosmic")))
                        pawnHasAnosmicGene = true;
                }
            }
            if (storyTraits != null)
            {
                pawnTraitNames = new HashSet<string>();
                foreach (Trait t in storyTraits)
                    pawnTraitNames.Add(t.def.defName);
            }
            if (pawn.genes != null)
            {
                pawnGeneNames = new HashSet<string>();
                foreach (Gene g in pawn.genes.GenesListForReading)
                    if (g.Active) pawnGeneNames.Add(g.def.defName);
            }
            Room pawnRoom = pawn.GetRoom();
            bool pawnOutdoors = pawnRoom == null || pawnRoom.PsychologicallyOutdoors;
            var scentsToApply = new List<ThoughtDef>();
            var scentDysosmicStatus = new Dictionary<ThoughtDef, bool>();
            void ConsiderScent(ModExtension_Scent ext)
            {
                if (ext?.thought == null) return;
                bool anosmic = pawnHasAnosmicGene || ScentIsAnosmic(pawnTraitNames, pawnGeneNames, ext, storyTraits);
                if (anosmic) return;
                bool dysosmic = pawnHasDysosmicTrait || pawnHasDysosmicGene || ScentIsDysosmic(pawnTraitNames, pawnGeneNames, ext, storyTraits);
                scentsToApply.Add(ext.thought);
                scentDysosmicStatus[ext.thought] = scentDysosmicStatus.TryGetValue(ext.thought, out bool existing) ? existing || dysosmic : dysosmic;
            }
            int radius = RimScentReworkedMod.Settings?.scentRadius ?? 8;
            bool homeOnly = RimScentReworkedMod.Settings?.homeOnly ?? false;
            Area homeArea = homeOnly ? pawn.Map?.areaManager?.Home : null;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(pawn.Position, radius, true))
            {
                if (!cell.InBounds(pawn.Map)) continue;
                if (homeOnly && homeArea != null && !homeArea[cell]) continue;
                if (!GenSight.LineOfSight(pawn.Position, cell, pawn.Map, true)) continue;
                Room cellRoom = cell.GetRoom(pawn.Map);
                bool cellOutdoors = cellRoom == null || cellRoom.PsychologicallyOutdoors;
                if (pawnOutdoors)
                    if (!cellOutdoors) continue;
                else if (cellRoom != pawnRoom)
                    continue;
                List<Thing> things = pawn.Map.thingGrid.ThingsListAtFast(cell);
                for (int i = 0, count = things.Count; i < count; i++)
                {
                    Thing thing = things[i];
                    if (thing is Pawn otherPawn && otherPawn != pawn)
                    {
                        HediffSet hediffs = otherPawn.health?.hediffSet;
                        if (hediffs == null) continue;
                        List<Hediff> allHediffs = hediffs.hediffs;
                        for (int h = 0, hCount = allHediffs.Count; h < hCount; h++)
                        {
                            Hediff hediff = allHediffs[h];
                            if (!ScentHediffDefs.Contains(hediff.def)) continue;
                            ModExtension_Scent ext = hediff.def.GetModExtension<ModExtension_Scent>();
                            if (ext?.thought == null) continue;
                            ConsiderScent(ext);
                        }
                        continue;
                    }
                    if (!ScentThingDefs.Contains(thing.def)) continue;
                    CompRefuelable refuelable = thing.TryGetComp<CompRefuelable>();
                    if (refuelable != null && !refuelable.HasFuel) continue;
                    CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
                    if (power != null && !power.PowerOn) continue;
                    ModExtension_Scent thingExt = thing.def.GetModExtension<ModExtension_Scent>();
                    if (thingExt?.thought == null) continue;
                    if (thingExt.onlyFresh && thing.TryGetComp<CompRottable>() is CompRottable rottable && rottable.Stage != RotStage.Fresh) continue;
                    if (thingExt.onlyMatureHealthyPlant && (thing is not Plant plant || plant.Growth < 0.99f || plant.Blighted || plant.LeaflessNow)) continue;
                    ConsiderScent(thingExt);
                }
            }

            foreach (GameCondition condition in pawn.Map.gameConditionManager.ActiveConditions)
            {
                var ext = condition.def.GetModExtension<ModExtension_Scent>();
                ConsiderScent(ext);
            }
            WeatherDef weather = pawn.Map.weatherManager.curWeather;
            if (weather != null)
            {
                var ext = weather.GetModExtension<ModExtension_Scent>();
                ConsiderScent(ext);
            }
            if (scentsToApply.Count == 0)
            {
                ClearThought(pawn);
                return;
            }
            var counts = new Dictionary<ThoughtDef, int>();
            foreach (ThoughtDef thought in scentsToApply)
            {
                counts.TryGetValue(thought, out int count);
                counts[thought] = count + 1;
            }
            bool uncapped = RimScentReworkedMod.Settings?.uncappedScents ?? false;
            if (!uncapped)
            {
                ThoughtDef winner = null;
                float winnerMagnitude = -1f;
                float winnerMood = float.PositiveInfinity;
                foreach (var pair in counts)
                {
                    float magnitude = ThoughtMagnitude(pawn, pair.Key, pair.Value);
                    bool inverted = scentDysosmicStatus.TryGetValue(pair.Key, out bool dysosmic) && dysosmic;
                    float mood = pair.Key.stages[0].baseMoodEffect * (inverted ? -1f : 1f);
                    if (magnitude > winnerMagnitude || (magnitude == winnerMagnitude && mood < 0f && winnerMood >= 0f))
                    {
                        winner = pair.Key;
                        winnerMagnitude = magnitude;
                        winnerMood = mood;
                    }
                }
                int count = counts[winner];
                counts.Clear();
                counts[winner] = count;
                activeThought = winner;
            }
            else activeThought = null;

            var memories = pawn.needs.mood.thoughts.memories;
            for (int i = memories.Memories.Count - 1; i >= 0; i--)
            {
                Thought_Memory memory = memories.Memories[i];
                if (ScentThoughtDefs.Contains(memory.def) && !counts.ContainsKey(memory.def))
                    memories.RemoveMemory(memory);
            }
            foreach (var pair in counts)
            {
                bool dysosmic = scentDysosmicStatus.TryGetValue(pair.Key, out bool dys) && dys;
                AddMemory(pawn, pair.Key, pair.Value, smellFactor, dysosmic, false);
                RemoveExcessMemory(pawn, pair.Key, pair.Value);
            }
        }

        private static bool ScentIsDysosmic(HashSet<string> traitNames, HashSet<string> geneNames, ModExtension_Scent ext, List<Trait> traits = null)
        {
            if (ext == null) return false;
            if (ext.dysosmicTraits != null && traitNames != null)
                for (int i = 0; i < ext.dysosmicTraits.Count; i++)
                    if (traitNames.Contains(ext.dysosmicTraits[i]))
                        return true;
            if (MatchesTraitRequirements(traits, ext.dysosmicTraitDegrees))
                return true;
            if (ext.dysosmicGenes != null && geneNames != null)
                for (int i = 0; i < ext.dysosmicGenes.Count; i++)
                    if (geneNames.Contains(ext.dysosmicGenes[i]))
                        return true;
            return false;
        }

        private static bool ScentIsAnosmic(HashSet<string> traitNames, HashSet<string> geneNames,
            ModExtension_Scent ext, List<Trait> traits = null)
        {
            if (ext == null) return false;
            if (ext.anosmicTraits != null && traitNames != null)
                for (int i = 0; i < ext.anosmicTraits.Count; i++)
                    if (traitNames.Contains(ext.anosmicTraits[i]))
                        return true;
            if (MatchesTraitRequirements(traits, ext.anosmicTraitDegrees))
                return true;
            if (ext.anosmicGenes != null && geneNames != null)
                for (int i = 0; i < ext.anosmicGenes.Count; i++)
                    if (geneNames.Contains(ext.anosmicGenes[i]))
                        return true;
            return false;
        }

        private static bool MatchesTraitRequirements(List<Trait> traits, List<TraitRequirement> requirements)
        {
            if (traits == null || requirements == null) return false;
            foreach (TraitRequirement requirement in requirements)
            {
                if (requirement?.def == null) continue;
                foreach (Trait trait in traits)
                    if (trait != null && !trait.Suppressed && requirement.Matches(trait))
                        return true;
            }
            return false;
        }

        private void ClearThought(Pawn pawn)
        {
            EnsureCacheBuilt();
            var memories = pawn.needs.mood.thoughts.memories;
            for (int i = memories.Memories.Count - 1; i >= 0; i--)
            {
                Thought_Memory memory = memories.Memories[i];
                if (memory.def == activeThought || ScentThoughtDefs.Contains(memory.def))
                    memories.RemoveMemory(memory);
            }
            activeThought = null;
        }

        private void AddMemory(Pawn pawn, ThoughtDef def, int desired, float smellFactor, bool dysosmic, bool anosmic)
        {
            if (anosmic)
            {
                pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(def);
                return;
            }
            bool allowStacking = RimScentReworkedMod.Settings?.allowMoodStacking ?? true;
            int stackLimit = allowStacking ? (def.stackLimit > 0 ? def.stackLimit : 1) : 1;
            int target = Mathf.Min(desired, stackLimit);
            float baseMood = def.stages[0].baseMoodEffect;
            float effectiveMood = baseMood * smellFactor * (dysosmic ? -1f : 1f);
            int offset = Mathf.RoundToInt(effectiveMood - baseMood);
            int existing = 0;
            foreach (Thought_Memory memory in pawn.needs.mood.thoughts.memories.Memories)
            {
                if (memory.def != def) continue;
                memory.moodOffset = offset;
                memory.age = 0;
                existing++;
            }
            for (int i = existing; i < target; i++)
            {
                Thought_Memory memory = (Thought_Memory)ThoughtMaker.MakeThought(def);
                memory.moodOffset = offset;
                pawn.needs.mood.thoughts.memories.TryGainMemory(memory);
            }
        }

        private void RemoveExcessMemory(Pawn pawn, ThoughtDef def, int desired)
        {
            bool allowStacking = RimScentReworkedMod.Settings?.allowMoodStacking ?? true;
            int stackLimit = allowStacking ? (def.stackLimit > 0 ? def.stackLimit : 1) : 1;
            int target = Mathf.Min(desired, stackLimit);
            List<Thought_Memory> memories = pawn.needs.mood.thoughts.memories.Memories.Where(m => m.def == def).ToList();
            int excess = memories.Count - target;
            for (int i = 0; i < excess; i++)
                pawn.needs.mood.thoughts.memories.RemoveMemory(memories[i]);
        }

        private int CountThought(Pawn pawn, ThoughtDef def)
        {
            return pawn.needs.mood.thoughts.memories.Memories.Count(m => m.def == def);
        }

        private bool PawnAllowedToSmell(Pawn pawn)
        {
            var settings = RimScentReworkedMod.Settings;
            if (settings == null) return true;
            if (pawn.IsSlave) return settings.slavesCanSmell;
            if (pawn.IsPrisoner) return settings.prisonersCanSmell;
            if (pawn.IsColonist) return settings.colonistsCanSmell;
            if (pawn.Faction != null)
                return pawn.Faction.HostileTo(Faction.OfPlayer) ? settings.enemyFactionsCanSmell : settings.friendlyFactionsCanSmell;
            return true;
        }

        private float ThoughtMagnitude(Pawn pawn, ThoughtDef def, int sourceCount)
        {
            bool allowStacking = RimScentReworkedMod.Settings?.allowMoodStacking ?? true;
            int stackLimit = allowStacking ? (def.stackLimit > 0 ? def.stackLimit : 1) : 1;
            int effective = Mathf.Min(sourceCount, stackLimit);
            return Mathf.Abs(def.stages[0].baseMoodEffect) * effective;
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref scentTickOffset, "scentTickOffset", 0);
            Scribe_Defs.Look(ref activeThought, "activeThought");
        }
    }
}