using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MoreMentalDisorders
{
    public class JobDriver_MindKill : JobDriver
    {
        private const int WarmupTicks = 120;
        private Effecter warmupEffecter;
        private Sustainer warmupSustainer;

        private Pawn Victim
        {
            get { return job.GetTarget(TargetIndex.A).Pawn; }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Victim == null || Victim.Dead || pawn.Downed
                || pawn.Map != Victim.Map
                || !GenSight.LineOfSight(pawn.Position, Victim.Position, pawn.Map));

            Toil warmup = new Toil();
            warmup.initAction = delegate
            {
                warmup.actor.pather.StopDead();
                warmup.actor.rotationTracker.FaceTarget(Victim);
                AbilityDef original = DefDatabase<AbilityDef>.GetNamedSilentFail("PsychicSlaughter");
                if (original == null) return;
                if (original.warmupMote != null)
                    MoteMaker.MakeAttachedOverlay(warmup.actor, original.warmupMote,
                        Vector3.zero, 1f, 2f);
                if (original.warmupEffecter != null)
                    warmupEffecter = original.warmupEffecter.SpawnMaintained(warmup.actor,
                        warmup.actor.Map);
                if (original.warmupSound != null)
                    warmupSustainer = original.warmupSound.TrySpawnSustainer(
                        SoundInfo.InMap(warmup.actor, MaintenanceType.PerTick));
            };
            warmup.tickAction = delegate
            {
                warmup.actor.rotationTracker.FaceTarget(Victim);
                if (warmupEffecter != null)
                    warmupEffecter.EffectTick(warmup.actor, Victim);
                if (warmupSustainer != null)
                    warmupSustainer.Maintain();
            };
            warmup.AddFinishAction(delegate
            {
                if (warmupEffecter != null)
                {
                    warmupEffecter.Cleanup();
                    warmupEffecter = null;
                }
                if (warmupSustainer != null)
                {
                    warmupSustainer.End();
                    warmupSustainer = null;
                }
            });
            warmup.defaultCompleteMode = ToilCompleteMode.Delay;
            warmup.defaultDuration = WarmupTicks;
            warmup.WithProgressBarToilDelay(TargetIndex.B);
            yield return warmup;

            Toil slaughter = new Toil();
            slaughter.initAction = delegate
            {
                Pawn victim = Victim;
                Hediff_MentalDisorder disorder = pawn.health.hediffSet.hediffs
                    .OfType<Hediff_MentalDisorder>()
                    .FirstOrDefault(h => h.def == MMDDefOf.MMD_ParanoidDelusion);
                if (victim == null || victim.Dead || disorder == null
                    || !disorder.IsValidMindKillTarget(victim)
                    || !GenSight.LineOfSight(pawn.Position, victim.Position, pawn.Map))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                victim.TakeDamage(new DamageInfo(DamageDefOf.Bomb, 99999f, 999f, instigator: pawn));
                MoteMaker.ThrowText(victim.DrawPos, victim.Map,
                    MMDLocalization.Pick("心灵宰杀", "Mind-kill"), Color.red);
            };
            slaughter.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return slaughter;
        }
    }
}
