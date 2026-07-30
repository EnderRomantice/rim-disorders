using System.Linq;
using RimWorld;
using Verse;

namespace MoreMentalDisorders
{
    public class Thought_SocialCrazy : Thought_MemorySocial { }
    public class Thought_SocialParanoid : Thought_MemorySocial { }
    public class Thought_SocialDepression : Thought_MemorySocial { }
    public class Thought_SocialPresident : Thought_MemorySocial { }
    public class Thought_SocialNarcissistic : Thought_MemorySocial { }
    public class Thought_SocialDependency : Thought_MemorySocial { }

    public class ThoughtWorker_SocialCrazy : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            Hediff_MentalDisorder disorder = otherPawn.Disorder();
            return disorder == null ? ThoughtState.Inactive
                : ThoughtState.ActiveAtStage(MentalDisorderUtility.SeverityStage(disorder.def));
        }
    }
    public class ThoughtWorker_SocialParanoid : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            return p != null && p.Has(MMDDefOf.MMD_ParanoidDelusion) && otherPawn != null && p != otherPawn;
        }
    }
    public class ThoughtWorker_SocialDepression : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            return p != null && p.Has(MMDDefOf.MMD_MajorDepression) && otherPawn != null && p != otherPawn;
        }
    }
    public class ThoughtWorker_SocialPresident : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            Hediff_MentalDisorder d = otherPawn.Disorder();
            return d != null && d.identity == DelusionalIdentity.OldWorldPresident;
        }
    }

    public class ThoughtWorker_SocialNarcissistic : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            return p != null && p.Has(MMDDefOf.MMD_Narcissistic) && otherPawn != null && p != otherPawn;
        }
    }

    public class ThoughtWorker_SocialAnxiety : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            return p != null && p.Has(MMDDefOf.MMD_SocialAnxiety) && otherPawn != null && p != otherPawn;
        }
    }

    public class ThoughtWorker_SocialADHD : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            return otherPawn != null && otherPawn.Has(MMDDefOf.MMD_ADHD) && p != otherPawn;
        }
    }

    public class ThoughtWorker_SocialSchizotypal : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            return otherPawn != null && otherPawn.Has(MMDDefOf.MMD_Schizotypal) && p != otherPawn;
        }
    }

    public class ThoughtWorker_DependencyMood : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder disorder = p?.Disorders()
                .FirstOrDefault(d => d.def == MMDDefOf.MMD_DependentPersonality);
            if (disorder == null || disorder.dependentOn == null) return ThoughtState.Inactive;
            bool together = p.MapHeld != null && p.MapHeld == disorder.dependentOn.MapHeld
                && !disorder.dependentOn.Dead;
            return ThoughtState.ActiveAtStage(together ? 0 : 1);
        }
    }

    public class ThoughtWorker_SocialDependency : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn otherPawn)
        {
            Hediff_MentalDisorder disorder = p?.Disorders()
                .FirstOrDefault(d => d.def == MMDDefOf.MMD_DependentPersonality);
            return disorder != null && disorder.dependentOn == otherPawn
                ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }

    public abstract class ThoughtWorker_SymptomRelief : ThoughtWorker
    {
        protected abstract HediffDef DisorderDef { get; }

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder disorder = p?.Disorders().FirstOrDefault(d => d.def == DisorderDef);
            if (disorder == null) return ThoughtState.Inactive;
            return Find.TickManager.TicksGame - disorder.lastSymptomReliefTick >= 90000
                ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_BodyCheckingUrge : ThoughtWorker_SymptomRelief
    {
        protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_BodyDysmorphic; } }
    }

    public class ThoughtWorker_CompulsionUrge : ThoughtWorker_SymptomRelief
    {
        protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_OCD; } }
    }

    public class ThoughtWorker_HealthCheckUrge : ThoughtWorker_SymptomRelief
    {
        protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_IllnessAnxiety; } }
    }

    public class ThoughtWorker_BulimiaCycle : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder disorder = p?.Disorders().FirstOrDefault(d => d.def == MMDDefOf.MMD_Bulimia);
            if (disorder == null) return ThoughtState.Inactive;
            if (disorder.lastEpisodeTick <= 0) return ThoughtState.Inactive;
            int elapsed = Find.TickManager.TicksGame - disorder.lastEpisodeTick;
            if (elapsed < 30000) return ThoughtState.ActiveAtStage(0);
            if (elapsed < 90000) return ThoughtState.ActiveAtStage(1);
            return ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_AgoraphobiaExposure : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p == null || !p.Has(MMDDefOf.MMD_Agoraphobia)) return ThoughtState.Inactive;
            int exposure = MentalDisorderUtility.AgoraphobiaExposureStage(p);
            return exposure == 0 ? ThoughtState.ActiveAtStage(0)
                : exposure == 1 ? ThoughtState.ActiveAtStage(1)
                : ThoughtState.ActiveAtStage(2);
        }
    }

    public abstract class ThoughtWorker_AdvancedState : ThoughtWorker
    {
        protected abstract HediffDef DisorderDef { get; }
        protected virtual bool InactiveAtZero { get { return false; } }

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder disorder = p?.Disorders().FirstOrDefault(d => d.def == DisorderDef);
            if (disorder == null) return ThoughtState.Inactive;
            int stage = disorder.DynamicStage;
            return InactiveAtZero && stage == 0 ? ThoughtState.Inactive
                : ThoughtState.ActiveAtStage(InactiveAtZero ? stage - 1 : stage);
        }
    }

    public class ThoughtWorker_PanicAftermath : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_PanicDisorder; } }
      protected override bool InactiveAtZero { get { return true; } } }
    public class ThoughtWorker_ExplosiveRemorse : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_IntermittentExplosive; } }
      protected override bool InactiveAtZero { get { return true; } } }
    public class ThoughtWorker_SocialCrowd : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_SocialAnxiety; } } }
    public class ThoughtWorker_GeneralizedWorry : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_GeneralizedAnxiety; } } }
    public class ThoughtWorker_SpecificFear : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_SpecificPhobia; } } }
    public class ThoughtWorker_Claustrophobia : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_Claustrophobia; } } }
    public class ThoughtWorker_AdjustmentStress : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_AdjustmentDisorder; } } }
    public class ThoughtWorker_CyclothymiaPhase : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_Cyclothymia; } } }
    public class ThoughtWorker_HypomaniaPhase : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_Hypomania; } } }
    public class ThoughtWorker_BipolarIIPhase : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_BipolarII; } } }
    public class ThoughtWorker_BipolarIPhase : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_BipolarI; } } }
    public class ThoughtWorker_SchizoaffectivePhase : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_Schizoaffective; } } }
    public class ThoughtWorker_BorderlinePhase : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_Borderline; } } }
    public class ThoughtWorker_SchizotypalPhase : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_Schizotypal; } } }
    public class ThoughtWorker_PersistentLowEnergy : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_PersistentDepressive; } } }
    public class ThoughtWorker_SomaticConcern : ThoughtWorker_AdvancedState
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_SomaticSymptom; } } }
    public class ThoughtWorker_OCPDUrge : ThoughtWorker_SymptomRelief
    { protected override HediffDef DisorderDef { get { return MMDDefOf.MMD_OCPD; } } }

    public class ThoughtWorker_DissociationAftermath : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder d = p?.Disorders().FirstOrDefault(x => x.def == MMDDefOf.MMD_Dissociative);
            return d != null && Find.TickManager.TicksGame - d.lastEpisodeTick < 60000
                ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_TraumaFlashback : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder d = p?.Disorders().FirstOrDefault(x => x.def == MMDDefOf.MMD_PTSD);
            return d != null && d.DynamicStage == 1 ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_DissociativeAmnesia : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder d = p?.Disorders().FirstOrDefault(x => x.def == MMDDefOf.MMD_DissociativeAmnesia);
            return d != null && d.suppressedSkill != null ? ThoughtState.ActiveDefault : ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_NarcissisticState : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Hediff_MentalDisorder d = p?.Disorders().FirstOrDefault(x => x.def == MMDDefOf.MMD_Narcissistic);
            if (d == null || d.lastEpisodeTick <= 0
                || Find.TickManager.TicksGame - d.lastEpisodeTick >= 60000)
                return ThoughtState.Inactive;
            return ThoughtState.ActiveAtStage(d.mechanicPhase == 1 ? 0 : 1);
        }
    }
}
