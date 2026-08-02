using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreMentalDisorders
{
    [StaticConstructorOnStartup]
    public static class MMDHarmony
    {
        static MMDHarmony()
        {
            new Harmony("ender.morementaldisorders").PatchAll();
            LongEventHandler.ExecuteWhenFinished(TraumaTabUtility.InstallTabs);
            LongEventHandler.ExecuteWhenFinished(RimTalkCompatibility.TryRegister);
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_GeneratePawn_MMDPatch
    {
        public static void Postfix(Pawn __result)
        {
            if (__result != null && __result.RaceProps.Humanlike)
                MentalDisorderUtility.GenerateCongenitalLoadout(__result);
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class MentalStateHandler_TryStart_MMDPatch
    {
        public static bool Prefix(MentalStateHandler __instance, ref MentalStateDef stateDef, ref string reason,
            ref Pawn otherPawn, ref bool __result)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (MentalDisorderUtility.HasHippocampectomy(pawn))
            {
                __result = false;
                return false;
            }
            Hediff_MentalDisorder disorder = pawn.DisorderForBreak();
            if (disorder == null) return true;

            reason = "MMD_Triggered".Translate(pawn.LabelShortCap, disorder.LabelCap);
            if (disorder.def == MMDDefOf.MMD_ParanoidDelusion)
            {
                otherPawn = pawn.MapHeld == null ? null : pawn.MapHeld.mapPawns.AllPawnsSpawned
                    .Where(p => p != pawn && p.RaceProps.Humanlike)
                    .OrderBy(p => pawn.relations.OpinionOf(p)).FirstOrDefault();
                stateDef = DefDatabase<MentalStateDef>.GetNamed("MurderousRage");
            }
            else if (disorder.def == MMDDefOf.MMD_MajorDepression)
            {
                DamageInfo lethal = new DamageInfo(DamageDefOf.Cut, 99999f, 999f, instigator: pawn);
                pawn.TakeDamage(lethal);
                __result = true;
                return false;
            }
            else if (disorder.def == MMDDefOf.MMD_Mania)
            {
                stateDef = MentalStateDefOf.Berserk;
            }
            else if (disorder.def == MMDDefOf.MMD_OCD)
            {
                stateDef = DefDatabase<MentalStateDef>.GetNamed("Wander_Psychotic");
            }
            else if (disorder.def == MMDDefOf.MMD_PanicDisorder)
            {
                stateDef = MMDDefOf.MMD_PanicEpisode;
            }
            else if (disorder.def == MMDDefOf.MMD_IntermittentExplosive)
            {
                stateDef = MentalStateDefOf.Berserk;
            }
            else if (disorder.def == MMDDefOf.MMD_Narcissistic)
            {
                stateDef = DefDatabase<MentalStateDef>.GetNamed("InsultingSpree");
            }
            else if (disorder.def == MMDDefOf.MMD_ADHD)
            {
                stateDef = DefDatabase<MentalStateDef>.GetNamed("Wander_Psychotic");
            }
            else if (disorder.def == MMDDefOf.MMD_SocialAnxiety)
            {
                stateDef = MMDDefOf.MMD_PanicEpisode;
            }
            else if (disorder.def == MMDDefOf.MMD_PersistentDepressive)
            {
                stateDef = MentalStateDefOf.Wander_Sad;
            }
            else if (disorder.def == MMDDefOf.MMD_Schizotypal)
            {
                stateDef = MentalStateDefOf.Wander_Psychotic;
            }
            else if (disorder.def == MMDDefOf.MMD_Hypomania)
            {
                stateDef = Rand.Bool
                    ? DefDatabase<MentalStateDef>.GetNamed("InsultingSpree")
                    : MentalStateDefOf.Wander_Psychotic;
            }
            else if (disorder.def == MMDDefOf.MMD_GeneralizedAnxiety)
            {
                stateDef = MMDDefOf.MMD_PanicEpisode;
            }
            else if (disorder.def == MMDDefOf.MMD_Dissociative)
            {
                stateDef = MentalStateDefOf.Wander_Psychotic;
            }
            else if (disorder.def == MMDDefOf.MMD_SomaticSymptom)
            {
                stateDef = MentalStateDefOf.Wander_Sad;
            }
            else if (disorder.def == MMDDefOf.MMD_PTSD)
            {
                stateDef = Rand.Bool ? MMDDefOf.MMD_PanicEpisode : MentalStateDefOf.Berserk;
            }
            else if (disorder.def == MMDDefOf.MMD_Catatonia)
            {
                pawn.health.AddHediff(HediffDefOf.CatatonicBreakdown);
                Messages.Message(MMDLocalization.Pick(pawn.LabelShortCap + "病发并陷入了紧张性木僵。",
                    pawn.LabelShortCap + " entered a catatonic state during an episode."), pawn,
                    MessageTypeDefOf.NegativeHealthEvent);
                __result = true;
                return false;
            }
            else if (disorder.def == MMDDefOf.MMD_Insomnia
                || disorder.def == MMDDefOf.MMD_AdjustmentDisorder
                || disorder.def == MMDDefOf.MMD_BodyDysmorphic
                || disorder.def == MMDDefOf.MMD_IllnessAnxiety
                || disorder.def == MMDDefOf.MMD_Anorexia)
            {
                stateDef = MentalStateDefOf.Wander_Sad;
            }
            else if (disorder.def == MMDDefOf.MMD_SpecificPhobia
                || disorder.def == MMDDefOf.MMD_Agoraphobia
                || disorder.def == MMDDefOf.MMD_Claustrophobia)
            {
                stateDef = MMDDefOf.MMD_PanicEpisode;
            }
            else if (disorder.def == MMDDefOf.MMD_AvoidantPersonality)
            {
                stateDef = MentalStateDefOf.Wander_OwnRoom;
            }
            else if (disorder.def == MMDDefOf.MMD_DependentPersonality)
            {
                bool separated = disorder.dependentOn == null || disorder.dependentOn.Dead
                    || disorder.dependentOn.MapHeld != pawn.MapHeld;
                stateDef = separated ? MMDDefOf.MMD_PanicEpisode : MentalStateDefOf.Wander_Sad;
            }
            else if (disorder.def == MMDDefOf.MMD_OCPD)
            {
                stateDef = MentalStateDefOf.Wander_Psychotic;
            }
            else if (disorder.def == MMDDefOf.MMD_Cyclothymia
                || disorder.def == MMDDefOf.MMD_BipolarII)
            {
                stateDef = Rand.Bool
                    ? DefDatabase<MentalStateDef>.GetNamed("InsultingSpree")
                    : MentalStateDefOf.Wander_Sad;
            }
            else if (disorder.def == MMDDefOf.MMD_Borderline)
            {
                int choice = Rand.Range(0, 3);
                stateDef = choice == 0 ? MentalStateDefOf.Berserk
                    : choice == 1 ? MMDDefOf.MMD_PanicEpisode
                    : DefDatabase<MentalStateDef>.GetNamed("InsultingSpree");
            }
            else if (disorder.def == MMDDefOf.MMD_DissociativeAmnesia)
            {
                disorder.SuppressRandomSkill();
                stateDef = MentalStateDefOf.Wander_Psychotic;
            }
            else if (disorder.def == MMDDefOf.MMD_Bulimia)
            {
                stateDef = DefDatabase<MentalStateDef>.GetNamedSilentFail("FoodBinge")
                    ?? MentalStateDefOf.Wander_Sad;
            }
            else if (disorder.def == MMDDefOf.MMD_BipolarI)
            {
                stateDef = Rand.Bool ? MentalStateDefOf.Berserk
                    : DefDatabase<MentalStateDef>.GetNamed("InsultingSpree");
            }
            else if (disorder.def == MMDDefOf.MMD_DID)
            {
                disorder.SwitchIdentityOrPhase();
                stateDef = disorder.mechanicPhase == 0
                    ? MentalStateDefOf.Berserk : MentalStateDefOf.Wander_Sad;
            }
            else if (disorder.def == MMDDefOf.MMD_Schizoaffective)
            {
                disorder.SwitchIdentityOrPhase();
                stateDef = disorder.mechanicPhase == 0 ? MentalStateDefOf.Wander_Sad
                    : disorder.mechanicPhase == 1
                        ? DefDatabase<MentalStateDef>.GetNamed("InsultingSpree")
                        : MentalStateDefOf.Wander_Psychotic;
            }
            else if (disorder.def == MMDDefOf.MMD_Cotard)
            {
                stateDef = MentalStateDefOf.Berserk;
            }
            else if (disorder.def == MMDDefOf.MMD_Schizophrenia)
            {
                switch (disorder.identity)
                {
                    case DelusionalIdentity.SpecialForcesOfficer:
                    case DelusionalIdentity.HiddenSwordsman:
                        stateDef = MentalStateDefOf.Berserk;
                        break;
                    case DelusionalIdentity.OldWorldPresident:
                        stateDef = DefDatabase<MentalStateDef>.GetNamed("InsultingSpree");
                        break;
                    case DelusionalIdentity.ChiefScientist:
                        stateDef = DefDatabase<MentalStateDef>.GetNamed("Tantrum");
                        break;
                    default:
                        MentalDisorderUtility.CastRandomPsycast(pawn);
                        stateDef = DefDatabase<MentalStateDef>.GetNamed("Wander_Psychotic");
                        break;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MentalState), nameof(MentalState.RecoverFromState))]
    public static class MentalState_Recover_MMDPatch
    {
        public static void Postfix(MentalState __instance)
        {
            Pawn pawn = __instance.pawn;
            if (pawn != null)
            {
                MentalEtiologyUtility.AddCause(pawn, MMDDefOf.MMD_Cause_Trauma, 6f, "MentalBreak");
                if (pawn.needs?.mood != null && pawn.needs.mood.CurLevelPercentage < 0.3f)
                    MentalEtiologyUtility.AddCause(pawn, MMDDefOf.MMD_Cause_LowMood, 8f, "MentalBreak");
                Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, true);
                MentalEtiologyUtility.TryFormDisease(pawn, tracker);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Pawn_PreApplyDamage_MMDPatch
    {
        public static void Prefix(Pawn __instance, ref DamageInfo dinfo)
        {
            Hediff_MentalDisorder d = __instance.Disorder();
            Pawn attacker = dinfo.Instigator as Pawn;
            string incidentSource = attacker != null && attacker != __instance
                ? attacker.ThingID : dinfo.Def?.defName;
            if (attacker != null && attacker != __instance && __instance.relations != null
                && attacker.relations != null
                && (__instance.relations.DirectRelations.Any(r => r.otherPawn == attacker)
                    || __instance.relations.OpinionOf(attacker) >= 40))
            {
                // A quarrel is one rupture between two people, not one rupture per blow.
                MentalEtiologyUtility.AddIncidentCause(__instance,
                    MMDDefOf.MMD_Cause_RelationshipLoss, 25f, attacker.ThingID, 2500, 25f);
            }
            if (dinfo.HitPart != null && dinfo.Amount >= 10f
                && dinfo.HitPart.def.defName == "Brain")
            {
                MentalEtiologyUtility.AddIncidentCause(__instance, MMDDefOf.MMD_Cause_BrainDamage,
                    Mathf.Clamp(dinfo.Amount * 0.4f, 3f, 12f), incidentSource, 2500, 15f);
                MentalEtiologyUtility.AddIncidentCause(__instance, MMDDefOf.MMD_Cause_BodyTrauma,
                    Mathf.Clamp(dinfo.Amount * 0.2f, 1f, 6f), incidentSource, 2500, 8f);
            }
            if (attacker != null && attacker != __instance && __instance.Spawned
                && attacker.Map == __instance.Map
                && attacker.Position.DistanceTo(__instance.Position) >= 30f
                && !__instance.Position.Roofed(__instance.Map))
            {
                Room room = __instance.GetRoom();
                if (room == null || room.PsychologicallyOutdoors)
                {
                    float distance = attacker.Position.DistanceTo(__instance.Position);
                    float amount = Mathf.Clamp(dinfo.Amount * 0.6f + (distance - 30f) * 0.5f, 8f, 30f);
                    MentalEtiologyUtility.AddIncidentCause(__instance,
                        MMDDefOf.MMD_Cause_OpenFieldLongShot, amount, incidentSource, 2500, 30f);
                }
            }
            if (d != null && d.def == MMDDefOf.MMD_ParanoidDelusion && attacker != null && attacker != __instance)
                d.harmedByPawnIds.Add(attacker.thingIDNumber);
            if (d != null && d.def == MMDDefOf.MMD_PanicDisorder
                && !__instance.InMentalState && Rand.Chance(0.05f))
            {
                d.TriggerEpisode();
                __instance.mindState.mentalStateHandler.TryStartMentalState(
                    MMDDefOf.MMD_PanicEpisode, "MMD_Triggered".Translate(__instance.LabelShortCap,
                        MMDDefOf.MMD_PanicDisorder.LabelCap), true);
            }
            if (d != null && d.def == MMDDefOf.MMD_PTSD
                && !__instance.InMentalState
                && (d.triggerTag == "Violence" || dinfo.Def?.defName == d.triggerTag)
                && Rand.Chance(0.18f))
            {
                d.TriggerEpisode();
                __instance.mindState.mentalStateHandler.TryStartMentalState(
                    Rand.Bool ? MMDDefOf.MMD_PanicEpisode : MentalStateDefOf.Berserk,
                    "MMD_Triggered".Translate(__instance.LabelShortCap, MMDDefOf.MMD_PTSD.LabelCap), true);
            }
            if (d != null && d.def == MMDDefOf.MMD_IntermittentExplosive
                && !__instance.InMentalState && Rand.Chance(0.12f))
            {
                d.TriggerEpisode();
                __instance.mindState.mentalStateHandler.TryStartMentalState(
                    MentalStateDefOf.Berserk, "MMD_Triggered".Translate(__instance.LabelShortCap,
                        MMDDefOf.MMD_IntermittentExplosive.LabelCap), true);
            }
            if (dinfo.Amount >= 4f)
            {
                float trauma = Mathf.Clamp((dinfo.Amount - 3f) * 0.18f, 0.5f, 4f);
                MentalEtiologyUtility.AddIncidentCause(__instance, MMDDefOf.MMD_Cause_Trauma,
                    trauma, incidentSource, 2500, 6f);
            }
            if (dinfo.Def != null && dinfo.Def.defName.IndexOf("Psychic", System.StringComparison.OrdinalIgnoreCase) >= 0)
                MentalEtiologyUtility.AddIncidentCause(__instance, MMDDefOf.MMD_Cause_PsychicAttack,
                    15f, incidentSource, 2500, 15f);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Pawn_PostApplyDamage_NearDeath_MMDPatch
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (__instance == null || __instance.Dead || totalDamageDealt <= 0f
                || __instance.health == null) return;
            float health = __instance.health.summaryHealth.SummaryHealthPercent;
            if (health > 0.2f) return;
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(__instance, true);
            if (tracker == null) return;
            float amount = Mathf.Clamp(12f + (0.2f - health) * 40f, 12f, 20f);
            tracker.TryRecordNearDeath(amount, dinfo.Def?.defName);
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), "get_MaxEntropy")]
    public static class PsychicEntropy_Max_MMDPatch
    {
        public static void Postfix(Pawn_PsychicEntropyTracker __instance, ref float __result)
        {
            Pawn pawn = __instance.Pawn;
            if (pawn.Has(MMDDefOf.MMD_Mania)) __result *= 0.5f;
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), nameof(Pawn_PsychicEntropyTracker.TryAddEntropy))]
    public static class PsychicEntropy_Add_MMDPatch
    {
        public static void Prefix(Pawn_PsychicEntropyTracker __instance, ref float value)
        {
            Pawn pawn = __instance.Pawn;
            if (__instance.EntropyRelativeValue >= 0.8f && value > 0f)
                MentalEtiologyUtility.Tracker(pawn, true)?.TryRecordPsychicOverload(
                    Mathf.Clamp(value * 100f, 1f, 15f));
            if (pawn.Has(MMDDefOf.MMD_MajorDepression)) value *= 0.5f;
            if (pawn.Has(MMDDefOf.MMD_Schizophrenia) || pawn.Has(MMDDefOf.MMD_Mania)) value *= 2f;
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), nameof(Pawn_PsychicEntropyTracker.OffsetPsyfocusDirectly))]
    public static class Psyfocus_Offset_MMDPatch
    {
        public static void Prefix(Pawn_PsychicEntropyTracker __instance, ref float offset)
        {
            Pawn pawn = __instance.Pawn;
            if (offset > 0f && pawn.Has(MMDDefOf.MMD_Schizophrenia))
            {
                offset *= 2f;
                return;
            }
            if (offset >= 0f) return;
            if (pawn.Has(MMDDefOf.MMD_MajorDepression)) offset *= 0.5f;
            if (pawn.Has(MMDDefOf.MMD_PersistentDepressive)) offset *= 0.85f;
            if (pawn.Has(MMDDefOf.MMD_Cotard)) offset *= 0.25f;
            if (pawn.Has(MMDDefOf.MMD_Schizophrenia) || pawn.Has(MMDDefOf.MMD_Mania)) offset *= 2f;
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), nameof(Pawn_PsychicEntropyTracker.PsychicEntropyTrackerTickInterval))]
    public static class Psyfocus_NoDecay_MMDPatch
    {
        public static void Prefix(Pawn_PsychicEntropyTracker __instance, out float __state)
        {
            __state = __instance.CurrentPsyfocus;
        }

        public static void Postfix(Pawn_PsychicEntropyTracker __instance, float __state)
        {
            if (__instance.Pawn == null) return;
            if (__instance.Pawn.Has(MMDDefOf.MMD_MajorDepression) && __instance.CurrentPsyfocus < __state)
                __instance.OffsetPsyfocusDirectly(__state - __instance.CurrentPsyfocus);
            else if (__instance.Pawn.Has(MMDDefOf.MMD_Cotard) && __instance.CurrentPsyfocus < __state)
                __instance.OffsetPsyfocusDirectly(__state - __instance.CurrentPsyfocus);
            else if (__instance.Pawn.Has(MMDDefOf.MMD_PersistentDepressive) && __instance.CurrentPsyfocus < __state)
                __instance.OffsetPsyfocusDirectly((__state - __instance.CurrentPsyfocus) * 0.25f);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Pawn_Kill_Cotard_MMDPatch
    {
        public static void Postfix(Pawn __instance)
        {
            Map map = __instance.Corpse?.MapHeld;
            if (map != null)
            {
                IntVec3 deathPos = __instance.PositionHeld;
                foreach (Pawn witness in map.mapPawns.AllPawnsSpawned)
                    if (witness != __instance && witness.RaceProps.Humanlike
                        && witness.Position.DistanceTo(deathPos) <= 30f)
                    {
                        MentalEtiologyUtility.AddCause(witness, MMDDefOf.MMD_Cause_WitnessDeath, 12f,
                            __instance.ThingID);
                        if (witness.relations != null && witness.relations.OpinionOf(__instance) >= 40)
                            MentalEtiologyUtility.AddIncidentCause(witness,
                                MMDDefOf.MMD_Cause_RelationshipLoss, 25f,
                                __instance.ThingID, 2500, 25f);
                    }
            }
            MentalEtiologyUtility.AddCause(__instance, MMDDefOf.MMD_Cause_NearDeath, 30f, "Death");
            if (!__instance.Has(MMDDefOf.MMD_Cotard) || __instance.Corpse == null) return;
            if (ResurrectionUtility.TryResurrect(__instance))
            {
                Pawn_PsychicEntropyTracker tracker = __instance.psychicEntropy;
                if (tracker != null && tracker.CurrentPsyfocus < tracker.TargetPsyfocus)
                    tracker.OffsetPsyfocusDirectly(tracker.TargetPsyfocus - tracker.CurrentPsyfocus);
                Messages.Message(MMDLocalization.Pick(__instance.LabelShortCap + "否定了自己的死亡。",
                    __instance.LabelShortCap + " denied their own death."), __instance,
                    MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker), "get_RecoveryRate")]
    public static class PsychicEntropy_Recovery_MMDPatch
    {
        public static void Postfix(Pawn_PsychicEntropyTracker __instance, ref float __result)
        {
            Pawn pawn = __instance.Pawn;
            if (pawn.Has(MMDDefOf.MMD_Schizophrenia)) __result *= 2f;
            if (pawn.Has(MMDDefOf.MMD_MajorDepression)) __result *= 0.5f;
        }
    }

    [HarmonyPatch(typeof(Thought_Memory), "get_ShouldDiscard")]
    public static class PositiveMemoryDuration_MMDPatch
    {
        public static void Postfix(Thought_Memory __instance, ref bool __result)
        {
            Hediff_MentalDisorder disorder = __instance.pawn?.Disorder();
            float mood = __instance.MoodOffset();
            if (disorder == null || mood == 0f) return;
            float positive;
            float negative;
            MentalDisorderUtility.MoodMemoryFactors(disorder.def, out positive, out negative);
            if (disorder.def == MMDDefOf.MMD_SpecificPhobia)
            {
                positive = disorder.SpecificFearPenaltyActive ? 0.8f : 1.2f;
                negative = disorder.SpecificFearPenaltyActive ? 1.2f : 0.8f;
            }
            float factor = mood > 0f ? positive : negative;
            if (factor < 0f)
            {
                __result = false;
                return;
            }
            int adjustedDuration = Mathf.RoundToInt(__instance.DurationTicks * factor);
            if (!__result && __instance.age >= adjustedDuration) __result = true;
            else if (__result && factor > 1f && __instance.age < adjustedDuration) __result = false;
        }
    }

    [HarmonyPatch(typeof(Thought), "get_LabelCap")]
    public static class SpecificFearThoughtLabel_MMDPatch
    {
        public static void Postfix(Thought __instance, ref string __result)
        {
            if (__instance.def?.defName != "MMD_Thought_SpecificFear") return;
            Hediff_MentalDisorder disorder = __instance.pawn?.Disorders()
                .FirstOrDefault(d => d.def == MMDDefOf.MMD_SpecificPhobia);
            if (disorder == null) return;
            int stage = disorder.DynamicStage;
            if (stage == 0)
                __result = MMDLocalization.Pick("没有我不愿看见的" + disorder.SpecificFearLabel,
                    "No " + disorder.SpecificFearLabel + " I don't want to see");
            else if (stage == 1)
                __result = MMDLocalization.Pick("想起了" + disorder.SpecificFearLabel,
                    "Reminded of " + disorder.SpecificFearLabel);
            else if (stage == 2)
                __result = MMDLocalization.Pick(disorder.SpecificFearLabel + "带来的创伤挥之不去",
                    "The trauma from " + disorder.SpecificFearLabel + " lingers");
            else
                __result = MMDLocalization.Pick("被" + disorder.SpecificFearLabel + "的创伤压垮",
                    "Overwhelmed by trauma from " + disorder.SpecificFearLabel);
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    public static class SkillRecord_Interval_MMDPatch
    {
        public static bool Prefix(SkillRecord __instance)
        {
            return __instance.Pawn == null
                || (!__instance.Pawn.Has(MMDDefOf.MMD_Hyperthymesia)
                    && !MentalDisorderUtility.HasHippocampectomy(__instance.Pawn));
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class SkillRecord_Learn_MMDPatch
    {
        public static bool Prefix(SkillRecord __instance)
        {
            return __instance.Pawn == null
                || !MentalDisorderUtility.HasHippocampectomy(__instance.Pawn);
        }
    }

    [HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
    public static class Anorexia_GetFood_MMDPatch
    {
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && pawn.Has(MMDDefOf.MMD_Anorexia)
                && pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage > 0.16f
                && Rand.Chance(0.8f))
                __result = null;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.JobTrackerTickInterval))]
    public static class Insomnia_Sleep_MMDPatch
    {
        public static void Postfix(Pawn_JobTracker __instance, int delta)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || !pawn.Has(MMDDefOf.MMD_Insomnia) || pawn.CurJobDef != JobDefOf.LayDown)
                return;
            if (pawn.IsHashIntervalTick(7500, delta) && Rand.Chance(0.22f))
            {
                __instance.EndCurrentJob(JobCondition.InterruptForced);
                if (pawn.Spawned) MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
                    MMDLocalization.Pick("从浅睡中惊醒", "Woke from shallow sleep"), Color.yellow);
            }
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue),
        new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) })]
    public static class ConditionalDisorderStats_MMDPatch
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            Pawn pawn = thing as Pawn;
            Hediff_MentalDisorder disorder = pawn?.Disorder();
            if (disorder == null) return;
            int sinceRelief = Find.TickManager.TicksGame - disorder.lastSymptomReliefTick;
            if (disorder.def == MMDDefOf.MMD_BodyDysmorphic)
            {
                if (sinceRelief < 60000)
                {
                    if (stat == StatDefOf.SocialImpact) __result *= 1.15f;
                    if (stat == StatDefOf.WorkSpeedGlobal) __result *= 1.05f;
                }
                else if (sinceRelief >= 90000)
                {
                    if (stat == StatDefOf.SocialImpact) __result *= 0.75f;
                    if (stat == StatDefOf.WorkSpeedGlobal) __result *= 0.9f;
                }
            }
            else if (disorder.def == MMDDefOf.MMD_OCD && sinceRelief >= 90000
                && stat == StatDefOf.WorkSpeedGlobal) __result *= 0.85f;
            else if (disorder.def == MMDDefOf.MMD_ADHD && stat == StatDefOf.WorkSpeedGlobal)
            {
                int age = disorder.ObservedJobAge;
                if (age < 5000) __result *= 1.2f;
                else if (age > 10000) __result *= 0.8f;
            }
            else if (disorder.def == MMDDefOf.MMD_Insomnia && stat == StatDefOf.WorkSpeedGlobal)
            {
                int hour = GenLocalDate.HourOfDay(pawn);
                if (hour >= 20 || hour < 6) __result *= 1.15f;
            }
            else if (disorder.def == MMDDefOf.MMD_Agoraphobia)
            {
                int exposure = MentalDisorderUtility.AgoraphobiaExposureStage(pawn);
                if (exposure == 0)
                {
                    if (stat == StatDefOf.WorkSpeedGlobal) __result *= 1.1f;
                    if (stat == StatDefOf.ResearchSpeed) __result *= 1.15f;
                }
                else
                {
                    if (stat == StatDefOf.MoveSpeed) __result *= exposure == 2 ? 1.2f : 1.1f;
                    if (stat == StatDefOf.WorkSpeedGlobal) __result *= exposure == 2 ? 0.7f : 0.85f;
                    if (stat == StatDefOf.AimingDelayFactor) __result *= exposure == 2 ? 1.35f : 1.15f;
                }
            }
            else if (disorder.def == MMDDefOf.MMD_IllnessAnxiety && sinceRelief < 60000
                && stat == StatDefOf.WorkSpeedGlobal) __result *= 1.05f;
            else if (disorder.def == MMDDefOf.MMD_Bulimia && disorder.lastEpisodeTick > 0
                && Find.TickManager.TicksGame - disorder.lastEpisodeTick < 30000)
            {
                if (stat == StatDefOf.WorkSpeedGlobal) __result *= 1.15f;
                if (stat == StatDefOf.MoveSpeed) __result *= 1.1f;
            }
            else if (disorder.def == MMDDefOf.MMD_AvoidantPersonality && pawn.Spawned
                && pawn.Position.GetRoom(pawn.Map) != null
                && pawn.Position.GetRoom(pawn.Map).Owners.Count() <= 1)
            {
                if (stat == StatDefOf.ResearchSpeed) __result *= 1.2f;
                if (stat == StatDefOf.WorkSpeedGlobal) __result *= 1.1f;
            }
            __result *= AdvancedDisorderUtility.StatFactor(disorder, stat);
            if (disorder.def == MMDDefOf.MMD_Mania && pawn.InMentalState
                && stat == StatDefOf.MoveSpeed)
            {
                float manicMinimum = pawn.def.GetStatValueAbstract(StatDefOf.MoveSpeed) * 2f;
                __result = Mathf.Max(__result, manicMinimum);
            }
        }
    }

    [HarmonyPatch(typeof(HediffSet), "get_PainTotal")]
    public static class Mania_Pain_MMDPatch
    {
        public static void Postfix(HediffSet __instance, ref float __result)
        {
            Pawn pawn = __instance.pawn;
            if (pawn != null && pawn.InMentalState && pawn.Has(MMDDefOf.MMD_Mania))
                __result = 0f;
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractRandomly")]
    public static class SocialAnxiety_Interaction_MMDPatch
    {
        public static bool Prefix(Pawn_InteractionsTracker __instance, ref bool __result)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            Hediff_MentalDisorder disorder = pawn.Disorders()
                .FirstOrDefault(d => d.def == MMDDefOf.MMD_SocialAnxiety);
            if (disorder != null)
            {
                float chance = disorder.DynamicStage == 2 ? 0.8f
                    : disorder.DynamicStage == 1 ? 0.45f : 0.15f;
                if (Rand.Chance(chance))
                {
                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class InteractionDrivenDisorders_MMDPatch
    {
        public static void Postfix(Pawn_InteractionsTracker __instance, Pawn recipient,
            InteractionDef intDef, bool __result)
        {
            if (!__result || recipient == null || intDef == null) return;
            Pawn initiator = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            initiator?.Disorder()?.RegisterInteraction(recipient, intDef);
            recipient.Disorder()?.RegisterInteraction(initiator, intDef);
            string name = intDef.defName ?? "";
            if (name.IndexOf("Insult", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Slight", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Rebuff", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Reject", System.StringComparison.OrdinalIgnoreCase) >= 0)
                MentalEtiologyUtility.AddCause(recipient, MMDDefOf.MMD_Cause_SocialRejection,
                    6f, initiator?.ThingID);
            if (name.IndexOf("Breakup", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MentalEtiologyUtility.AddIncidentCause(recipient,
                    MMDDefOf.MMD_Cause_RelationshipLoss, 30f,
                    initiator?.ThingID, 2500, 30f);
                MentalEtiologyUtility.AddIncidentCause(initiator,
                    MMDDefOf.MMD_Cause_RelationshipLoss, 20f,
                    recipient.ThingID, 2500, 20f);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "StartSocialFight")]
    public static class SocialFight_Causes_MMDPatch
    {
        public static void Postfix(Pawn_InteractionsTracker __instance, Pawn otherPawn)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn == null || otherPawn == null) return;
            MentalEtiologyUtility.AddCause(pawn, MMDDefOf.MMD_Cause_SocialRejection, 8f,
                otherPawn.ThingID);
            MentalEtiologyUtility.AddCause(otherPawn, MMDDefOf.MMD_Cause_SocialRejection, 8f,
                pawn.ThingID);
        }
    }

    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.CanInteractNowWith))]
    public static class AvoidantPersonality_Interaction_MMDPatch
    {
        public static void Postfix(Pawn_InteractionsTracker __instance, Pawn recipient, ref bool __result)
        {
            if (!__result || recipient == null) return;
            Pawn initiator = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (initiator == null) return;
            Pawn avoidant = initiator.Has(MMDDefOf.MMD_AvoidantPersonality) ? initiator
                : recipient.Has(MMDDefOf.MMD_AvoidantPersonality) ? recipient : null;
            Pawn other = avoidant == initiator ? recipient : initiator;
            if (avoidant == null || other == null) return;
            int opinion = avoidant.relations?.OpinionOf(other) ?? 0;
            float avoidanceChance = Mathf.InverseLerp(15f, 100f, opinion) * 0.85f;
            if (Rand.Chance(avoidanceChance)) __result = false;
        }
    }

    [HarmonyPatch(typeof(GatheringsUtility), nameof(GatheringsUtility.ShouldPawnKeepGathering))]
    public static class SocialAnxiety_Gathering_MMDPatch
    {
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (p.Has(MMDDefOf.MMD_SocialAnxiety)) __result = false;
        }
    }

    [HarmonyPatch(typeof(MentalBreaker), "get_BreakThresholdMinor")]
    public static class ManiaBreakThresholdMinor_MMDPatch
    {
        public static void Postfix(MentalBreaker __instance, ref float __result)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn.Has(MMDDefOf.MMD_Mania)) __result = Mathf.Max(__result, 0.75f);
            else if (pawn.Has(MMDDefOf.MMD_IntermittentExplosive)) __result = Mathf.Max(__result, 0.5f);
        }
    }

    [HarmonyPatch(typeof(MentalBreaker), "get_BreakThresholdMajor")]
    public static class ManiaBreakThresholdMajor_MMDPatch
    {
        public static void Postfix(MentalBreaker __instance, ref float __result)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn.Has(MMDDefOf.MMD_Mania)) __result = Mathf.Max(__result, 0.55f);
            else if (pawn.Has(MMDDefOf.MMD_IntermittentExplosive)) __result = Mathf.Max(__result, 0.35f);
        }
    }

    [HarmonyPatch(typeof(MentalBreaker), "get_BreakThresholdExtreme")]
    public static class ManiaBreakThresholdExtreme_MMDPatch
    {
        public static void Postfix(MentalBreaker __instance, ref float __result)
        {
            Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
            if (pawn.Has(MMDDefOf.MMD_Mania)) __result = Mathf.Max(__result, 0.35f);
            else if (pawn.Has(MMDDefOf.MMD_IntermittentExplosive)) __result = Mathf.Max(__result, 0.2f);
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
    public static class StatExtension_GetStatValue_MMDPatch
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            Pawn pawn = thing as Pawn;
            Hediff_MentalDisorder d = pawn.Disorder();
            if (d == null) return;
            string n = stat.defName;

            if (d.identity == DelusionalIdentity.SpecialForcesOfficer)
            {
                if (n == "IncomingDamageFactor") __result *= 0.6f;
                else if (n == "MoveSpeed") __result *= 1.2f;
                else if (n == "AimingDelayFactor") __result *= 0.01f;
            }
            else if (d.identity == DelusionalIdentity.HiddenSwordsman)
            {
                if (n == "IncomingDamageFactor") __result *= 0.2f;
                else if (n == "MoveSpeed") __result *= 1.4f;
                else if (n == "MeleeDamageFactor") __result *= 4f;
                else if (n == "MeleeDodgeChance") __result = Mathf.Min(1f, __result + 0.7f);
            }
            else if (d.identity == DelusionalIdentity.OldWorldPresident && n == "NegotiationAbility")
                __result *= 1.4f;
            else if (d.identity == DelusionalIdentity.ChiefScientist && n == "ResearchSpeed")
                __result *= 2f;
        }
    }

    [HarmonyPatch(typeof(ShotReport), nameof(ShotReport.HitReportFor))]
    public static class ShotReport_HitReportFor_MMDPatch
    {
        private static readonly FieldInfo ShooterField = AccessTools.Field(typeof(ShotReport), "factorFromShooterAndDist");
        private static readonly FieldInfo EquipmentField = AccessTools.Field(typeof(ShotReport), "factorFromEquipment");
        private static readonly FieldInfo TargetField = AccessTools.Field(typeof(ShotReport), "factorFromTargetSize");
        private static readonly FieldInfo WeatherField = AccessTools.Field(typeof(ShotReport), "factorFromWeather");
        private static readonly FieldInfo CoverField = AccessTools.Field(typeof(ShotReport), "coversOverallBlockChance");
        private static readonly FieldInfo ForcedMissField = AccessTools.Field(typeof(ShotReport), "forcedMissRadius");

        public static void Postfix(Thing caster, ref ShotReport __result)
        {
            Pawn pawn = caster as Pawn;
            Hediff_MentalDisorder d = pawn.Disorder();
            if (d == null || d.identity != DelusionalIdentity.SpecialForcesOfficer) return;
            object boxed = __result;
            ShooterField?.SetValue(boxed, 1f);
            EquipmentField?.SetValue(boxed, 1f);
            TargetField?.SetValue(boxed, 1f);
            WeatherField?.SetValue(boxed, 1f);
            CoverField?.SetValue(boxed, 0f);
            ForcedMissField?.SetValue(boxed, 0f);
            __result = (ShotReport)boxed;
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetNonMissChance")]
    public static class Swordsman_NonMiss_MMDPatch
    {
        public static void Postfix(Verb_MeleeAttack __instance, ref float __result)
        {
            Hediff_MentalDisorder d = __instance.CasterPawn.Disorder();
            if (d != null && d.identity == DelusionalIdentity.HiddenSwordsman) __result = 1f;
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetDodgeChance")]
    public static class Swordsman_NoDodge_MMDPatch
    {
        public static void Postfix(Verb_MeleeAttack __instance, ref float __result)
        {
            Hediff_MentalDisorder d = __instance.CasterPawn.Disorder();
            if (d != null && d.identity == DelusionalIdentity.HiddenSwordsman) __result = 0f;
        }
    }

    [HarmonyPatch(typeof(MentalState_Berserk), nameof(MentalState_Berserk.ForceHostileTo),
        new[] { typeof(Thing) })]
    public static class Mania_BerserkHostile_MMDPatch
    {
        public static void Postfix(MentalState_Berserk __instance, Thing t, ref bool __result)
        {
            if (__instance.pawn.Has(MMDDefOf.MMD_Mania) && t != __instance.pawn) __result = true;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.StartCooldown), new[] { typeof(int) })]
    public static class Ability_StartCooldown_MMDPatch
    {
        public static void Prefix(Ability __instance, ref int ticks)
        {
            if (__instance.def.defName == "PsychicSlaughter"
                && __instance.pawn.Has(MMDDefOf.MMD_ParanoidDelusion))
            {
                ticks = 0;
                return;
            }
            if (__instance.pawn.Has(MMDDefOf.MMD_Mania)) ticks = 300;
        }
    }

    public static class MindKillOriginalAbilityUtility
    {
        public static bool IsParanoidSlaughter(Ability ability)
        {
            return ability != null && ability.def.defName == "PsychicSlaughter"
                && ability.pawn.Has(MMDDefOf.MMD_ParanoidDelusion);
        }
    }

    [HarmonyPatch(typeof(Verb), "get_EffectiveRange")]
    public static class MindKillOriginalRange_MMDPatch
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            Ability ability = __instance.DirectOwner as Ability;
            if (MindKillOriginalAbilityUtility.IsParanoidSlaughter(ability))
                __result = 99999f;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.CanApplyOn),
        new[] { typeof(LocalTargetInfo) })]
    public static class MindKillOriginalTarget_MMDPatch
    {
        public static bool Prefix(Ability __instance, LocalTargetInfo target, ref bool __result)
        {
            if (!MindKillOriginalAbilityUtility.IsParanoidSlaughter(__instance)) return true;
            Pawn victim = target.Pawn;
            Hediff_MentalDisorder disorder = __instance.pawn.Disorders()
                .FirstOrDefault(d => d.def == MMDDefOf.MMD_ParanoidDelusion);
            __result = victim != null && !victim.Dead && victim != __instance.pawn
                && victim.Map == __instance.pawn.Map && disorder != null
                && disorder.IsValidMindKillTarget(victim)
                && GenSight.LineOfSight(__instance.pawn.Position, victim.Position,
                    __instance.pawn.Map);
            return false;
        }
    }

    [HarmonyPatch(typeof(CompAbilityEffect_PsychicSlaughter),
        nameof(CompAbilityEffect_PsychicSlaughter.Valid))]
    public static class MindKillOriginalCompValid_MMDPatch
    {
        public static bool Prefix(CompAbilityEffect_PsychicSlaughter __instance,
            LocalTargetInfo target, ref bool __result)
        {
            Ability ability = __instance.parent;
            if (!MindKillOriginalAbilityUtility.IsParanoidSlaughter(ability)) return true;
            Hediff_MentalDisorder disorder = ability.pawn.Disorders()
                .FirstOrDefault(d => d.def == MMDDefOf.MMD_ParanoidDelusion);
            __result = target.Pawn != null && disorder != null
                && disorder.IsValidMindKillTarget(target.Pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GizmosVisible))]
    public static class MindKillOriginalGizmo_MMDPatch
    {
        public static void Postfix(Ability __instance, ref bool __result)
        {
            if (MindKillOriginalAbilityUtility.IsParanoidSlaughter(__instance))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedCooldownTicks),
        new[] { typeof(Verb), typeof(Pawn) })]
    public static class MeleeCooldown_MMDPatch
    {
        public static void Postfix(Verb ownerVerb, Pawn attacker, ref int __result)
        {
            if (attacker.Has(MMDDefOf.MMD_Mania) && ownerVerb is Verb_MeleeAttack)
                __result = Mathf.Max(1, Mathf.RoundToInt(__result * 0.1f));
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDisabledWorkTypes))]
    public static class Pawn_DisabledWork_MMDPatch
    {
        public static void Postfix(Pawn __instance, ref List<WorkTypeDef> __result)
        {
            Hediff_MentalDisorder d = __instance.Disorder();
            if (d == null) return;
            if (d.def == MMDDefOf.MMD_Schizophrenia)
            {
                string[] disabled = null;
                if (d.identity == DelusionalIdentity.AncientPsycaster) disabled = new[] { "Mining", "Cleaning", "Hauling" };
                if (d.identity == DelusionalIdentity.SpecialForcesOfficer) disabled = new[] { "Research", "Art", "Crafting" };
                if (d.identity == DelusionalIdentity.OldWorldPresident) disabled = new[] { "Mining", "Construction", "Growing", "Cleaning", "Hauling" };
                if (d.identity == DelusionalIdentity.HiddenSwordsman) disabled = new[] { "Research", "Warden", "Art" };
                if (d.identity == DelusionalIdentity.ChiefScientist) disabled = new[] { "Warden", "Hunting", "Mining" };
                if (disabled != null)
                    foreach (string defName in disabled)
                    {
                        WorkTypeDef work = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
                        if (work != null && !__result.Contains(work)) __result.Add(work);
                    }
            }
        }
    }

    [HarmonyPatch(typeof(Thing), "PostIngested")]
    public static class Thing_PostIngested_MMDPatch
    {
        public static void Postfix(Thing __instance, Pawn ingester)
        {
            Hediff_MentalDisorder d = ingester.Disorder();
            if (d != null && ModsConfig.AnomalyActive && MentalDisorderUtility.IsSerum(__instance))
                MentalDisorderUtility.CureAll(ingester);
        }
    }
}
