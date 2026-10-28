using RimWorld;
using Verse;
using System.Collections.Generic;

namespace RimScentReworked
{
    public class ModExtension_Scent : DefModExtension
    {
        public ThoughtDef thought;
        public bool onlyFresh;
        public bool onlyMatureHealthyPlant;
        public List<string> dysosmicGenes;
        public List<string> dysosmicTraits;
        public List<TraitRequirement> dysosmicTraitDegrees;
        public List<string> anosmicGenes;
        public List<string> anosmicTraits;
        public List<TraitRequirement> anosmicTraitDegrees;
    }
}
