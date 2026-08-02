using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoreMentalDisorders
{
    public partial class Hediff_MentalDisorder
    {
        public int mechanicPhase;
        public int nextPhaseTick;
        public int lastEpisodeTick;
        public Pawn focusPawn;
        public string triggerTag;
        public SkillDef suppressedSkill;
        public int suppressedSkillLevel = -1;
        public int specificFearExposureTicks;
        public int specificFearAftermathUntil;
        public int specificFearAftermathStage;

        public string SpecificFearLabel
        {
            get
            {
                string[] parts = (triggerTag ?? "").Split('|');
                return parts.Length >= 3 ? parts[2] : triggerTag;
            }
        }

        public bool UsesCycle
        {
            get
            {
                return def == MMDDefOf.MMD_Hypomania || def == MMDDefOf.MMD_Cyclothymia
                    || def == MMDDefOf.MMD_BipolarII || def == MMDDefOf.MMD_BipolarI
                    || def == MMDDefOf.MMD_Schizoaffective || def == MMDDefOf.MMD_DID
                    || def == MMDDefOf.MMD_Borderline || def == MMDDefOf.MMD_Schizotypal;
            }
        }

        public int DynamicStage
        {
            get
            {
                if (def == MMDDefOf.MMD_SocialAnxiety) return Mathf.Clamp(NearbyHumanCount() / 3, 0, 2);
                if (def == MMDDefOf.MMD_GeneralizedAnxiety) return ColonyConcernStage();
                if (def == MMDDefOf.MMD_SpecificPhobia) return SpecificFearStage();
                if (def == MMDDefOf.MMD_Claustrophobia) return MentalDisorderUtility.ClaustrophobiaExposureStage(pawn);
                if (def == MMDDefOf.MMD_PTSD) return lastEpisodeTick > 0
                    && Find.TickManager.TicksGame - lastEpisodeTick < 60000 ? 1 : 0;
                if (def == MMDDefOf.MMD_PanicDisorder) return lastEpisodeTick > 0
                    && Find.TickManager.TicksGame - lastEpisodeTick < 90000 ? 1 : 0;
                if (def == MMDDefOf.MMD_IntermittentExplosive) return lastEpisodeTick > 0
                    && Find.TickManager.TicksGame - lastEpisodeTick < 60000 ? 1 : 0;
                if (def == MMDDefOf.MMD_AdjustmentDisorder) return pawn.needs?.mood != null
                    && pawn.needs.mood.CurLevelPercentage < 0.4f ? 1 : 0;
                if (def == MMDDefOf.MMD_PersistentDepressive || def == MMDDefOf.MMD_SomaticSymptom)
                    return pawn.needs?.mood != null && pawn.needs.mood.CurLevelPercentage < 0.45f ? 1 : 0;
                return mechanicPhase;
            }
        }

        private void InitializeAdvancedMechanics()
        {
            if (nextPhaseTick == 0)
                nextPhaseTick = Find.TickManager.TicksGame + Rand.RangeInclusive(90000, 240000);
            if (def == MMDDefOf.MMD_Borderline)
                focusPawn = ChooseImportantPawn();
            if (def == MMDDefOf.MMD_SpecificPhobia && triggerTag.NullOrEmpty())
                InitializeSpecificFear();
            else if (def == MMDDefOf.MMD_SpecificPhobia && triggerTag.IndexOf('|') < 0)
                MigrateLegacySpecificFear();
            if (def == MMDDefOf.MMD_PTSD && triggerTag.NullOrEmpty())
            {
                Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, false);
                MentalCauseRecord trauma = tracker?.records
                    .Where(r => r.cause == MMDDefOf.MMD_Cause_Trauma && !r.context.NullOrEmpty())
                    .OrderByDescending(r => r.amount).FirstOrDefault();
                triggerTag = trauma?.context ?? "Violence";
            }
        }

        public void TriggerEpisode()
        {
            lastEpisodeTick = Find.TickManager.TicksGame;
        }

        public void RegisterInteraction(Pawn other, InteractionDef interaction)
        {
            if (interaction == null) return;
            string name = interaction.defName;
            bool negative = name.IndexOf("Insult", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Slight", StringComparison.OrdinalIgnoreCase) >= 0;
            if (def == MMDDefOf.MMD_Narcissistic)
            {
                mechanicPhase = negative ? 2 : 1;
                lastEpisodeTick = Find.TickManager.TicksGame;
            }
            if (def == MMDDefOf.MMD_Borderline && other == focusPawn)
            {
                mechanicPhase = negative ? 1 : 0;
                lastEpisodeTick = Find.TickManager.TicksGame;
            }
        }

        public void SwitchIdentityOrPhase()
        {
            mechanicPhase = def == MMDDefOf.MMD_Schizoaffective
                ? (mechanicPhase + 1) % 3 : 1 - mechanicPhase;
            nextPhaseTick = Find.TickManager.TicksGame + Rand.RangeInclusive(90000, 240000);
        }

        public void SuppressRandomSkill()
        {
            if (pawn.skills == null || suppressedSkill != null) return;
            SkillRecord record = pawn.skills.skills.Where(s => s.Level > 2).InRandomOrder().FirstOrDefault();
            if (record == null) return;
            suppressedSkill = record.def;
            suppressedSkillLevel = record.Level;
            record.Level = Math.Max(0, record.Level - Rand.RangeInclusive(2, 5));
            lastEpisodeTick = Find.TickManager.TicksGame;
        }

        private void TickAdvancedMechanics()
        {
            int now = Find.TickManager.TicksGame;
            if (def == MMDDefOf.MMD_SpecificPhobia)
                UpdateSpecificFearExposure(now);
            if (def == MMDDefOf.MMD_Borderline
                && (focusPawn == null || focusPawn.Dead || focusPawn.Destroyed))
                focusPawn = ChooseImportantPawn();
            if (UsesCycle && now >= nextPhaseTick)
            {
                SwitchIdentityOrPhase();
                if (pawn.Spawned) MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, PhaseLabel(), Color.yellow);
            }
            if (def == MMDDefOf.MMD_DissociativeAmnesia && suppressedSkill != null
                && now - lastEpisodeTick >= 120000 && pawn.skills != null)
            {
                SkillRecord record = pawn.skills.GetSkill(suppressedSkill);
                record.Level = Math.Max(record.Level, suppressedSkillLevel);
                suppressedSkill = null;
                suppressedSkillLevel = -1;
            }
            if (def == MMDDefOf.MMD_AdjustmentDisorder && now - onsetTick > 1800000
                && pawn.needs?.mood != null && pawn.needs.mood.CurLevelPercentage > 0.65f)
            {
                pawn.health.RemoveHediff(this);
                return;
            }
            if (!pawn.Spawned || pawn.InMentalState || pawn.Drafted || pawn.jobs == null) return;
            if (def == MMDDefOf.MMD_PanicDisorder && now >= nextPhaseTick)
            {
                TriggerEpisode();
                nextPhaseTick = now + Rand.RangeInclusive(120000, 300000);
                pawn.mindState.mentalStateHandler.TryStartMentalState(MMDDefOf.MMD_PanicEpisode,
                    "MMD_Triggered".Translate(pawn.LabelShortCap, def.LabelCap), true);
            }
            else if (def == MMDDefOf.MMD_GeneralizedAnxiety && DynamicStage >= 2
                && Rand.Chance(0.002f))
                pawn.mindState.mentalStateHandler.TryStartMentalState(MMDDefOf.MMD_PanicEpisode,
                    "MMD_Triggered".Translate(pawn.LabelShortCap, def.LabelCap), true);
            else if (def == MMDDefOf.MMD_SpecificPhobia && DynamicStage >= 2
                && Rand.Chance(0.02f))
                pawn.mindState.mentalStateHandler.TryStartMentalState(MMDDefOf.MMD_PanicEpisode,
                    "MMD_Triggered".Translate(pawn.LabelShortCap, def.LabelCap), true);
            else if (def == MMDDefOf.MMD_Catatonia && now >= nextPhaseTick)
            {
                StartPause(MMDLocalization.Pick("动作突然停止", "Movement suddenly stops"), 1000);
                nextPhaseTick = now + Rand.RangeInclusive(120000, 300000);
            }
            else if (def == MMDDefOf.MMD_Dissociative && now >= nextPhaseTick)
            {
                StartPause(MMDLocalization.Pick("短暂失去现实感", "Briefly loses touch with reality"), 750);
                TriggerEpisode();
                nextPhaseTick = now + Rand.RangeInclusive(120000, 300000);
            }
            else if (def == MMDDefOf.MMD_SomaticSymptom && now >= nextPhaseTick
                && pawn.needs?.mood != null && pawn.needs.mood.CurLevelPercentage < 0.55f)
            {
                Building_Bed bed = RestUtility.FindPatientBedFor(pawn);
                if (bed != null)
                    pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed),
                        JobCondition.InterruptForced, resumeCurJobAfterwards: true);
                nextPhaseTick = now + Rand.RangeInclusive(90000, 180000);
            }
            else if (def == MMDDefOf.MMD_OCPD && now >= nextPhaseTick)
            {
                StartPause(MMDLocalization.Pick("重新检查工作细节", "Rechecking work details"), 600);
                lastSymptomReliefTick = now;
                nextPhaseTick = now + Rand.RangeInclusive(90000, 180000);
            }
        }

        private void StartPause(string text, int ticks)
        {
            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = ticks;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced, resumeCurJobAfterwards: true);
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, text, Color.white);
        }

        private Pawn ChooseImportantPawn()
        {
            return LovePartnerRelationUtility.ExistingLovePartner(pawn)
                ?? PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                    .Where(p => p != pawn && p.Faction == pawn.Faction)
                    .OrderByDescending(p => pawn.relations?.OpinionOf(p) ?? 0).FirstOrDefault();
        }

        private int NearbyHumanCount()
        {
            if (!pawn.Spawned) return 0;
            return pawn.Map.mapPawns.AllPawnsSpawned.Count(p => p != pawn && p.RaceProps.Humanlike
                && p.Position.DistanceToSquared(pawn.Position) <= 100f);
        }

        private int ColonyConcernStage()
        {
            if (!pawn.Spawned) return 0;
            int concerns = pawn.Map.mapPawns.FreeColonistsSpawned.Count(p =>
                p.health.hediffSet.PainTotal > 0.25f || p.needs?.mood?.CurLevelPercentage < 0.3f);
            if (pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Fire).Any()) concerns += 2;
            return Mathf.Clamp(concerns, 0, 2);
        }

        private int SpecificFearStage()
        {
            if (!pawn.Spawned) return 0;
            int current = CurrentSpecificFearStage();
            if (current > 0) return current;
            if (Find.TickManager.TicksGame < specificFearAftermathUntil)
                return specificFearAftermathStage;
            return 0;
        }

        private int CurrentSpecificFearStage()
        {
            if (!pawn.Spawned) return 0;
            string[] parts = (triggerTag ?? "").Split('|');
            if (parts.Length < 2) return 0;
            string kind = parts[0];
            string value = parts[1];
            if (kind == "Time")
            {
                int hour = GenLocalDate.HourOfDay(pawn);
                bool active = value == "Night" ? hour < 6 || hour >= 20
                    : value == "Dawn" ? hour >= 6 && hour < 9
                    : value == "Dusk" ? hour >= 17 && hour < 20
                    : hour >= 9 && hour < 17;
                return active ? ExposureStage : 0;
            }
            if (kind == "Weather")
                return pawn.Map.weatherManager.curWeather != null
                    && pawn.Map.weatherManager.curWeather.defName == value ? ExposureStage : 0;
            IEnumerable<Thing> nearby = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, 12f, true);
            int count = nearby.Count(t => MatchesSpecificFear(t, kind, value));
            return count == 0 ? 0 : ExposureStage;
        }

        private int ExposureStage
        {
            get
            {
                return specificFearExposureTicks < 15000 ? 1
                    : specificFearExposureTicks < 45000 ? 2 : 3;
            }
        }

        private void UpdateSpecificFearExposure(int now)
        {
            int current = CurrentSpecificFearStage();
            if (current > 0)
            {
                specificFearExposureTicks = Math.Min(120000, specificFearExposureTicks + 2500);
                specificFearAftermathStage = ExposureStage;
                specificFearAftermathUntil = now + (specificFearAftermathStage == 1 ? 15000
                    : specificFearAftermathStage == 2 ? 60000 : 120000);
            }
            else if (now >= specificFearAftermathUntil)
            {
                specificFearExposureTicks = 0;
                specificFearAftermathStage = 0;
            }
        }

        public bool SpecificFearPenaltyActive
        {
            get { return def == MMDDefOf.MMD_SpecificPhobia && DynamicStage > 0; }
        }

        private bool MatchesSpecificFear(Thing thing, string kind, string value)
        {
            Pawn other = thing as Pawn;
            if (kind == "Person") return other != null && other.thingIDNumber.ToString() == value;
            if (kind == "Animal") return other != null && other.RaceProps.Animal && other.def.defName == value;
            if (kind == "Race") return other != null && other.kindDef != null && other.kindDef.defName == value;
            if (kind == "Entity") return other != null && !other.RaceProps.Humanlike
                && !other.RaceProps.Animal && other.def.defName == value;
            if (kind == "Special") return value == "Fire" ? thing is Fire : thing is Corpse;
            if (kind == "Furniture") return thing.def.defName == value
                && thing.def.category == ThingCategory.Building;
            if (kind == "Item") return thing.def.defName == value
                && thing.def.category == ThingCategory.Item;
            return false;
        }

        private void InitializeSpecificFear()
        {
            string kind = new[] { "Time", "Entity", "Weather", "Animal",
                "Person", "Race", "Furniture", "Item" }.RandomElement();
            List<Thing> things = pawn.Spawned ? pawn.Map.listerThings.AllThings : new List<Thing>();
            Pawn chosenPawn;
            if (kind == "Time")
            {
                string value = new[] { "Night", "Dawn", "Day", "Dusk" }.RandomElement();
                string label = value == "Night" ? MMDLocalization.Pick("夜晚", "night")
                    : value == "Dawn" ? MMDLocalization.Pick("黎明", "dawn")
                    : value == "Dusk" ? MMDLocalization.Pick("黄昏", "dusk")
                    : MMDLocalization.Pick("白昼", "daylight");
                triggerTag = "Time|" + value + "|" + label;
                return;
            }
            if (kind == "Weather")
            {
                WeatherDef weather = pawn.Spawned ? pawn.Map.weatherManager.curWeather
                    : DefDatabase<WeatherDef>.AllDefsListForReading.RandomElement();
                triggerTag = "Weather|" + weather.defName + "|" + weather.LabelCap;
                return;
            }
            if (kind == "Person")
            {
                chosenPawn = things.OfType<Pawn>().Where(p => p != pawn && p.RaceProps.Humanlike)
                    .InRandomOrder().FirstOrDefault();
                if (chosenPawn != null)
                {
                    triggerTag = "Person|" + chosenPawn.thingIDNumber + "|" + chosenPawn.LabelShortCap;
                    return;
                }
                kind = "Race";
            }
            if (kind == "Animal")
            {
                chosenPawn = things.OfType<Pawn>().Where(p => p.RaceProps.Animal)
                    .InRandomOrder().FirstOrDefault();
                ThingDef animal = chosenPawn != null ? chosenPawn.def
                    : DefDatabase<ThingDef>.AllDefsListForReading
                        .Where(d => d.race != null && d.race.Animal).InRandomOrder().FirstOrDefault();
                if (animal != null)
                {
                    triggerTag = "Animal|" + animal.defName + "|" + animal.LabelCap;
                    return;
                }
                kind = "Item";
            }
            if (kind == "Entity")
            {
                chosenPawn = things.OfType<Pawn>().Where(p => !p.RaceProps.Humanlike && !p.RaceProps.Animal)
                    .InRandomOrder().FirstOrDefault();
                if (chosenPawn != null)
                {
                    triggerTag = "Entity|" + chosenPawn.def.defName + "|" + chosenPawn.def.LabelCap;
                    return;
                }
                ThingDef animal = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(d => d.race != null && d.race.Animal).InRandomOrder().FirstOrDefault();
                if (animal != null)
                    triggerTag = "Animal|" + animal.defName + "|" + animal.LabelCap;
                else
                    triggerTag = "Time|Night|" + MMDLocalization.Pick("夜晚", "night");
                return;
            }
            InitializeSpecificFearFallback(kind, things);
        }

        private void InitializeSpecificFearFallback(string kind, List<Thing> things)
        {
            if (kind == "Race")
            {
                Pawn other = things.OfType<Pawn>().Where(p => p != pawn && p.RaceProps.Humanlike)
                    .InRandomOrder().FirstOrDefault();
                PawnKindDef race = other != null ? other.kindDef : pawn.kindDef;
                triggerTag = "Race|" + race.defName + "|" + race.LabelCap;
                return;
            }
            ThingCategory category = kind == "Furniture" ? ThingCategory.Building : ThingCategory.Item;
            Thing chosen = things.Where(t => t.def.category == category
                    && (kind != "Furniture" || t.def.building != null))
                .InRandomOrder().FirstOrDefault();
            ThingDef chosenDef = chosen != null ? chosen.def
                : DefDatabase<ThingDef>.AllDefsListForReading.Where(d => d.category == category
                    && (kind != "Furniture" || d.building != null)).InRandomOrder().FirstOrDefault();
            if (chosenDef != null)
                triggerTag = kind + "|" + chosenDef.defName + "|" + chosenDef.LabelCap;
            else
                triggerTag = "Time|Night|" + MMDLocalization.Pick("夜晚", "night");
        }

        private void MigrateLegacySpecificFear()
        {
            if (triggerTag == "Fire")
                triggerTag = "Special|Fire|" + MMDLocalization.Pick("火焰", "fire");
            else if (triggerTag == "Corpse")
                triggerTag = "Special|Corpse|" + MMDLocalization.Pick("尸体", "corpses");
            else
                InitializeSpecificFear();
        }

        public string PhaseLabel()
        {
            if (MMDLocalization.English)
            {
                if (def == MMDDefOf.MMD_Hypomania) return mechanicPhase == 1 ? "hypomanic" : "stable";
                if (def == MMDDefOf.MMD_Cyclothymia) return mechanicPhase == 1 ? "elevated" : "low";
                if (def == MMDDefOf.MMD_BipolarII) return mechanicPhase == 1 ? "hypomanic" : "depressive";
                if (def == MMDDefOf.MMD_BipolarI) return mechanicPhase == 1 ? "manic" : "depressive";
                if (def == MMDDefOf.MMD_Schizoaffective)
                    return mechanicPhase == 0 ? "depressive" : mechanicPhase == 1 ? "manic" : "psychotic";
                if (def == MMDDefOf.MMD_DID) return mechanicPhase == 0 ? "protector identity" : "caregiver identity";
                if (def == MMDDefOf.MMD_Borderline) return mechanicPhase == 0 ? "idealization" : "devaluation";
                if (def == MMDDefOf.MMD_Schizotypal) return mechanicPhase == 0 ? "withdrawn" : "active strange beliefs";
                return "";
            }
            if (def == MMDDefOf.MMD_Hypomania) return mechanicPhase == 1 ? "轻躁期" : "平稳期";
            if (def == MMDDefOf.MMD_Cyclothymia) return mechanicPhase == 1 ? "高涨期" : "低落期";
            if (def == MMDDefOf.MMD_BipolarII) return mechanicPhase == 1 ? "轻躁期" : "抑郁期";
            if (def == MMDDefOf.MMD_BipolarI) return mechanicPhase == 1 ? "躁狂期" : "抑郁期";
            if (def == MMDDefOf.MMD_Schizoaffective)
                return mechanicPhase == 0 ? "抑郁期" : mechanicPhase == 1 ? "躁狂期" : "精神病性期";
            if (def == MMDDefOf.MMD_DID) return mechanicPhase == 0 ? "保护者身份" : "照料者身份";
            if (def == MMDDefOf.MMD_Borderline) return mechanicPhase == 0 ? "理想化" : "贬低";
            if (def == MMDDefOf.MMD_Schizotypal) return mechanicPhase == 0 ? "退缩期" : "奇异信念活跃期";
            return "";
        }

        private void ExposeAdvancedData()
        {
            Scribe_Values.Look(ref mechanicPhase, "mechanicPhase");
            Scribe_Values.Look(ref nextPhaseTick, "nextPhaseTick");
            Scribe_Values.Look(ref lastEpisodeTick, "lastEpisodeTick");
            Scribe_References.Look(ref focusPawn, "focusPawn");
            Scribe_Values.Look(ref triggerTag, "triggerTag");
            Scribe_Defs.Look(ref suppressedSkill, "suppressedSkill");
            Scribe_Values.Look(ref suppressedSkillLevel, "suppressedSkillLevel", -1);
            Scribe_Values.Look(ref specificFearExposureTicks, "specificFearExposureTicks");
            Scribe_Values.Look(ref specificFearAftermathUntil, "specificFearAftermathUntil");
            Scribe_Values.Look(ref specificFearAftermathStage, "specificFearAftermathStage");
        }
    }

    public static class AdvancedDisorderUtility
    {
        public static float StatFactor(Hediff_MentalDisorder disorder, StatDef stat)
        {
            if (disorder == null) return 1f;
            int phase = disorder.DynamicStage;
            HediffDef def = disorder.def;
            if (def == MMDDefOf.MMD_SocialAnxiety)
            {
                if (stat == StatDefOf.SocialImpact) return phase == 2 ? 0.55f : phase == 1 ? 0.8f : 1.1f;
                if (stat == StatDefOf.WorkSpeedGlobal) return phase == 2 ? 0.85f : phase == 1 ? 0.95f : 1.05f;
            }
            if (def == MMDDefOf.MMD_GeneralizedAnxiety)
            {
                if (stat == StatDefOf.WorkSpeedGlobal) return phase == 2 ? 0.75f : phase == 1 ? 0.9f : 1.05f;
                if (stat == StatDefOf.AimingDelayFactor) return phase == 2 ? 1.3f : phase == 1 ? 1.15f : 0.95f;
            }
            if (def == MMDDefOf.MMD_SpecificPhobia || def == MMDDefOf.MMD_Claustrophobia)
            {
                if (stat == StatDefOf.MoveSpeed) return phase >= 2 ? 1.25f : phase == 1 ? 1.1f : 1f;
                if (stat == StatDefOf.WorkSpeedGlobal) return phase >= 2 ? 0.65f : phase == 1 ? 0.9f : 1.05f;
                if (stat == StatDefOf.AimingDelayFactor) return phase >= 2 ? 1.4f : phase == 1 ? 1.2f : 1f;
            }
            if (def == MMDDefOf.MMD_Hypomania && phase == 1)
            {
                if (stat == StatDefOf.WorkSpeedGlobal) return 1.25f;
                if (stat == StatDefOf.MoveSpeed) return 1.15f;
                if (stat == StatDefOf.RestFallRateFactor) return 1.25f;
            }
            if (def == MMDDefOf.MMD_Cyclothymia)
            {
                if (stat == StatDefOf.WorkSpeedGlobal) return phase == 1 ? 1.15f : 0.85f;
                if (stat == StatDefOf.MoveSpeed) return phase == 1 ? 1.1f : 0.9f;
            }
            if (def == MMDDefOf.MMD_BipolarII)
            {
                if (stat == StatDefOf.WorkSpeedGlobal) return phase == 1 ? 1.3f : 0.65f;
                if (stat == StatDefOf.MoveSpeed) return phase == 1 ? 1.15f : 0.85f;
                if (stat == StatDefOf.RestFallRateFactor) return phase == 1 ? 1.25f : 0.85f;
            }
            if (def == MMDDefOf.MMD_BipolarI)
            {
                if (stat == StatDefOf.WorkSpeedGlobal) return phase == 1 ? 1.6f : 0.5f;
                if (stat == StatDefOf.MoveSpeed) return phase == 1 ? 1.35f : 0.75f;
                if (stat == StatDefOf.RestFallRateFactor) return phase == 1 ? 1.5f : 0.75f;
            }
            if (def == MMDDefOf.MMD_Schizoaffective)
            {
                if (phase == 0 && stat == StatDefOf.WorkSpeedGlobal) return 0.65f;
                if (phase == 1 && stat == StatDefOf.WorkSpeedGlobal) return 1.4f;
                if (phase == 1 && stat == StatDefOf.MoveSpeed) return 1.2f;
                if (phase == 2 && stat == StatDefOf.PsychicSensitivity) return 1.5f;
                if (phase == 2 && stat == StatDefOf.ResearchSpeed) return 1.2f;
                if ((phase == 0 || phase == 2) && stat == StatDefOf.SocialImpact) return phase == 2 ? 0.5f : 0.7f;
            }
            if (def == MMDDefOf.MMD_DID)
            {
                if (phase == 0 && stat == StatDefOf.MeleeDamageFactor) return 1.5f;
                if (phase == 0 && stat == StatDefOf.IncomingDamageFactor) return 0.8f;
                if (phase == 1 && stat == StatDefOf.SocialImpact) return 1.4f;
                if (phase == 1 && stat.defName == "MedicalTendSpeed") return 1.25f;
            }
            if (def == MMDDefOf.MMD_Borderline)
            {
                if (stat == StatDefOf.SocialImpact) return phase == 0 ? 1.3f : 0.65f;
                if (stat == StatDefOf.WorkSpeedGlobal) return phase == 0 ? 1.1f : 0.8f;
            }
            if (def == MMDDefOf.MMD_Schizotypal)
            {
                if (stat == StatDefOf.ResearchSpeed) return phase == 0 ? 1.15f : 1.1f;
                if (stat == StatDefOf.SocialImpact) return phase == 0 ? 0.6f : 0.75f;
                if (phase == 1 && stat == StatDefOf.PsychicSensitivity) return 1.5f;
            }
            if (def == MMDDefOf.MMD_Dissociative && Find.TickManager.TicksGame - disorder.lastEpisodeTick < 30000)
            {
                if (stat == StatDefOf.WorkSpeedGlobal || stat == StatDefOf.GlobalLearningFactor) return 0.6f;
            }
            if (def == MMDDefOf.MMD_AdjustmentDisorder && phase == 1
                && stat == StatDefOf.WorkSpeedGlobal) return 0.8f;
            if (def == MMDDefOf.MMD_PersistentDepressive && stat == StatDefOf.WorkSpeedGlobal)
                return phase == 1 ? 0.8f : 1.05f;
            if (def == MMDDefOf.MMD_SomaticSymptom && phase == 1
                && stat == StatDefOf.WorkSpeedGlobal) return 0.75f;
            if (def == MMDDefOf.MMD_PanicDisorder && phase == 1)
            {
                if (stat == StatDefOf.MoveSpeed) return 1.25f;
                if (stat == StatDefOf.WorkSpeedGlobal) return 0.6f;
                if (stat == StatDefOf.AimingDelayFactor) return 1.5f;
            }
            if (def == MMDDefOf.MMD_PTSD && phase == 1)
            {
                if (stat == StatDefOf.MoveSpeed) return 1.2f;
                if (stat == StatDefOf.WorkSpeedGlobal) return 0.7f;
            }
            if (def == MMDDefOf.MMD_IntermittentExplosive && phase == 1)
            {
                if (stat == StatDefOf.MeleeDamageFactor) return 1.5f;
                if (stat == StatDefOf.SocialImpact) return 0.5f;
            }
            if (def == MMDDefOf.MMD_Narcissistic && Find.TickManager.TicksGame - disorder.lastEpisodeTick < 60000)
            {
                if (stat == StatDefOf.SocialImpact) return disorder.mechanicPhase == 1 ? 1.25f : 0.7f;
                if (stat == StatDefOf.WorkSpeedGlobal) return disorder.mechanicPhase == 1 ? 1.1f : 0.8f;
            }
            return 1f;
        }
    }
}
