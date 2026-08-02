using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreMentalDisorders
{
    public class MentalCauseDef : Def
    {
        public float decayPerDay;
        public float maxAmount = 100f;
        public bool recoverableByHighMood = true;
    }

    // Public alias for extension mods. Existing MentalCauseDefs remain fully compatible.
    public class TraumaDef : MentalCauseDef
    {
    }

    public class CauseRequirement
    {
        public MentalCauseDef cause;
        public float minAmount;
        public int minEvents;
        public MentalCauseDef afterCause;
        public float afterCauseMinAmount;
        public float maxAgeDays;
    }

    public class AcquisitionPath
    {
        public List<CauseRequirement> all;
        public float chance = -1f;
    }

    public class DiseaseAcquisitionExtension : DefModExtension
    {
        public List<CauseRequirement> all;
        public List<CauseRequirement> any;
        public List<AcquisitionPath> alternatives;
        public float chance = 0.01f;
        public int cooldownDays = 120;
        public List<string> specialEffects;
    }

    public class MentalCauseRecord : IExposable
    {
        public MentalCauseDef cause;
        public float amount;
        public int eventCount;
        public string context;
        public int firstTick;
        public int lastTick;
        public int incidentWindowStartTick;
        public float incidentWindowAmount;
        public List<int> milestoneTicks = new List<int>();

        public void ExposeData()
        {
            Scribe_Defs.Look(ref cause, "cause");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref eventCount, "eventCount");
            Scribe_Values.Look(ref context, "context");
            Scribe_Values.Look(ref firstTick, "firstTick");
            Scribe_Values.Look(ref lastTick, "lastTick");
            Scribe_Values.Look(ref incidentWindowStartTick, "incidentWindowStartTick");
            Scribe_Values.Look(ref incidentWindowAmount, "incidentWindowAmount");
            Scribe_Collections.Look(ref milestoneTicks, "milestoneTicks", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && milestoneTicks == null)
                milestoneTicks = new List<int>();
        }
    }

    public class Hediff_MentalEtiology : Hediff
    {
        public List<MentalCauseRecord> records = new List<MentalCauseRecord>();
        public int nextMainOnsetTick;
        public int nextOverlayOnsetTick;
        public int highMoodStabilityTicks;
        public bool nearDeathEpisodeActive;
        public bool psychicOverloadEpisodeActive;
        private int dataVersion = 2;
        private int nextStateSampleTick;

        public override bool Visible { get { return false; } }

        public void Add(MentalCauseDef cause, float amount, string context)
        {
            if (cause == null || amount <= 0f) return;
            MentalCauseRecord record = records.FirstOrDefault(r => r.cause == cause && r.context == context);
            if (record == null)
            {
                record = new MentalCauseRecord { cause = cause, context = context };
                records.Add(record);
            }
            ApplyAmount(record, cause, amount, true);
        }

        // Groups rapid repetitions (for example every punch in one social fight) into one
        // psychological incident and imposes a total cap for that incident.
        public void AddIncident(MentalCauseDef cause, float amount, string context,
            int windowTicks, float maxPerIncident)
        {
            if (cause == null || amount <= 0f || maxPerIncident <= 0f) return;
            MentalCauseRecord record = records.FirstOrDefault(r => r.cause == cause && r.context == context);
            if (record == null)
            {
                record = new MentalCauseRecord { cause = cause, context = context };
                records.Add(record);
            }
            int now = Find.TickManager.TicksGame;
            bool newIncident = record.incidentWindowStartTick <= 0
                || now - record.incidentWindowStartTick > windowTicks;
            if (newIncident)
            {
                record.incidentWindowStartTick = now;
                record.incidentWindowAmount = 0f;
            }
            float accepted = Math.Min(amount, maxPerIncident - record.incidentWindowAmount);
            if (accepted <= 0f) return;
            record.incidentWindowAmount += accepted;
            ApplyAmount(record, cause, accepted, newIncident);
        }

        private static void ApplyAmount(MentalCauseRecord record, MentalCauseDef cause,
            float amount, bool countEvent)
        {
            int now = Find.TickManager.TicksGame;
            if (record.firstTick == 0) record.firstTick = now;
            float oldAmount = record.amount;
            record.amount = Math.Min(cause.maxAmount, record.amount + amount);
            int oldMilestone = (int)(oldAmount / 10f);
            int newMilestone = (int)(record.amount / 10f);
            for (int i = oldMilestone; i < newMilestone; i++)
                record.milestoneTicks.Add(now);
            if (countEvent) record.eventCount++;
            record.lastTick = now;
        }

        public void AddContinuous(MentalCauseDef cause, float amount, string context)
        {
            MentalCauseRecord record = records.FirstOrDefault(r => r.cause == cause && r.context == context);
            int now = Find.TickManager.TicksGame;
            if (record != null && now - record.lastTick > 5000)
            {
                record.amount = 0f;
                record.eventCount = 0;
                record.firstTick = now;
                record.milestoneTicks.Clear();
            }
            Add(cause, amount, context);
        }

        public float Amount(MentalCauseDef cause)
        {
            return records.Where(r => r.cause == cause).Sum(r => r.amount);
        }

        public int Events(MentalCauseDef cause)
        {
            return records.Where(r => r.cause == cause).Sum(r => r.eventCount);
        }

        public int FirstTick(MentalCauseDef cause)
        {
            IEnumerable<MentalCauseRecord> found = records.Where(r => r.cause == cause && r.amount > 0f);
            return found.Any() ? found.Min(r => r.firstTick) : 0;
        }

        public int LastTick(MentalCauseDef cause)
        {
            IEnumerable<MentalCauseRecord> found = records.Where(r => r.cause == cause && r.amount > 0f);
            return found.Any() ? found.Max(r => r.lastTick) : 0;
        }

        public int TickAtAmount(MentalCauseDef cause, float amount)
        {
            int milestone = Math.Max(1, (int)Math.Ceiling(amount / 10f));
            List<int> ticks = records.Where(r => r.cause == cause && r.amount >= amount
                    && r.milestoneTicks != null && r.milestoneTicks.Count >= milestone)
                .Select(r => r.milestoneTicks[milestone - 1]).ToList();
            return ticks.Count > 0 ? ticks.Min() : 0;
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
            int now = Find.TickManager.TicksGame;
            if (now >= nextStateSampleTick)
            {
                nextStateSampleTick = now + 2500;
                if (pawn.health.summaryHealth.SummaryHealthPercent > 0.55f && !pawn.Downed)
                    nearDeathEpisodeActive = false;
                if (pawn.psychicEntropy == null || pawn.psychicEntropy.EntropyRelativeValue < 0.5f)
                    psychicOverloadEpisodeActive = false;
                UpdateHighMoodRecovery(2500);
                MentalEtiologyUtility.SampleLongTermState(pawn, this);
                MentalEtiologyUtility.TryFormDisease(pawn, this);
            }
            if (pawn.IsHashIntervalTick(60000, delta))
            {
                foreach (MentalCauseRecord record in records)
                    if (record.cause != null)
                        record.amount = Math.Max(0f, record.amount - record.cause.decayPerDay);
                records.RemoveAll(r => r.cause == null || r.amount <= 0.01f);
            }
        }

        public bool TryRecordNearDeath(float amount, string context)
        {
            if (nearDeathEpisodeActive || amount <= 0f) return false;
            nearDeathEpisodeActive = true;
            Add(MMDDefOf.MMD_Cause_NearDeath, amount, context);
            return true;
        }

        public bool TryRecordPsychicOverload(float amount)
        {
            if (psychicOverloadEpisodeActive || amount <= 0f) return false;
            psychicOverloadEpisodeActive = true;
            Add(MMDDefOf.MMD_Cause_PsychicOverload, amount, null);
            return true;
        }

        private void UpdateHighMoodRecovery(int sampleTicks)
        {
            if (pawn.needs?.mood == null || records.Count == 0) return;
            float mood = pawn.needs.mood.CurLevelPercentage;
            if (mood >= 0.7f)
                highMoodStabilityTicks = Math.Min(180000, highMoodStabilityTicks + sampleTicks);
            else if (mood < 0.6f)
                highMoodStabilityTicks = Math.Max(0, highMoodStabilityTicks - sampleTicks);

            if (highMoodStabilityTicks < 180000 || mood < 0.7f) return;
            float perDay = mood >= 0.9f ? 4f : mood >= 0.8f ? 3f : 2f;
            float reduction = perDay * sampleTicks / 60000f;
            foreach (MentalCauseRecord record in records)
                if (record.cause != null && record.cause.recoverableByHighMood)
                    record.amount = Math.Max(0f, record.amount - reduction);
            records.RemoveAll(r => r.amount <= 0.01f);
        }

        public float HighMoodStabilityProgress
        {
            get { return Mathf.Clamp01(highMoodStabilityTicks / 180000f); }
        }

        public float CurrentHighMoodRecoveryPerDay
        {
            get
            {
                if (highMoodStabilityTicks < 180000 || pawn.needs?.mood == null
                    || pawn.needs.mood.CurLevelPercentage < 0.7f) return 0f;
                float mood = pawn.needs.mood.CurLevelPercentage;
                return mood >= 0.9f ? 4f : mood >= 0.8f ? 3f : 2f;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "records", LookMode.Deep);
            Scribe_Values.Look(ref nextMainOnsetTick, "nextMainOnsetTick");
            Scribe_Values.Look(ref nextOverlayOnsetTick, "nextOverlayOnsetTick");
            Scribe_Values.Look(ref highMoodStabilityTicks, "highMoodStabilityTicks");
            Scribe_Values.Look(ref nearDeathEpisodeActive, "nearDeathEpisodeActive");
            Scribe_Values.Look(ref psychicOverloadEpisodeActive, "psychicOverloadEpisodeActive");
            Scribe_Values.Look(ref dataVersion, "dataVersion", 0);
            Scribe_Values.Look(ref nextStateSampleTick, "nextStateSampleTick");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (records == null) records = new List<MentalCauseRecord>();
                if (dataVersion < 1)
                {
                    foreach (MentalCauseRecord record in records.Where(r =>
                        r.cause == MMDDefOf.MMD_Cause_NearDeath && r.context != "Death"
                        && r.eventCount > 1 && r.lastTick - r.firstTick < 10000))
                    {
                        record.amount = Math.Min(record.amount, 12f);
                        record.eventCount = 1;
                        if (record.milestoneTicks != null && record.milestoneTicks.Count > 1)
                            record.milestoneTicks.RemoveRange(1, record.milestoneTicks.Count - 1);
                    }
                    dataVersion = 1;
                }
                if (dataVersion < 2)
                {
                    LimitRapidLegacyEvents(MMDDefOf.MMD_Cause_Trauma, 6f);
                    LimitRapidLegacyEvents(MMDDefOf.MMD_Cause_BrainDamage, 15f);
                    LimitRapidLegacyEvents(MMDDefOf.MMD_Cause_BodyTrauma, 8f);
                    LimitRapidLegacyEvents(MMDDefOf.MMD_Cause_OpenFieldLongShot, 30f);
                    LimitRapidLegacyEvents(MMDDefOf.MMD_Cause_PsychicAttack, 15f);
                    dataVersion = 2;
                }
            }
        }

        private void LimitRapidLegacyEvents(MentalCauseDef cause, float cap)
        {
            foreach (MentalCauseRecord record in records.Where(r => r.cause == cause
                && r.context != "MentalBreak" && r.eventCount > 1
                && r.lastTick - r.firstTick < 10000))
            {
                record.amount = Math.Min(record.amount, cap);
                record.eventCount = 1;
                record.incidentWindowStartTick = record.lastTick;
                record.incidentWindowAmount = record.amount;
                int milestones = (int)(record.amount / 10f);
                if (record.milestoneTicks != null && record.milestoneTicks.Count > milestones)
                    record.milestoneTicks.RemoveRange(milestones,
                        record.milestoneTicks.Count - milestones);
            }
        }
    }

    public static class MentalEtiologyUtility
    {
        public static Hediff_MentalEtiology Tracker(Pawn pawn, bool create)
        {
            if (!MentalDisorderUtility.EligibleForMentalDisorders(pawn)) return null;
            Hediff_MentalEtiology tracker = pawn.health.hediffSet.hediffs.OfType<Hediff_MentalEtiology>().FirstOrDefault();
            if (tracker == null && create)
                tracker = (Hediff_MentalEtiology)pawn.health.AddHediff(MMDDefOf.MMD_MentalEtiology);
            return tracker;
        }

        public static void AddCause(Pawn pawn, MentalCauseDef cause, float amount, string context = null)
        {
            Tracker(pawn, true)?.Add(cause, amount, context);
        }

        public static void AddIncidentCause(Pawn pawn, MentalCauseDef cause, float amount,
            string context, int windowTicks, float maxPerIncident)
        {
            Tracker(pawn, true)?.AddIncident(cause, amount, context, windowTicks, maxPerIncident);
        }

        public static void SampleLongTermState(Pawn pawn, Hediff_MentalEtiology tracker)
        {
            if (!MentalDisorderUtility.EligibleForMentalDisorders(pawn) || tracker == null) return;
            if (pawn.needs?.mood != null && pawn.needs.mood.CurLevelPercentage < 0.3f)
                tracker.Add(MMDDefOf.MMD_Cause_LowMood, 0.12f, null);
            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevelPercentage < 0.2f)
                tracker.Add(MMDDefOf.MMD_Cause_SleepLoss, 0.1f, null);
            if (pawn.health.hediffSet.PainTotal > 0.35f)
                tracker.Add(MMDDefOf.MMD_Cause_ChronicPain, 0.1f, null);
            if (pawn.needs?.food != null && pawn.needs.food.CurLevelPercentage < 0.18f)
                tracker.Add(MMDDefOf.MMD_Cause_Hunger, 0.25f, null);
            if (pawn.Spawned && !pawn.Map.mapPawns.AllPawnsSpawned
                .Any(p => p != pawn && p.RaceProps.Humanlike
                    && p.Position.DistanceTo(pawn.Position) <= 12f))
                tracker.Add(MMDDefOf.MMD_Cause_Isolation, 0.12f, null);
            if (pawn.CurJob != null && pawn.needs?.mood != null
                && pawn.needs.mood.CurLevelPercentage < 0.4f)
                tracker.Add(MMDDefOf.MMD_Cause_WorkStress, 0.1f, null);
            if (pawn.health.hediffSet.hediffs.Any(h => h.def.isBad
                && !(h is Hediff_MentalDisorder) && !(h is Hediff_MentalEtiology)
                && !(h is Hediff_Injury)
                && h.def != HediffDefOf.MissingBodyPart
                && h.def != HediffDefOf.BloodLoss))
                tracker.Add(MMDDefOf.MMD_Cause_RecurrentIllness, 0.08f, null);
            if (pawn.health.hediffSet.hediffs.Any(h => h is Hediff_MissingPart))
                tracker.Add(MMDDefOf.MMD_Cause_BodyTrauma, 0.04f, "MissingPart");
            if (pawn.health.hediffSet.hediffs.Any(h =>
            {
                string n = h.def.defName;
                return n.IndexOf("WakeUp", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("GoJuice", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Yayo", StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf("Flake", StringComparison.OrdinalIgnoreCase) >= 0;
            }))
                tracker.Add(MMDDefOf.MMD_Cause_StimulantExposure, 0.5f, null);
            if (pawn.Spawned && pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Filth)
                .Any(t => t.Position.DistanceTo(pawn.Position) <= 6f))
                tracker.Add(MMDDefOf.MMD_Cause_ChaoticEnvironment, 0.08f, "Filth");
            ThoughtDef severeCabinFever = DefDatabase<ThoughtDef>.GetNamedSilentFail("CabinFeverSevere");
            if (severeCabinFever != null && severeCabinFever.Worker.CurrentState(pawn).Active)
                tracker.AddContinuous(MMDDefOf.MMD_Cause_SevereCabinFever, 0.25f, "CabinFeverSevere");
        }

        public static void TryFormDisease(Pawn pawn, Hediff_MentalEtiology tracker)
        {
            if (!MentalDisorderUtility.EligibleForMentalDisorders(pawn) || tracker == null) return;
            int now = Find.TickManager.TicksGame;
            foreach (HediffDef disease in DefDatabase<HediffDef>.AllDefsListForReading
                .Where(d => d.GetModExtension<DiseaseAcquisitionExtension>() != null).InRandomOrder())
            {
                DiseaseAcquisitionExtension recipe = disease.GetModExtension<DiseaseAcquisitionExtension>();
                if (recipe == null || !MentalDisorderUtility.CanAddDisorder(pawn, disease)) continue;
                bool main = MentalDisorderUtility.SeverityStage(disease) >= 1
                    || MentalDisorderUtility.IsMechanicAltering(disease);
                if ((main ? tracker.nextMainOnsetTick : tracker.nextOverlayOnsetTick) > now) continue;
                float chance = MatchingChance(tracker, recipe);
                chance *= MMDChanceSettings.ChanceMultiplier(disease);
                if (chance < 0f || !Rand.Chance(chance)) continue;
                pawn.health.AddHediff(disease);
                int until = now + recipe.cooldownDays * 60000;
                if (main) tracker.nextMainOnsetTick = until; else tracker.nextOverlayOnsetTick = until;
                ReduceRelevantCauses(tracker, recipe);
                Messages.Message("MMD_Acquired".Translate(pawn.LabelShortCap, disease.label), pawn,
                    MessageTypeDefOf.NegativeHealthEvent);
                return;
            }
        }

        private static float MatchingChance(Hediff_MentalEtiology tracker, DiseaseAcquisitionExtension recipe)
        {
            if (recipe.alternatives != null)
            {
                foreach (AcquisitionPath path in recipe.alternatives)
                    if (path.all != null && path.all.All(r => MeetsRequirement(tracker, r)))
                        return path.chance >= 0f ? path.chance : recipe.chance;
                return -1f;
            }
            bool all = recipe.all == null || recipe.all.All(r => MeetsRequirement(tracker, r));
            bool any = recipe.any == null || recipe.any.Count == 0
                || recipe.any.Any(r => MeetsRequirement(tracker, r));
            return all && any ? recipe.chance : -1f;
        }

        private static bool MeetsRequirement(Hediff_MentalEtiology tracker, CauseRequirement requirement)
        {
            if (tracker.Amount(requirement.cause) < requirement.minAmount
                || tracker.Events(requirement.cause) < requirement.minEvents) return false;
            int eventTick = tracker.LastTick(requirement.cause);
            if (requirement.afterCause != null)
            {
                int requiredTick = requirement.afterCauseMinAmount > 0f
                    ? tracker.TickAtAmount(requirement.afterCause, requirement.afterCauseMinAmount)
                    : tracker.FirstTick(requirement.afterCause);
                if (requiredTick == 0 || eventTick < requiredTick) return false;
            }
            if (requirement.maxAgeDays > 0f
                && Find.TickManager.TicksGame - eventTick > requirement.maxAgeDays * 60000f) return false;
            return true;
        }

        private static void ReduceRelevantCauses(Hediff_MentalEtiology tracker, DiseaseAcquisitionExtension recipe)
        {
            HashSet<MentalCauseDef> causes = new HashSet<MentalCauseDef>();
            if (recipe.all != null) foreach (CauseRequirement r in recipe.all) causes.Add(r.cause);
            if (recipe.any != null) foreach (CauseRequirement r in recipe.any) causes.Add(r.cause);
            if (recipe.alternatives != null)
                foreach (AcquisitionPath path in recipe.alternatives)
                    if (path.all != null) foreach (CauseRequirement r in path.all) causes.Add(r.cause);
            foreach (MentalCauseRecord record in tracker.records)
                if (causes.Contains(record.cause)) record.amount *= 0.5f;
        }

        public static string DescribeRecipe(HediffDef disease, DiseaseAcquisitionExtension recipe)
        {
            if (recipe == null)
                return MMDLocalization.Pick("• 先天获得。", "• Congenital only.");
            if (recipe.alternatives != null && recipe.alternatives.Count > 0)
            {
                List<string> paths = new List<string>();
                for (int i = 0; i < recipe.alternatives.Count; i++)
                {
                    AcquisitionPath path = recipe.alternatives[i];
                    float chance = path.chance >= 0f ? path.chance : recipe.chance;
                    paths.Add("• " + DescribeRequirements(path.all, MMDLocalization.Pick(" 且 ", " and "))
                        + MMDLocalization.Pick("后，有 ", " gives a ")
                        + (chance * 100f).ToString("0.###")
                        + MMDLocalization.Pick("% 概率患病。", "% onset chance per check."));
                }
                return string.Join("\n", paths.ToArray());
            }
            List<string> parts = new List<string>();
            if (recipe.all != null && recipe.all.Count > 0)
                parts.Add(DescribeRequirements(recipe.all, MMDLocalization.Pick(" 且 ", " and ")));
            if (recipe.any != null && recipe.any.Count > 0)
                parts.Add(MMDLocalization.Pick("并至少满足其一：", "at least one of: ")
                    + DescribeRequirements(recipe.any, MMDLocalization.Pick(" 或 ", " or ")));
            return "• " + string.Join(MMDLocalization.Pick("；", "; "), parts.ToArray())
                + MMDLocalization.Pick("后，有 ", " gives a ")
                + (recipe.chance * 100f).ToString("0.###")
                + MMDLocalization.Pick("% 概率患病。", "% onset chance per check.");
        }

        private static string DescribeRequirements(List<CauseRequirement> requirements, string separator)
        {
            if (requirements == null || requirements.Count == 0) return MMDLocalization.Pick("无", "none");
            return string.Join(separator, requirements.Select(r =>
            {
                string result = r.cause.label + "≥" + r.minAmount.ToString("0.##");
                if (r.minEvents > 0) result += MMDLocalization.Pick("（至少", " (at least ")
                    + r.minEvents + MMDLocalization.Pick("次）", " events)");
                if (r.afterCause != null)
                {
                    result += MMDLocalization.Pick("，且发生于“", ", occurring after ") + r.afterCause.label;
                    if (r.afterCauseMinAmount > 0f)
                        result += "≥" + r.afterCauseMinAmount.ToString("0.##");
                    result += MMDLocalization.Pick("”之后", "");
                }
                if (r.maxAgeDays > 0f) result += MMDLocalization.Pick("，事件距今不超过", ", no more than ")
                    + r.maxAgeDays.ToString("0.##") + MMDLocalization.Pick("天", " days ago");
                return result;
            }).ToArray());
        }
    }
}
