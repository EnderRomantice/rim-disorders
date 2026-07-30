using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreMentalDisorders
{
    public enum DelusionalIdentity
    {
        None,
        AncientPsycaster,
        SpecialForcesOfficer,
        OldWorldPresident,
        HiddenSwordsman,
        ChiefScientist
    }

    public static class MentalDisorderUtility
    {
        public const float CongenitalChance = 0.001f;
        public const float AcquiredChance = 0.0002f;

        public static readonly List<HediffDef> AllDefs = new List<HediffDef>
        {
            MMDDefOf.MMD_ParanoidDelusion,
            MMDDefOf.MMD_MajorDepression,
            MMDDefOf.MMD_Schizophrenia,
            MMDDefOf.MMD_Mania,
            MMDDefOf.MMD_OCD,
            MMDDefOf.MMD_PanicDisorder,
            MMDDefOf.MMD_IntermittentExplosive,
            MMDDefOf.MMD_Narcissistic,
            MMDDefOf.MMD_ADHD,
            MMDDefOf.MMD_SocialAnxiety,
            MMDDefOf.MMD_PersistentDepressive,
            MMDDefOf.MMD_Schizotypal,
            MMDDefOf.MMD_Hypomania,
            MMDDefOf.MMD_GeneralizedAnxiety,
            MMDDefOf.MMD_Dissociative,
            MMDDefOf.MMD_SomaticSymptom,
            MMDDefOf.MMD_PTSD,
            MMDDefOf.MMD_Catatonia,
            MMDDefOf.MMD_Insomnia,
            MMDDefOf.MMD_AdjustmentDisorder,
            MMDDefOf.MMD_SpecificPhobia,
            MMDDefOf.MMD_Agoraphobia,
            MMDDefOf.MMD_AvoidantPersonality,
            MMDDefOf.MMD_DependentPersonality,
            MMDDefOf.MMD_OCPD,
            MMDDefOf.MMD_Cyclothymia,
            MMDDefOf.MMD_BodyDysmorphic,
            MMDDefOf.MMD_IllnessAnxiety,
            MMDDefOf.MMD_Borderline,
            MMDDefOf.MMD_BipolarII,
            MMDDefOf.MMD_DissociativeAmnesia,
            MMDDefOf.MMD_Anorexia,
            MMDDefOf.MMD_Bulimia,
            MMDDefOf.MMD_BipolarI,
            MMDDefOf.MMD_DID,
            MMDDefOf.MMD_Schizoaffective,
            MMDDefOf.MMD_Cotard,
            MMDDefOf.MMD_Claustrophobia,
            MMDDefOf.MMD_Hyperthymesia
        };

        public static Hediff_MentalDisorder Disorder(this Pawn pawn)
        {
            return pawn.Disorders()
                .OrderByDescending(d => SeverityStage(d.def))
                .ThenByDescending(d => IsMechanicAltering(d.def))
                .FirstOrDefault();
        }

        public static List<Hediff_MentalDisorder> Disorders(this Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return new List<Hediff_MentalDisorder>();
            return pawn.health.hediffSet.hediffs.OfType<Hediff_MentalDisorder>().ToList();
        }

        public static Hediff_MentalDisorder DisorderForBreak(this Pawn pawn)
        {
            List<Hediff_MentalDisorder> disorders = pawn.Disorders();
            if (disorders.Count == 0) return null;
            int highestStage = disorders.Max(d => SeverityStage(d.def));
            List<Hediff_MentalDisorder> pool = disorders
                .Where(d => SeverityStage(d.def) == highestStage).ToList();
            List<Hediff_MentalDisorder> mechanicPool = pool
                .Where(d => IsMechanicAltering(d.def)).ToList();
            return (mechanicPool.Count > 0 ? mechanicPool : pool).RandomElement();
        }

        public static bool Has(this Pawn pawn, HediffDef def)
        {
            return pawn.Disorders().Any(d => d.def == def);
        }

        public static void AddRandomDisorder(Pawn pawn, bool acquired)
        {
            if (!EligibleForMentalDisorders(pawn)) return;
            HediffDef chosen = ChooseDisorderBySeverity(pawn);
            if (chosen == null) return;
            pawn.health.AddHediff(chosen);
            if (acquired)
            {
                Messages.Message("MMD_Acquired".Translate(pawn.LabelShortCap, chosen.label), pawn, MessageTypeDefOf.NegativeHealthEvent);
            }
        }

        public static void GenerateCongenitalLoadout(Pawn pawn)
        {
            if (!EligibleForMentalDisorders(pawn) || pawn.Disorders().Count > 0) return;

            if (Rand.Value < MMDChanceSettings.InitialDiseaseChance)
                AddRandomFromStage(pawn, ChooseSeverityFromSettings(), false);
        }

        private static bool AddRandomFromStage(Pawn pawn, int stage, bool acquired)
        {
            List<HediffDef> pool = AllDefs
                .Where(d => SeverityStage(d) == stage && CanAddDisorder(pawn, d)
                    && MMDChanceSettings.DiseaseWeight(d) > 0f)
                .ToList();
            if (pool.Count == 0) return false;
            HediffDef chosen = pool.RandomElementByWeight(MMDChanceSettings.DiseaseWeight);
            pawn.health.AddHediff(chosen);
            if (acquired)
                Messages.Message("MMD_Acquired".Translate(pawn.LabelShortCap, chosen.label), pawn,
                    MessageTypeDefOf.NegativeHealthEvent);
            return true;
        }

        public static bool IsMechanicAltering(HediffDef def)
        {
            if (SeverityStage(def) >= 1) return true;
            return def == MMDDefOf.MMD_SocialAnxiety
                || def == MMDDefOf.MMD_PersistentDepressive;
        }

        public static bool CanAddDisorder(Pawn pawn, HediffDef def)
        {
            if (!EligibleForMentalDisorders(pawn)) return false;
            List<Hediff_MentalDisorder> current = pawn.Disorders();
            return current.Count == 0;
        }

        public static bool EligibleForMentalDisorders(Pawn pawn)
        {
            return pawn != null
                && pawn.health != null
                && pawn.RaceProps.Humanlike
                && !pawn.IsShambler
                && !HasCognitiveStabilizer(pawn);
        }

        public static bool HasCognitiveStabilizer(Pawn pawn)
        {
            return pawn != null
                && pawn.health != null
                && MMDDefOf.MMD_CognitiveStabilizer != null
                && pawn.health.hediffSet.HasHediff(MMDDefOf.MMD_CognitiveStabilizer);
        }

        public static bool HasHippocampectomy(Pawn pawn)
        {
            return pawn != null
                && pawn.health != null
                && MMDDefOf.MMD_Hippocampectomy != null
                && pawn.health.hediffSet.HasHediff(MMDDefOf.MMD_Hippocampectomy);
        }

        public static void StabilizeMind(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) return;
            foreach (Hediff_MentalDisorder disorder in pawn.Disorders().ToList())
                pawn.health.RemoveHediff(disorder);
            foreach (Hediff_MentalEtiology tracker in pawn.health.hediffSet.hediffs
                .OfType<Hediff_MentalEtiology>().ToList())
                pawn.health.RemoveHediff(tracker);
        }

        private static HediffDef ChooseDisorderBySeverity(Pawn pawn)
        {
            int stage = ChooseSeverityFromSettings();
            List<HediffDef> pool = AllDefs
                .Where(d => SeverityStage(d) == stage && CanAddDisorder(pawn, d)
                    && MMDChanceSettings.DiseaseWeight(d) > 0f)
                .ToList();
            if (pool.Count == 0)
                pool = AllDefs.Where(d => CanAddDisorder(pawn, d)
                    && MMDChanceSettings.DiseaseWeight(d) > 0f).ToList();
            return pool.Count > 0 ? pool.RandomElementByWeight(MMDChanceSettings.DiseaseWeight) : null;
        }

        private static int ChooseSeverityFromSettings()
        {
            return Enumerable.Range(0, 4)
                .RandomElementByWeight(i => MMDChanceSettings.SeverityWeight(i));
        }

        public static bool MeetsAcquiredConditions(Pawn pawn)
        {
            return EligibleForMentalDisorders(pawn)
                && pawn.needs != null
                && pawn.needs.mood != null
                && pawn.needs.mood.CurLevelPercentage <= 0.15f;
        }

        public static int RequiredPsylinkLevel(HediffDef def)
        {
            int stage = SeverityStage(def);
            return stage == 3 ? 6 : stage == 0 ? 3 : 4;
        }

        public static string SeverityLabel(HediffDef def)
        {
            if (def == MMDDefOf.MMD_ParanoidDelusion
                || def == MMDDefOf.MMD_MajorDepression
                || def == MMDDefOf.MMD_Schizophrenia
                || def == MMDDefOf.MMD_Mania
                || def == MMDDefOf.MMD_Cotard
                || def == MMDDefOf.MMD_Hyperthymesia)
                return MMDLocalization.Pick("极重", "extreme");
            if (def == MMDDefOf.MMD_IntermittentExplosive
                || def == MMDDefOf.MMD_PTSD
                || def == MMDDefOf.MMD_Catatonia
                || def == MMDDefOf.MMD_BipolarI
                || def == MMDDefOf.MMD_DID
                || def == MMDDefOf.MMD_Schizoaffective)
                return MMDLocalization.Pick("重度", "severe");
            if (def == MMDDefOf.MMD_PanicDisorder
                || def == MMDDefOf.MMD_Narcissistic
                || def == MMDDefOf.MMD_GeneralizedAnxiety
                || def == MMDDefOf.MMD_Dissociative
                || def == MMDDefOf.MMD_SomaticSymptom
                || def == MMDDefOf.MMD_Borderline
                || def == MMDDefOf.MMD_BipolarII
                || def == MMDDefOf.MMD_DissociativeAmnesia
                || def == MMDDefOf.MMD_Anorexia
                || def == MMDDefOf.MMD_Bulimia)
                return MMDLocalization.Pick("中度", "moderate");
            return MMDLocalization.Pick("轻度", "mild");
        }

        public static int SeverityStage(HediffDef def)
        {
            string severity = SeverityLabel(def);
            if (severity == "中度" || severity == "moderate") return 1;
            if (severity == "重度" || severity == "severe") return 2;
            if (severity == "极重" || severity == "extreme") return 3;
            return 0;
        }

        public static int UniversalOpinionOffset(HediffDef def)
        {
            return -5 * (SeverityStage(def) + 1);
        }

        public static int AgoraphobiaExposureStage(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return 0;
            Room room = pawn.GetRoom();
            if (pawn.Position.Roofed(pawn.Map)
                && room != null && !room.PsychologicallyOutdoors) return 0;
            bool coverNearby = GenRadial.RadialCellsAround(pawn.Position, 12f, true)
                .Any(cell => cell.InBounds(pawn.Map) && cell.Roofed(pawn.Map));
            return coverNearby ? 1 : 2;
        }

        public static int ClaustrophobiaExposureStage(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null) return 0;
            Room room = pawn.GetRoom();
            if (room == null || room.PsychologicallyOutdoors || !pawn.Position.Roofed(pawn.Map)) return 0;
            if (room.CellCount >= 100) return 1;
            return room.CellCount < 35 ? 2 : 1;
        }

        public static void MoodMemoryFactors(HediffDef def, out float positive, out float negative)
        {
            positive = 1f;
            negative = 1f;
            if (def == MMDDefOf.MMD_Hyperthymesia) { positive = -1f; negative = -1f; }
            else if (def == MMDDefOf.MMD_ParanoidDelusion) { positive = 0.5f; negative = 2f; }
            else if (def == MMDDefOf.MMD_MajorDepression) { positive = 0.5f; negative = 2f; }
            else if (def == MMDDefOf.MMD_Schizophrenia) { positive = 0.75f; negative = 1.25f; }
            else if (def == MMDDefOf.MMD_Mania) { positive = 2f; negative = 0.5f; }
            else if (def == MMDDefOf.MMD_Cotard) { positive = 0.25f; negative = 0.25f; }
            else if (def == MMDDefOf.MMD_IntermittentExplosive) { positive = 0.75f; negative = 1.5f; }
            else if (def == MMDDefOf.MMD_PTSD) { positive = 0.7f; negative = 2f; }
            else if (def == MMDDefOf.MMD_Catatonia) { positive = 0.7f; negative = 1.5f; }
            else if (def == MMDDefOf.MMD_BipolarI || def == MMDDefOf.MMD_DID) { positive = 0.8f; negative = 0.8f; }
            else if (def == MMDDefOf.MMD_Schizoaffective) { positive = 0.8f; negative = 1.25f; }
            else if (def == MMDDefOf.MMD_PanicDisorder) { positive = 0.8f; negative = 1.25f; }
            else if (def == MMDDefOf.MMD_Narcissistic) { positive = 1.5f; negative = 1.5f; }
            else if (def == MMDDefOf.MMD_GeneralizedAnxiety) { positive = 0.8f; negative = 1.5f; }
            else if (def == MMDDefOf.MMD_Dissociative || def == MMDDefOf.MMD_DissociativeAmnesia) { positive = 0.6f; negative = 0.6f; }
            else if (def == MMDDefOf.MMD_SomaticSymptom || def == MMDDefOf.MMD_Anorexia) { positive = 0.8f; negative = 1.3f; }
            else if (def == MMDDefOf.MMD_Borderline) { positive = 1.5f; negative = 1.5f; }
            else if (def == MMDDefOf.MMD_BipolarII) { positive = 0.9f; negative = 1.2f; }
            else if (def == MMDDefOf.MMD_Bulimia) { positive = 0.8f; negative = 1.25f; }
            else if (def == MMDDefOf.MMD_OCD || def == MMDDefOf.MMD_OCPD) { positive = 0.9f; negative = 1.25f; }
            else if (def == MMDDefOf.MMD_ADHD) { positive = 0.9f; negative = 0.9f; }
            else if (def == MMDDefOf.MMD_SocialAnxiety || def == MMDDefOf.MMD_Agoraphobia
                || def == MMDDefOf.MMD_Claustrophobia) { positive = 0.9f; negative = 1.3f; }
            else if (def == MMDDefOf.MMD_PersistentDepressive) { positive = 0.75f; negative = 1.25f; }
            else if (def == MMDDefOf.MMD_Hypomania) { positive = 1.25f; negative = 0.75f; }
            else if (def == MMDDefOf.MMD_Insomnia) { positive = 0.9f; negative = 1.15f; }
            else if (def == MMDDefOf.MMD_AdjustmentDisorder) { positive = 0.75f; negative = 1.5f; }
            else if (def == MMDDefOf.MMD_SpecificPhobia) { positive = 1.2f; negative = 0.8f; }
            else if (def == MMDDefOf.MMD_Schizotypal) { positive = 1f; negative = 1.1f; }
            else if (def == MMDDefOf.MMD_AvoidantPersonality) { positive = 0.9f; negative = 1.35f; }
            else if (def == MMDDefOf.MMD_DependentPersonality) { positive = 1.2f; negative = 1.5f; }
            else if (def == MMDDefOf.MMD_Cyclothymia) { positive = 0.8f; negative = 0.8f; }
            else if (def == MMDDefOf.MMD_BodyDysmorphic) { positive = 0.8f; negative = 1.4f; }
            else if (def == MMDDefOf.MMD_IllnessAnxiety) { positive = 0.9f; negative = 1.35f; }
        }

        public static void EnsurePsylink(Pawn pawn, int level)
        {
            if (!ModsConfig.RoyaltyActive || pawn == null) return;
            int current = pawn.GetPsylinkLevel();
            if (current < level) PawnUtility.ChangePsylinkLevel(pawn, level - current, false);
        }

        public static void LoseRandomSkills(Pawn pawn)
        {
            if (pawn.skills == null) return;
            List<SkillRecord> candidates = pawn.skills.skills.Where(s => s.Level > 0).InRandomOrder().Take(Rand.RangeInclusive(1, 3)).ToList();
            foreach (SkillRecord skill in candidates)
            {
                int loss = Rand.RangeInclusive(1, Math.Min(4, skill.Level));
                skill.Level -= loss;
                skill.xpSinceLastLevel = 0f;
            }
            if (candidates.Count > 0)
                Messages.Message(MMDLocalization.Pick(pawn.LabelShortCap + "因病发永久遗忘了部分技能。",
                    pawn.LabelShortCap + " permanently forgot part of their skills during an episode."),
                    pawn, MessageTypeDefOf.NegativeHealthEvent);
        }

        public static void CastRandomPsycast(Pawn pawn)
        {
            if (pawn == null || pawn.abilities == null || !pawn.Spawned) return;
            List<Ability> abilities = pawn.abilities.AllAbilitiesForReading
                .Where(a => a is Psycast && a.CanCast.Accepted).InRandomOrder().ToList();
            List<Thing> targets = pawn.Map.listerThings.AllThings
                .Where(t => t != pawn && t.Spawned).InRandomOrder().Take(80).ToList();
            foreach (Ability ability in abilities)
            {
                Thing target = targets.FirstOrDefault(t => ability.CanApplyOn(new LocalTargetInfo(t)));
                if (target != null)
                {
                    ability.Activate(target, target);
                    Messages.Message(MMDLocalization.Pick(
                        pawn.LabelShortCap + "在病发中随机释放了" + ability.def.label + "。",
                        pawn.LabelShortCap + " cast " + ability.def.label + " at random during an episode."), pawn,
                        MessageTypeDefOf.NegativeEvent);
                    return;
                }
            }
        }

        public static void ApplyIdentitySkills(Hediff_MentalDisorder disorder)
        {
            Pawn pawn = disorder.pawn;
            if (pawn.skills == null) return;
            SkillDef skillDef = null;
            if (disorder.identity == DelusionalIdentity.SpecialForcesOfficer) skillDef = SkillDefOf.Shooting;
            if (disorder.identity == DelusionalIdentity.OldWorldPresident) skillDef = SkillDefOf.Social;
            if (disorder.identity == DelusionalIdentity.HiddenSwordsman) skillDef = SkillDefOf.Melee;
            if (disorder.identity == DelusionalIdentity.ChiefScientist) skillDef = SkillDefOf.Intellectual;
            if (skillDef == null) return;
            SkillRecord skill = pawn.skills.GetSkill(skillDef);
            disorder.boostedSkill = skillDef;
            disorder.originalSkillLevel = skill.Level;
            if (skill.Level < 20) skill.Level = 20;
        }

        public static string IdentityLabel(DelusionalIdentity identity)
        {
            if (MMDLocalization.English)
            {
                switch (identity)
                {
                    case DelusionalIdentity.AncientPsycaster: return "ancient psycaster";
                    case DelusionalIdentity.SpecialForcesOfficer: return "special forces officer";
                    case DelusionalIdentity.OldWorldPresident: return "old-world president";
                    case DelusionalIdentity.HiddenSwordsman: return "hidden swordsman";
                    case DelusionalIdentity.ChiefScientist: return "chief scientist";
                    default: return "none";
                }
            }
            switch (identity)
            {
                case DelusionalIdentity.AncientPsycaster: return "古代灵能大师";
                case DelusionalIdentity.SpecialForcesOfficer: return "特殊部队军官";
                case DelusionalIdentity.OldWorldPresident: return "旧时代总统";
                case DelusionalIdentity.HiddenSwordsman: return "隐世剑客";
                case DelusionalIdentity.ChiefScientist: return "首席科学家";
                default: return "无";
            }
        }

        public static string IdentityDescription(DelusionalIdentity identity)
        {
            if (MMDLocalization.English)
            {
                switch (identity)
                {
                    case DelusionalIdentity.AncientPsycaster:
                        return "Current identity: ancient psycaster\n• Psylink level: 6\n• All vanilla psycasts\n• Episode: casts a random psycast\n• Refuses mining, cleaning, and hauling";
                    case DelusionalIdentity.SpecialForcesOfficer:
                        return "Current identity: special forces officer\n• Shooting minimum: 20\n• Guaranteed ranged hits; aiming time: ×0.01\n• Incoming damage: ×0.6; move speed: ×1.2\n• Episode: attacks people or animals\n• Refuses research, art, and crafting";
                    case DelusionalIdentity.OldWorldPresident:
                        return "Current identity: old-world president\n• Social minimum: 20\n• Others' opinion: +40; negotiation: ×1.4\n• Episode: insults a random person\n• Refuses mining, construction, plants, cleaning, and hauling";
                    case DelusionalIdentity.HiddenSwordsman:
                        return "Current identity: hidden swordsman\n• Melee minimum: 20\n• Melee damage: ×4; guaranteed melee hits\n• Melee dodge: +70 percentage points\n• Incoming damage: ×0.2; move speed: ×1.4\n• Episode: attacks people or animals\n• Refuses research, management, and art";
                    case DelusionalIdentity.ChiefScientist:
                        return "Current identity: chief scientist\n• Intellectual minimum: 20; research speed: ×2\n• Episode: destroys furniture\n• Refuses management, hunting, and mining";
                    default: return "";
                }
            }
            switch (identity)
            {
                case DelusionalIdentity.AncientPsycaster:
                    return "当前人格：古代灵能大师\n"
                        + "• 灵能等级：6\n"
                        + "• 掌握全部原版灵能\n"
                        + "• 病发：随机释放一种灵能\n"
                        + "• 拒绝工作：采矿、清洁、搬运";
                case DelusionalIdentity.SpecialForcesOfficer:
                    return "当前人格：特殊部队军官\n"
                        + "• 射击技能最低为20\n"
                        + "• 射击必定命中，瞄准时间：×0.01\n"
                        + "• 承伤系数：×0.6，移动速度：×1.2\n"
                        + "• 病发：无差别攻击人物或动物\n"
                        + "• 拒绝工作：研究、艺术、制造";
                case DelusionalIdentity.OldWorldPresident:
                    return "当前人格：旧时代总统\n"
                        + "• 社交技能最低为20\n"
                        + "• 所有人对其评价+40，谈判能力：×1.4\n"
                        + "• 病发：随机侮辱他人\n"
                        + "• 拒绝工作：采矿、建造、种植、清洁、搬运";
                case DelusionalIdentity.HiddenSwordsman:
                    return "当前人格：隐世剑客\n"
                        + "• 格斗技能最低为20\n"
                        + "• 近战伤害：×4，近战必定命中\n"
                        + "• 近战闪避率+70个百分点\n"
                        + "• 承伤系数：×0.2，移动速度：×1.4\n"
                        + "• 病发：无差别屠杀人物或动物\n"
                        + "• 拒绝工作：研究、管理、艺术";
                case DelusionalIdentity.ChiefScientist:
                    return "当前人格：首席科学家\n"
                        + "• 智力技能最低为20，研究速度：×2\n"
                        + "• 病发：随机毁坏家具\n"
                        + "• 拒绝工作：管理、狩猎、采矿";
                default:
                    return "";
            }
        }

        public static string CompactSpecialEffects(Hediff_MentalDisorder disorder)
        {
            if (disorder == null) return "";
            HediffDef def = disorder.def;
            if (MMDLocalization.English)
            {
                if (def == MMDDefOf.MMD_ParanoidDelusion)
                    return "Mind-kill: unlimited range; line of sight required; 2-second interruptible cast; no cooldown\nHuman target: prior attacker or opinion at −40 or lower";
                if (def == MMDDefOf.MMD_MajorDepression)
                    return "Psycast cost: ×0.5\nNeural heat gain and recovery: ×0.5\nNatural psyfocus decay: none";
                if (def == MMDDefOf.MMD_Mania)
                    return "Psycast cost and neural heat gain: ×2\nNeural heat capacity: ×0.5\nAbility cooldown: 5 seconds\nMelee attack interval: ×0.1\nPain during episode: none\nMovement slowdown during episode: none";
                if (def == MMDDefOf.MMD_Schizophrenia)
                    return IdentityDescription(disorder.identity);
                if (def == MMDDefOf.MMD_Hyperthymesia)
                    return "Natural skill decay: none\nPositive and negative mood memories: permanent";
                if (disorder.UsesCycle) return "Current phase: " + disorder.PhaseLabel();
                if (def == MMDDefOf.MMD_SpecificPhobia)
                    return "Trauma source: " + disorder.SpecificFearLabel
                        + "\nSafe: mood +6; positive memories ×1.2; negative memories ×0.8"
                        + "\nTrauma active: memory multipliers reversed"
                        + "\nExposure penalty: −4 / −9 / −15; aftermath: 6 hours / 1 day / 2 days";
                if (def == MMDDefOf.MMD_PTSD) return "Trauma trigger: " + disorder.triggerTag;
                return "";
            }
            if (def == MMDDefOf.MMD_ParanoidDelusion)
                return "心灵宰杀：无距离限制，需要视线，2秒可打断前摇，无冷却\n人类目标：曾伤害患者或评价低于或等于−40";
            if (def == MMDDefOf.MMD_MajorDepression)
                return "灵能消耗：×0.5\n精神熵获取与消退：×0.5\n精神力自然降低：无";
            if (def == MMDDefOf.MMD_Mania)
                return "灵能消耗与精神熵获取：×2\n精神熵上限：×0.5\n技能冷却：5秒\n近战攻击间隔：×0.1\n病发时疼痛感知：无\n病发时移动减速：无";
            if (def == MMDDefOf.MMD_Schizophrenia)
            {
                switch (disorder.identity)
                {
                    case DelusionalIdentity.AncientPsycaster:
                        return "灵能等级：6\n原版灵能：全部掌握";
                    case DelusionalIdentity.SpecialForcesOfficer:
                        return "射击技能：最低20\n射击命中：必定\n瞄准时间：×0.01\n承伤系数：×0.6\n移动速度：×1.2";
                    case DelusionalIdentity.OldWorldPresident:
                        return "社交技能：最低20\n他人评价：+40\n谈判能力：×1.4";
                    case DelusionalIdentity.HiddenSwordsman:
                        return "格斗技能：最低20\n近战伤害：×4\n近战命中：必定\n近战闪避：+70个百分点\n承伤系数：×0.2\n移动速度：×1.4";
                    case DelusionalIdentity.ChiefScientist:
                        return "智力技能：最低20\n研究速度：×2";
                }
            }
            if (def == MMDDefOf.MMD_Hyperthymesia)
                return "技能自然遗忘：无\n正面与负面心情记忆：永久";
            if (disorder.UsesCycle)
                return "当前状态：" + disorder.PhaseLabel();
            if (def == MMDDefOf.MMD_SpecificPhobia)
                return "创伤来源：" + disorder.SpecificFearLabel
                    + "\n安全时：心情+6；正面情绪持续：×1.2；负面情绪持续：×0.8"
                    + "\n创伤减益存在时：情绪持续倍率反转"
                    + "\n暴露减益：−4 / −9 / −15；后效持续：6小时 / 1天 / 2天";
            if (def == MMDDefOf.MMD_PTSD)
                return "创伤触发源：" + disorder.triggerTag;
            return "";
        }

        public static void Cure(Pawn pawn, Hediff_MentalDisorder disorder)
        {
            if (pawn == null || disorder == null || pawn.health == null) return;
            pawn.health.RemoveHediff(disorder);
            if (!pawn.IsShambler)
                Messages.Message("MMD_Cured".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent);
        }

        public static void CureAll(Pawn pawn)
        {
            List<Hediff_MentalDisorder> disorders = pawn.Disorders();
            if (disorders.Count == 0 || pawn.health == null) return;
            foreach (Hediff_MentalDisorder disorder in disorders.ToList())
                pawn.health.RemoveHediff(disorder);
            if (!pawn.IsShambler)
                Messages.Message("MMD_Cured".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent);
        }

        public static bool IsSerum(Thing thing)
        {
            if (thing == null || thing.def == null) return false;
            string n = thing.def.defName ?? "";
            return n.IndexOf("MindNumb", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Psychophagy", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("思滞", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
