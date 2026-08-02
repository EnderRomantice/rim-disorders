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
        private int generatedPawnIndex;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref assigned, "assigned", false);
            Scribe_Values.Look(ref generatedPawnIndex, "generatedPawnIndex", 0);
        }

        public override void Notify_PawnGenerated(Pawn pawn, PawnGenerationContext context, bool redressed)
        {
            if (context != PawnGenerationContext.PlayerStarter || pawn == null || !pawn.RaceProps.Humanlike)
                return;

            // Make the scenario visible on the character selection page instead of
            // waiting until the map is generated. The final six are normalized again
            // below, since rerolling a slot can change candidate generation order.
            HediffDef previewDef = ExtremeDisorders[generatedPawnIndex % ExtremeDisorders.Length];
            generatedPawnIndex++;
            MentalDisorderUtility.StabilizeMind(pawn);
            pawn.health.AddHediff(previewDef);
        }

        public override void GenerateIntoMap(Map map)
        {
            if (assigned || Find.GameInitData == null)
                return;

            List<Pawn> pawns = Find.GameInitData.startingAndOptionalPawns
                .Where(p => p != null && p.RaceProps.Humanlike)
                .Take(Find.GameInitData.startingPawnCount)
                .ToList();
            assigned = AssignUnique(pawns, "before arrival");
        }

        public override void PostMapGenerate(Map map)
        {
            if (map == null)
                return;

            // Use the actual landed colonists as the authoritative source. This is a
            // deliberate second pass: other scenario parts and pawn-selection mods can
            // reorder or replace GameInitData entries during map generation.
            List<Pawn> landed = map.mapPawns.FreeColonistsSpawned
                .Where(p => p != null && p.Faction == Faction.OfPlayer)
                .Take(ExtremeDisorders.Length)
                .ToList();
            if (landed.Count == ExtremeDisorders.Length)
                assigned = AssignUnique(landed, "after landing");
        }

        private static bool AssignUnique(List<Pawn> pawns, string phase)
        {
            if (pawns.Count != ExtremeDisorders.Length)
            {
                Log.Error("[More Mental Disorders] Asylum Escapees expected six starting pawns "
                    + phase + ", but found " + pawns.Count + ".");
                return false;
            }

            List<HediffDef> shuffled = ExtremeDisorders.InRandomOrder().ToList();
            for (int i = 0; i < pawns.Count; i++)
            {
                MentalDisorderUtility.StabilizeMind(pawns[i]);
                pawns[i].health.AddHediff(shuffled[i]);
            }

            return pawns.SelectMany(p => p.Disorders()).Select(d => d.def).Distinct().Count()
                == ExtremeDisorders.Length;
        }
    }
}
