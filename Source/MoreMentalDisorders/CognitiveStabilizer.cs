using RimWorld;
using Verse;

namespace MoreMentalDisorders
{
    public class Hediff_CognitiveStabilizer : Hediff_Implant
    {
        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            MentalDisorderUtility.StabilizeMind(pawn);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn != null && pawn.IsHashIntervalTick(250, delta))
                MentalDisorderUtility.StabilizeMind(pawn);
        }
    }

    public class ThoughtWorker_CognitiveStability : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn pawn)
        {
            return MentalDisorderUtility.HasCognitiveStabilizer(pawn)
                ? ThoughtState.ActiveDefault
                : ThoughtState.Inactive;
        }
    }
}
