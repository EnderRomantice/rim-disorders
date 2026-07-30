using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreMentalDisorders
{
    public partial class Hediff_MentalDisorder : HediffWithComps
    {
        public DelusionalIdentity identity;
        public SkillDef boostedSkill;
        public int originalSkillLevel = -1;
        public HashSet<int> harmedByPawnIds = new HashSet<int>();
        public List<AbilityDef> grantedPsycasts = new List<AbilityDef>();
        public Pawn dependentOn;
        private int nextCureCheck;
        private int onsetTick;
        public int lastSymptomReliefTick;
        private int nextBehaviorTick;
        private JobDef observedJob;
        private int observedJobSince;

        public int ObservedJobAge
        {
            get { return observedJob == pawn.CurJobDef ? Find.TickManager.TicksGame - observedJobSince : 0; }
        }

        public override string LabelInBrackets
        {
            get
            {
                if (def == MMDDefOf.MMD_Schizophrenia)
                    return MentalDisorderUtility.SeverityLabel(def) + "; "
                        + "MMD_Identity".Translate(MentalDisorderUtility.IdentityLabel(identity));
                return MentalDisorderUtility.SeverityLabel(def);
            }
        }

        public override string LabelBase
        {
            get
            {
                if (def == MMDDefOf.MMD_SpecificPhobia && !SpecificFearLabel.NullOrEmpty())
                    return MMDLocalization.English
                        ? "Post-traumatic stress from " + SpecificFearLabel
                        : "来自" + SpecificFearLabel + "的创伤后遗症";
                return base.LabelBase;
            }
        }

        public override string Description
        {
            get
            {
                string description = DiseasePresentation.Summary(def);
                if (def == MMDDefOf.MMD_Schizophrenia && identity != DelusionalIdentity.None)
                    description += MMDLocalization.Pick("\n当前妄想身份：", "\nCurrent delusional identity: ")
                        + MentalDisorderUtility.IdentityLabel(identity) + ".";
                if (def == MMDDefOf.MMD_DependentPersonality && dependentOn != null)
                    description += MMDLocalization.Pick("\n当前依赖对象：", "\nCurrent dependent person: ")
                        + dependentOn.LabelShortCap + ".";
                if (UsesCycle)
                    description += MMDLocalization.Pick("\n当前状态：", "\nCurrent phase: ") + PhaseLabel() + ".";
                if (def == MMDDefOf.MMD_SpecificPhobia)
                    description += MMDLocalization.Pick("\n恐惧对象：", "\nPhobic trigger: ")
                        + SpecificFearLabel + ".";
                if (def == MMDDefOf.MMD_PTSD)
                    description += MMDLocalization.Pick("\n创伤触发源：", "\nTrauma trigger: ") + triggerTag + ".";
                if (def == MMDDefOf.MMD_Borderline && focusPawn != null)
                    description += MMDLocalization.Pick("\n重要关系对象：", "\nImportant relationship: ")
                        + focusPawn.LabelShortCap + ".";
                return description;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                StringBuilder text = new StringBuilder();
                if (CurStage != null && CurStage.statFactors != null)
                    foreach (StatModifier modifier in CurStage.statFactors)
                        text.AppendLine(modifier.stat.LabelCap + "：×" + modifier.value.ToString("0.##"));
                string special = MentalDisorderUtility.CompactSpecialEffects(this);
                if (!special.NullOrEmpty())
                {
                    if (text.Length > 0) text.AppendLine();
                    text.Append(special);
                }
                return text.ToString().TrimEndNewlines();
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (onsetTick == 0) onsetTick = Find.TickManager.TicksGame;
            if (def == MMDDefOf.MMD_Schizophrenia && identity == DelusionalIdentity.None)
            {
                identity = (DelusionalIdentity)Rand.RangeInclusive(1, 5);
                MentalDisorderUtility.ApplyIdentitySkills(this);
                if (identity == DelusionalIdentity.AncientPsycaster && pawn.abilities != null)
                {
                    foreach (AbilityDef ability in DefDatabase<AbilityDef>.AllDefsListForReading)
                    {
                        if (ability.abilityClass != null && typeof(Psycast).IsAssignableFrom(ability.abilityClass)
                            && pawn.abilities.GetAbility(ability) == null)
                        {
                            pawn.abilities.GainAbility(ability);
                            grantedPsycasts.Add(ability);
                        }
                    }
                }
            }
            if (def == MMDDefOf.MMD_ParanoidDelusion)
                EnsureMindKillAbility();
            MentalDisorderUtility.EnsurePsylink(pawn, MentalDisorderUtility.RequiredPsylinkLevel(def));
            if (def == MMDDefOf.MMD_DependentPersonality) RefreshDependency();
            lastSymptomReliefTick = Find.TickManager.TicksGame;
            nextBehaviorTick = Find.TickManager.TicksGame + Rand.RangeInclusive(30000, 90000);
            InitializeAdvancedMechanics();
            nextCureCheck = Find.TickManager.TicksGame + 250;
        }

        public override void PostRemoved()
        {
            if (boostedSkill != null && originalSkillLevel >= 0 && pawn.skills != null)
            {
                SkillRecord skill = pawn.skills.GetSkill(boostedSkill);
                if (skill.Level == 20) skill.Level = originalSkillLevel;
            }
            if (pawn.abilities != null)
                foreach (AbilityDef ability in grantedPsycasts)
                    pawn.abilities.RemoveAbility(ability);
            base.PostRemoved();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos()) yield return gizmo;
            if (def != MMDDefOf.MMD_ParanoidDelusion || pawn.Faction != Faction.OfPlayer || !pawn.Spawned) yield break;
            EnsureMindKillAbility();
            AbilityDef psychicSlaughter = DefDatabase<AbilityDef>.GetNamedSilentFail("PsychicSlaughter");
            yield return new Command_Action
            {
                defaultLabel = MMDLocalization.Pick("心灵宰杀", "Mind-kill"),
                defaultDesc = MMDLocalization.Pick(
                    "选择同一地图内一个曾伤害过患者的血肉生物、敌对非人血肉生物，或患者评价低于或等于−40的人类。无距离限制，但需要视线；施法前摇2秒，可被打断，无冷却。命中后使用原版心灵宰杀效果。",
                    "Select a flesh creature on the same map that previously harmed the patient, a hostile non-human flesh creature, or a human the patient rates at −40 or lower. Unlimited range, but requires line of sight. The 2-second cast can be interrupted. No cooldown. Uses the vanilla psychic slaughter effect on impact."),
                icon = psychicSlaughter != null ? psychicSlaughter.uiIcon
                    : ContentFinder<Texture2D>.Get("UI/Abilities/Slaughter", false) ?? BaseContent.BadTex,
                action = BeginMindKillTargeting
            };
        }

        private void BeginMindKillTargeting()
        {
            AbilityDef psychicSlaughter = DefDatabase<AbilityDef>.GetNamedSilentFail("PsychicSlaughter");
            EnsureMindKillAbility();
            Texture2D icon = psychicSlaughter != null ? psychicSlaughter.uiIcon
                : ContentFinder<Texture2D>.Get("UI/Abilities/Slaughter", false) ?? BaseContent.BadTex;
            TargetingParameters parameters = TargetingParameters.ForPawns();
            parameters.canTargetHumans = true;
            parameters.canTargetAnimals = true;
            parameters.canTargetMechs = false;
            parameters.canTargetSubhumans = true;
            parameters.validator = target =>
            {
                Pawn victim = target.Thing as Pawn;
                return victim != null && victim.RaceProps.IsFlesh
                    && victim != pawn && !victim.Dead && victim.Map == pawn.Map
                    && GenSight.LineOfSight(pawn.Position, victim.Position, pawn.Map)
                    && IsValidMindKillTarget(victim);
            };
            Find.Targeter.BeginTargeting(parameters, target =>
            {
                Pawn victim = target.Pawn;
                if (victim == null || !parameters.CanTarget(target.ToTargetInfo(pawn.Map)))
                {
                    Messages.Message(MMDLocalization.Pick("这个目标不满足心灵宰杀条件。",
                        "This target does not meet the conditions for mind-kill."),
                        pawn, MessageTypeDefOf.RejectInput);
                    return;
                }
                Ability originalAbility = psychicSlaughter == null || pawn.abilities == null
                    ? null : pawn.abilities.GetAbility(psychicSlaughter);
                if (originalAbility != null)
                {
                    originalAbility.QueueCastingJob(victim, victim);
                    return;
                }
                Messages.Message(MMDLocalization.Pick(
                    "无法取得Anomaly的原版心灵宰杀能力。",
                    "The vanilla Anomaly psychic slaughter ability is unavailable."),
                    pawn, MessageTypeDefOf.RejectInput);
            }, pawn, null, icon);
        }

        private void EnsureMindKillAbility()
        {
            if (def != MMDDefOf.MMD_ParanoidDelusion || pawn?.abilities == null) return;
            AbilityDef psychicSlaughter =
                DefDatabase<AbilityDef>.GetNamedSilentFail("PsychicSlaughter");
            if (psychicSlaughter == null || pawn.abilities.GetAbility(psychicSlaughter) != null) return;
            pawn.abilities.GainAbility(psychicSlaughter);
            if (!grantedPsycasts.Contains(psychicSlaughter))
                grantedPsycasts.Add(psychicSlaughter);
        }

        public bool IsValidMindKillTarget(Pawn victim)
        {
            if (victim == null || victim == pawn || victim.Dead || !victim.RaceProps.IsFlesh)
                return false;
            if (harmedByPawnIds.Contains(victim.thingIDNumber)) return true;
            if (victim.RaceProps.Humanlike && pawn.relations != null)
                return pawn.relations.OpinionOf(victim) <= -40;
            return victim.HostileTo(pawn);
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn == null || pawn.health == null) return;
            if (pawn.IsShambler)
            {
                pawn.health.RemoveHediff(this);
                return;
            }
            if (pawn.IsHashIntervalTick(2500, delta)
                && def == MMDDefOf.MMD_Claustrophobia && pawn.Spawned && !pawn.InMentalState
                && MentalDisorderUtility.ClaustrophobiaExposureStage(pawn) == 2
                && Rand.Chance(0.003f))
                pawn.mindState.mentalStateHandler.TryStartMentalState(
                    MMDDefOf.MMD_PanicEpisode,
                    "MMD_Triggered".Translate(pawn.LabelShortCap, def.LabelCap), true);
            if (pawn.IsHashIntervalTick(2500, delta)
                && def == MMDDefOf.MMD_Agoraphobia && pawn.Spawned && !pawn.InMentalState
                && MentalDisorderUtility.AgoraphobiaExposureStage(pawn) == 2
                && Rand.Chance(0.003f))
                pawn.mindState.mentalStateHandler.TryStartMentalState(
                    MMDDefOf.MMD_PanicEpisode,
                    "MMD_Triggered".Translate(pawn.LabelShortCap, def.LabelCap), true);
            if (pawn.IsHashIntervalTick(2500, delta) && def == MMDDefOf.MMD_DependentPersonality
                && (dependentOn == null || dependentOn.Dead || dependentOn.Destroyed))
                RefreshDependency();
            if (pawn.IsHashIntervalTick(2500, delta)) TickObservableBehaviors();
            if (pawn.IsHashIntervalTick(2500, delta)) TickAdvancedMechanics();
            if (pawn.IsHashIntervalTick(250, delta) && Find.TickManager.TicksGame >= nextCureCheck)
            {
                nextCureCheck = Find.TickManager.TicksGame + 250;
                if (def == MMDDefOf.MMD_Schizophrenia && boostedSkill == null
                    && (identity == DelusionalIdentity.SpecialForcesOfficer
                        || identity == DelusionalIdentity.OldWorldPresident
                        || identity == DelusionalIdentity.HiddenSwordsman
                        || identity == DelusionalIdentity.ChiefScientist))
                    MentalDisorderUtility.ApplyIdentitySkills(this);
                if (pawn.GetStatValue(StatDefOf.PsychicSensitivity) <= 0.0001f)
                    MentalDisorderUtility.CureAll(pawn);
                else
                    MentalDisorderUtility.EnsurePsylink(pawn, MentalDisorderUtility.RequiredPsylinkLevel(def));
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref identity, "identity", DelusionalIdentity.None);
            Scribe_Defs.Look(ref boostedSkill, "boostedSkill");
            Scribe_Values.Look(ref originalSkillLevel, "originalSkillLevel", -1);
            Scribe_Collections.Look(ref harmedByPawnIds, "harmedByPawnIds", LookMode.Value);
            Scribe_Collections.Look(ref grantedPsycasts, "grantedPsycasts", LookMode.Def);
            Scribe_References.Look(ref dependentOn, "dependentOn");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (harmedByPawnIds == null) harmedByPawnIds = new HashSet<int>();
                if (grantedPsycasts == null) grantedPsycasts = new List<AbilityDef>();
                if (onsetTick == 0) onsetTick = Find.TickManager.TicksGame;
            }
            Scribe_Values.Look(ref nextCureCheck, "nextCureCheck");
            Scribe_Values.Look(ref onsetTick, "onsetTick");
            Scribe_Values.Look(ref lastSymptomReliefTick, "lastSymptomReliefTick");
            Scribe_Values.Look(ref nextBehaviorTick, "nextBehaviorTick");
            Scribe_Defs.Look(ref observedJob, "observedJob");
            Scribe_Values.Look(ref observedJobSince, "observedJobSince");
            ExposeAdvancedData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) InitializeAdvancedMechanics();
        }

        private void RefreshDependency()
        {
            Pawn partner = LovePartnerRelationUtility.ExistingLovePartner(pawn);
            if (partner != null && !partner.Dead)
            {
                dependentOn = partner;
                return;
            }
            dependentOn = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                .Where(p => p != pawn && p.Faction == pawn.Faction)
                .OrderByDescending(p => pawn.relations?.OpinionOf(p) ?? 0).FirstOrDefault();
        }

        private void TickObservableBehaviors()
        {
            int now = Find.TickManager.TicksGame;
            if (def == MMDDefOf.MMD_ADHD)
            {
                JobDef current = pawn.CurJobDef;
                if (current != observedJob)
                {
                    observedJob = current;
                    observedJobSince = now;
                }
                else if (current != null && now - observedJobSince > 10000 && pawn.jobs != null
                    && !pawn.Drafted && !pawn.InMentalState && Rand.Chance(0.25f))
                {
                    pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    observedJobSince = now;
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
                        MMDLocalization.Pick("难以继续专注", "Unable to stay focused"), Color.yellow);
                }
                return;
            }
            if (now < nextBehaviorTick || pawn.jobs == null || pawn.Drafted || pawn.InMentalState
                || !pawn.Awake() || !pawn.Spawned) return;
            if (def == MMDDefOf.MMD_BodyDysmorphic || def == MMDDefOf.MMD_OCD)
            {
                Job check = JobMaker.MakeJob(JobDefOf.Wait);
                check.expiryInterval = def == MMDDefOf.MMD_OCD ? 900 : 600;
                pawn.jobs.StartJob(check, JobCondition.InterruptForced, resumeCurJobAfterwards: true);
                lastSymptomReliefTick = now;
                nextBehaviorTick = now + Rand.RangeInclusive(60000, 120000);
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
                    def == MMDDefOf.MMD_OCD
                        ? MMDLocalization.Pick("反复检查", "Checking repeatedly")
                        : MMDLocalization.Pick("检查仪容", "Checking appearance"), Color.white);
            }
            else if (def == MMDDefOf.MMD_IllnessAnxiety)
            {
                Building_Bed bed = RestUtility.FindPatientBedFor(pawn);
                if (bed != null)
                {
                    pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed),
                        JobCondition.InterruptForced, resumeCurJobAfterwards: true);
                    lastSymptomReliefTick = now;
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map,
                        MMDLocalization.Pick("寻求身体检查", "Seeking a medical examination"), Color.white);
                }
                nextBehaviorTick = now + Rand.RangeInclusive(60000, 120000);
            }
            else if (def == MMDDefOf.MMD_Bulimia && pawn.needs?.food != null
                && pawn.needs.food.CurLevelPercentage < 0.7f && Rand.Chance(0.35f))
            {
                pawn.mindState.mentalStateHandler.TryStartMentalState(
                    DefDatabase<MentalStateDef>.GetNamedSilentFail("FoodBinge")
                        ?? MentalStateDefOf.Wander_Sad,
                    "MMD_Triggered".Translate(pawn.LabelShortCap, def.LabelCap), true);
                lastEpisodeTick = now;
                nextBehaviorTick = now + 120000;
            }
        }
    }
}
