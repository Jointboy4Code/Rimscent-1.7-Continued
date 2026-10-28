using Verse;

namespace RimScentReworked
{
    public class RimScentReworkedSettings : ModSettings
    {
        public int scentTickInterval = 500;
        public int scentRadius = 8;
        public bool uncappedScents = false;
        public bool colonistsCanSmell = true;
        public bool prisonersCanSmell = true;
        public bool slavesCanSmell = true;
        public bool friendlyFactionsCanSmell = false;
        public bool enemyFactionsCanSmell = false;
        public bool homeOnly = false;
        public bool allowMoodStacking = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref scentTickInterval, "scentTickInterval", 500);
            Scribe_Values.Look(ref scentRadius, "scentRadius", 8);
            Scribe_Values.Look(ref uncappedScents, "uncappedScents", false);
            Scribe_Values.Look(ref colonistsCanSmell, "colonistsCanSmell", true);
            Scribe_Values.Look(ref prisonersCanSmell, "prisonersCanSmell", true);
            Scribe_Values.Look(ref slavesCanSmell, "slavesCanSmell", true);
            Scribe_Values.Look(ref friendlyFactionsCanSmell, "friendlyFactionsCanSmell", false);
            Scribe_Values.Look(ref enemyFactionsCanSmell, "enemyFactionsCanSmell", false);
            Scribe_Values.Look(ref homeOnly, "homeOnly", false);
            Scribe_Values.Look(ref allowMoodStacking, "allowMoodStacking", false);
            base.ExposeData();
        }
    }
}
