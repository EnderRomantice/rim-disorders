using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MoreMentalDisorders
{
    /// <summary>
    /// Gives the six final starting pawns the complete set of extreme disorders,
    /// one unique disorder per pawn. This runs before the arrival scen part.
    /// </summary>
    public sealed class ScenPart_AssignUniqueExtremeDisorders : ScenPart
    {
        private static readonly HediffDef[] ExtremeDisorders =
        {
            MMDDefOf.MMD_ParanoidDelusion,
            MMDDefOf.MMD_MajorDepression,
            MMDDefOf.MMD_Schizophrenia,
            MMDDefOf.MMD_Mania,
            MMDDefOf.MMD_Cotard,
            MMDDefOf.MMD_Hyperthymesia
        };

        private bool assigned;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref assigned, "assigned", false);
        }

        public override void GenerateIntoMap(Map map)
        {
            if (assigned || Find.GameInitData == null)
                return;

            List<Pawn> pawns = Find.GameInitData.startingAndOptionalPawns
                .Where(p => p != null && p.RaceProps.Humanlike)
                .Take(ExtremeDisorders.Length)
                .ToList();
            if (pawns.Count != ExtremeDisorders.Length)
            {
                Log.Error("[More Mental Disorders] Asylum Escapees expected six humanlike starting pawns, but found " + pawns.Count + ".");
                return;
            }

            List<HediffDef> shuffled = ExtremeDisorders.InRandomOrder().ToList();
            for (int i = 0; i < pawns.Count; i++)
            {
                // Pawn generation can add a random congenital disorder. The scenario
                // deliberately replaces it so all six extreme disorders occur once.
                MentalDisorderUtility.StabilizeMind(pawns[i]);
                pawns[i].health.AddHediff(shuffled[i]);
            }

            assigned = true;
        }
    }
}
