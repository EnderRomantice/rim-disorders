using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoreMentalDisorders
{
    public sealed class DiseaseRiskInfo
    {
        public HediffDef disease;
        public float progress;
        public float chance;
        public List<CauseRequirement> requirements = new List<CauseRequirement>();
    }

    /// <summary>
    /// Stable public entry point for extension mods. Trauma types and disease recipes
    /// are Def-driven; consumers do not need to touch the hidden tracker hediff.
    /// </summary>
    public static class TraumaAPI
    {
        public static event Action<Pawn, MentalCauseDef, float> OnTraumaChanged;
        public static event Action<Pawn, MentalCauseDef> OnTraumaRecovered;

        public static void Add(Pawn pawn, MentalCauseDef trauma, float amount, string context = null)
        {
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, true);
            if (tracker == null || trauma == null || amount <= 0f) return;
            tracker.Add(trauma, amount, context);
            NotifyChanged(pawn, trauma, tracker.Amount(trauma));
        }

        public static void Reduce(Pawn pawn, MentalCauseDef trauma, float amount)
        {
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, false);
            if (tracker == null || trauma == null || amount <= 0f) return;
            foreach (MentalCauseRecord record in tracker.records.Where(r => r.cause == trauma).ToList())
            {
                float take = Math.Min(record.amount, amount);
                record.amount -= take;
                amount -= take;
                if (amount <= 0f) break;
            }
            tracker.records.RemoveAll(r => r.amount <= 0.01f);
            float remaining = tracker.Amount(trauma);
            NotifyChanged(pawn, trauma, remaining);
            if (remaining <= 0f)
            {
                Action<Pawn, MentalCauseDef> recovered = OnTraumaRecovered;
                if (recovered != null) recovered(pawn, trauma);
            }
        }

        public static float GetSeverity(Pawn pawn, MentalCauseDef trauma)
        {
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, false);
            return tracker == null || trauma == null ? 0f : tracker.Amount(trauma);
        }

        public static List<MentalCauseRecord> GetRecords(Pawn pawn)
        {
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, false);
            return tracker == null ? new List<MentalCauseRecord>() : tracker.records.ToList();
        }

        public static List<DiseaseRiskInfo> GetDiseaseRisks(Pawn pawn)
        {
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, false);
            if (tracker == null) return new List<DiseaseRiskInfo>();
            List<DiseaseRiskInfo> result = new List<DiseaseRiskInfo>();
            foreach (HediffDef disease in DefDatabase<HediffDef>.AllDefsListForReading
                .Where(d => d.GetModExtension<DiseaseAcquisitionExtension>() != null))
            {
                if (disease == null || pawn.health.hediffSet.HasHediff(disease)) continue;
                DiseaseAcquisitionExtension recipe = disease.GetModExtension<DiseaseAcquisitionExtension>();
                if (recipe == null) continue;
                DiseaseRiskInfo risk = BuildRisk(tracker, disease, recipe);
                risk.chance *= MMDChanceSettings.ChanceMultiplier(disease);
                if (risk.progress > 0f) result.Add(risk);
            }
            return result.OrderByDescending(r => r.progress).ThenByDescending(r => r.chance).ToList();
        }

        internal static void NotifyChanged(Pawn pawn, MentalCauseDef trauma, float value)
        {
            Action<Pawn, MentalCauseDef, float> changed = OnTraumaChanged;
            if (changed != null) changed(pawn, trauma, value);
        }

        private static DiseaseRiskInfo BuildRisk(Hediff_MentalEtiology tracker, HediffDef disease,
            DiseaseAcquisitionExtension recipe)
        {
            DiseaseRiskInfo best = new DiseaseRiskInfo { disease = disease, chance = recipe.chance };
            if (recipe.alternatives != null && recipe.alternatives.Count > 0)
            {
                foreach (AcquisitionPath path in recipe.alternatives)
                {
                    DiseaseRiskInfo candidate = FromRequirements(tracker, disease, path.all,
                        path.chance >= 0f ? path.chance : recipe.chance);
                    if (candidate.progress > best.progress) best = candidate;
                }
                return best;
            }
            List<CauseRequirement> requirements = new List<CauseRequirement>();
            if (recipe.all != null) requirements.AddRange(recipe.all);
            if (recipe.any != null && recipe.any.Count > 0)
                requirements.Add(recipe.any.OrderByDescending(r => RequirementProgress(tracker, r)).First());
            return FromRequirements(tracker, disease, requirements, recipe.chance);
        }

        private static DiseaseRiskInfo FromRequirements(Hediff_MentalEtiology tracker, HediffDef disease,
            List<CauseRequirement> requirements, float chance)
        {
            DiseaseRiskInfo info = new DiseaseRiskInfo
            {
                disease = disease,
                chance = chance,
                requirements = requirements == null
                    ? new List<CauseRequirement>() : requirements.ToList()
            };
            info.progress = info.requirements.Count == 0 ? 0f
                : info.requirements.Min(r => RequirementProgress(tracker, r));
            return info;
        }

        public static float RequirementProgress(Hediff_MentalEtiology tracker, CauseRequirement requirement)
        {
            if (tracker == null || requirement == null || requirement.cause == null) return 0f;
            float amountProgress = requirement.minAmount <= 0f ? 1f
                : Mathf.Clamp01(tracker.Amount(requirement.cause) / requirement.minAmount);
            float eventProgress = requirement.minEvents <= 0 ? 1f
                : Mathf.Clamp01((float)tracker.Events(requirement.cause) / requirement.minEvents);
            float result = Math.Min(amountProgress, eventProgress);
            if (requirement.afterCause != null)
            {
                int requiredTick = requirement.afterCauseMinAmount > 0f
                    ? tracker.TickAtAmount(requirement.afterCause, requirement.afterCauseMinAmount)
                    : tracker.FirstTick(requirement.afterCause);
                if (requiredTick == 0 || tracker.LastTick(requirement.cause) < requiredTick)
                    result = Math.Min(result, 0.5f);
            }
            if (requirement.maxAgeDays > 0f && tracker.LastTick(requirement.cause) > 0
                && Find.TickManager.TicksGame - tracker.LastTick(requirement.cause)
                    > requirement.maxAgeDays * 60000f)
                result = 0f;
            return result;
        }
    }

    public static class TraumaTabUtility
    {
        public static void InstallTabs()
        {
            Type tabType = typeof(ITab_Pawn_Trauma);
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.race == null || !def.race.Humanlike) continue;
                if (def.inspectorTabs == null) def.inspectorTabs = new List<Type>();
                if (def.inspectorTabsResolved == null) def.inspectorTabsResolved = new List<InspectTabBase>();
                if (!def.inspectorTabs.Contains(tabType)) def.inspectorTabs.Add(tabType);
                if (!def.inspectorTabsResolved.Any(t => t.GetType() == tabType))
                    def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
            }
        }
    }

    public class ITab_Pawn_Trauma : ITab
    {
        private Vector2 scrollPosition;
        private readonly HashSet<string> expandedGroups = new HashSet<string>();
        private int currentPage;
        private int riskSeverity = -1;

        private sealed class TraumaDisplayGroup
        {
            public MentalCauseDef cause;
            public List<MentalCauseRecord> records;
            public float Amount { get { return records.Sum(r => r.amount); } }
            public int Events { get { return records.Sum(r => r.eventCount); } }
            public int LastTick { get { return records.Max(r => r.lastTick); } }
        }

        public ITab_Pawn_Trauma()
        {
            labelKey = "MMD_TraumaTab";
            size = new Vector2(620f, 520f);
        }

        public override bool IsVisible
        {
            get { return SelPawn != null && SelPawn.RaceProps.Humanlike && !SelPawn.IsShambler; }
        }

        protected override void FillTab()
        {
            Pawn pawn = SelPawn;
            if (pawn == null) return;
            float tabWidth = (size.x - 24f) / 2f;
            if (DrawTabButton(new Rect(10f, 8f, tabWidth, 32f),
                MMDLocalization.Pick("创伤记录", "Trauma records"), currentPage == 0))
            {
                currentPage = 0;
                scrollPosition = Vector2.zero;
            }
            if (DrawTabButton(new Rect(14f + tabWidth, 8f, tabWidth, 32f),
                MMDLocalization.Pick("可能形成的疾病", "Possible disorders"), currentPage == 1))
            {
                currentPage = 1;
                scrollPosition = Vector2.zero;
            }
            if (currentPage == 1)
            {
                FillRiskPage(pawn);
                return;
            }
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, false);
            List<TraumaDisplayGroup> groups = tracker == null
                ? new List<TraumaDisplayGroup>()
                : tracker.records.Where(r => r.cause != null && r.amount > 0.01f)
                    .GroupBy(r => r.cause)
                    .Select(g => new TraumaDisplayGroup { cause = g.Key, records = g.ToList() })
                    .OrderByDescending(g => g.Amount / Math.Max(1f, g.cause.maxAmount)).ToList();
            float contentHeight = 150f + groups.Count * 82f;
            foreach (TraumaDisplayGroup group in groups)
                if (expandedGroups.Contains(GroupKey(pawn, group.cause)) && group.records.Count > 1)
                    contentHeight += group.records.Count * 30f;
            Rect outer = new Rect(10f, 46f, size.x - 20f, size.y - 56f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, Math.Max(outer.height, contentHeight));
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            float y = 0f;
            DrawRecovery(pawn, tracker, view.width, ref y);
            DrawHeading(MMDLocalization.Pick("已记录的创伤", "Recorded trauma"), view.width, ref y);
            if (groups.Count == 0)
            {
                Widgets.Label(new Rect(8f, y, view.width - 16f, 30f),
                    MMDLocalization.Pick("当前没有可见的创伤记录。", "No visible trauma is currently recorded."));
                y += 38f;
            }
            else
                foreach (TraumaDisplayGroup group in groups)
                    DrawTraumaGroup(pawn, group, view.width, ref y);

            Widgets.EndScrollView();
        }

        private void FillRiskPage(Pawn pawn)
        {
            Hediff_MentalEtiology tracker = MentalEtiologyUtility.Tracker(pawn, false);
            List<DiseaseRiskInfo> risks = TraumaAPI.GetDiseaseRisks(pawn)
                .Where(r => riskSeverity < 0
                    || MentalDisorderUtility.SeverityStage(r.disease) == riskSeverity).ToList();
            float contentHeight = 92f + Math.Min(20, risks.Count) * 78f;
            Rect outer = new Rect(10f, 46f, size.x - 20f, size.y - 56f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, Math.Max(outer.height, contentHeight));
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            float filterWidth = (view.width - 32f) / 5f;
            for (int index = 0; index < 5; index++)
            {
                int captured = index - 1;
                string label = index == 0
                    ? MMDLocalization.Pick("全部", "All") : SeverityTabLabel(captured);
                if (DrawTabButton(new Rect(8f + index * (filterWidth + 4f), 0f, filterWidth, 30f),
                    label, riskSeverity == captured))
                {
                    riskSeverity = captured;
                    scrollPosition = Vector2.zero;
                }
            }
            float y = 38f;
            Widgets.Label(new Rect(8f, y, view.width - 16f, 38f),
                MMDLocalization.Pick("显示当前创伤可能形成的疾病及条件进度。",
                    "Disorders that may form from current trauma and their requirement progress."));
            y += 44f;
            if (risks.Count == 0)
                Widgets.Label(new Rect(8f, y, view.width - 16f, 30f),
                    MMDLocalization.Pick("当前没有该严重度的疾病风险。",
                        "There is currently no disorder risk at this severity."));
            else
                foreach (DiseaseRiskInfo risk in risks.Take(20))
                    DrawRiskRow(tracker, risk, view.width, ref y);
            Widgets.EndScrollView();
        }

        internal static string SeverityTabLabel(int stage)
        {
            if (stage == 0) return MMDLocalization.Pick("轻度", "Mild");
            if (stage == 1) return MMDLocalization.Pick("中度", "Moderate");
            if (stage == 2) return MMDLocalization.Pick("重度", "Severe");
            return MMDLocalization.Pick("极重", "Extreme");
        }

        internal static bool DrawTabButton(Rect rect, string label, bool selected)
        {
            bool clicked = Widgets.ButtonText(rect, label);
            if (selected)
                Widgets.DrawHighlightSelected(rect);
            return clicked;
        }

        internal static void DrawRecovery(Pawn pawn, Hediff_MentalEtiology tracker, float width, ref float y)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(8f, y, width - 16f, 32f),
                MMDLocalization.Pick("心理恢复", "Psychological recovery"));
            Text.Font = GameFont.Small;
            y += 34f;
            float mood = pawn.needs?.mood?.CurLevelPercentage ?? 0f;
            float progress = tracker == null ? 0f : tracker.HighMoodStabilityProgress;
            Widgets.Label(new Rect(8f, y, width - 16f, 24f),
                MMDLocalization.Pick("当前心情：", "Current mood: ") + mood.ToStringPercent());
            y += 24f;
            Widgets.FillableBar(new Rect(8f, y, width - 16f, 18f), progress);
            y += 22f;
            string status;
            if (tracker == null || tracker.records.Count == 0)
                status = MMDLocalization.Pick("没有需要消除的创伤。", "There is no trauma to recover from.");
            else if (tracker.CurrentHighMoodRecoveryPerDay > 0f)
                status = MMDLocalization.Pick("正在消除创伤：每天", "Recovering trauma: ")
                    + tracker.CurrentHighMoodRecoveryPerDay.ToString("0.#")
                    + MMDLocalization.Pick("点。", " points per day.");
            else if (mood >= 0.7f)
                status = MMDLocalization.Pick("高心情稳定进度：", "High-mood stability: ")
                    + progress.ToStringPercent()
                    + MMDLocalization.Pick("（满3天后开始恢复）", " (recovery begins after 3 days)");
            else if (mood >= 0.6f)
                status = MMDLocalization.Pick("心情不足70%，稳定进度暂停。", "Mood is below 70%; stability progress is paused.");
            else
                status = MMDLocalization.Pick("心情低于60%，稳定进度正在倒退。", "Mood is below 60%; stability progress is receding.");
            Widgets.Label(new Rect(8f, y, width - 16f, 38f), status);
            y += 44f;
        }

        internal static void DrawHeading(string label, float width, ref float y)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(8f, y, width - 16f, 30f), label);
            Text.Font = GameFont.Small;
            y += 32f;
        }

        private static string GroupKey(Pawn pawn, MentalCauseDef cause)
        {
            return pawn.thingIDNumber + ":" + cause.defName;
        }

        private void DrawTraumaGroup(Pawn pawn, TraumaDisplayGroup group, float width, ref float y)
        {
            Rect row = new Rect(4f, y, width - 8f, 76f);
            Widgets.DrawHighlightIfMouseover(row);
            string key = GroupKey(pawn, group.cause);
            bool canExpand = group.records.Count > 1;
            bool expanded = canExpand && expandedGroups.Contains(key);
            if (canExpand && Widgets.ButtonText(new Rect(8f, y, 28f, 24f), expanded ? "▼" : "▶"))
            {
                if (expanded) expandedGroups.Remove(key);
                else expandedGroups.Add(key);
                expanded = !expanded;
            }
            float labelX = canExpand ? 42f : 8f;
            Widgets.Label(new Rect(labelX, y, width * 0.55f, 24f), group.cause.LabelCap);
            Widgets.Label(new Rect(width - 150f, y, 138f, 24f),
                group.Amount.ToString("0.#") + " / " + group.cause.maxAmount.ToString("0.#"));
            y += 25f;
            Widgets.FillableBar(new Rect(8f, y, width - 20f, 14f),
                Mathf.Clamp01(group.Amount / Math.Max(1f, group.cause.maxAmount)));
            y += 18f;
            string details = MMDLocalization.Pick("事件：", "Events: ") + group.Events;
            if (canExpand)
                details += MMDLocalization.Pick("　来源记录：", "  Source records: ") + group.records.Count;
            else if (!group.records[0].context.NullOrEmpty())
                details += MMDLocalization.Pick("　来源：", "  Source: ")
                    + LocalizedContext(group.records[0].context);
            if (group.LastTick > 0)
                details += MMDLocalization.Pick("　距今：", "  Last: ")
                    + ((Find.TickManager.TicksGame - group.LastTick) / 60000f).ToString("0.#")
                    + MMDLocalization.Pick("天", " days ago");
            Widgets.Label(new Rect(8f, y, width - 20f, 30f), details);
            y += 33f;
            if (!expanded) return;
            foreach (MentalCauseRecord record in group.records
                .OrderByDescending(r => r.lastTick))
            {
                string source = record.context.NullOrEmpty()
                    ? MMDLocalization.Pick("未记录来源", "Unrecorded source")
                    : LocalizedContext(record.context);
                string line = "  • " + source + MMDLocalization.Pick("：", ": ")
                    + record.amount.ToString("0.#")
                    + MMDLocalization.Pick("点，", " points, ")
                    + record.eventCount + MMDLocalization.Pick("次", " events");
                Widgets.Label(new Rect(18f, y, width - 30f, 28f), line);
                y += 30f;
            }
        }

        private static string LocalizedContext(string context)
        {
            if (context.NullOrEmpty()) return MMDLocalization.Pick("未记录来源", "Unrecorded source");
            if (context == "MentalBreak") return MMDLocalization.Pick("精神崩溃", "mental break");
            if (context == "MissingPart") return MMDLocalization.Pick("肢体缺失", "missing body part");
            if (context == "Filth") return MMDLocalization.Pick("污秽环境", "filthy environment");
            if (context == "CabinFeverSevere") return MMDLocalization.Pick("重度幽居病", "severe cabin fever");
            if (context == "Death") return MMDLocalization.Pick("死亡经历", "death");
            if (context == "Violence") return MMDLocalization.Pick("暴力经历", "violence");

            Pawn pawn = PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead
                .FirstOrDefault(p => p.ThingID == context);
            if (pawn != null) return pawn.LabelShortCap;
            DamageDef damage = DefDatabase<DamageDef>.GetNamedSilentFail(context);
            if (damage != null) return damage.LabelCap;
            ThingDef thing = DefDatabase<ThingDef>.GetNamedSilentFail(context);
            if (thing != null) return thing.LabelCap;
            BodyPartDef bodyPart = DefDatabase<BodyPartDef>.GetNamedSilentFail(context);
            if (bodyPart != null) return bodyPart.LabelCap;
            HediffDef hediff = DefDatabase<HediffDef>.GetNamedSilentFail(context);
            if (hediff != null) return hediff.LabelCap;
            WeatherDef weather = DefDatabase<WeatherDef>.GetNamedSilentFail(context);
            if (weather != null) return weather.LabelCap;
            return context;
        }

        internal static void DrawRiskRow(Hediff_MentalEtiology tracker, DiseaseRiskInfo risk,
            float width, ref float y)
        {
            Rect row = new Rect(4f, y, width - 8f, 72f);
            Widgets.DrawHighlightIfMouseover(row);
            Widgets.Label(new Rect(8f, y, width * 0.55f, 24f), risk.disease.LabelCap);
            string riskLabel = risk.progress >= 1f
                ? MMDLocalization.Pick("条件已满足", "conditions met")
                : MMDLocalization.Pick("形成进度：", "progress: ") + risk.progress.ToStringPercent();
            Widgets.Label(new Rect(width - 180f, y, 168f, 24f), riskLabel);
            y += 25f;
            Widgets.FillableBar(new Rect(8f, y, width - 20f, 12f), risk.progress);
            y += 16f;
            string requirements = string.Join(MMDLocalization.Pick("；", "; "), risk.requirements.Select(r =>
                r.cause.label + " " + (tracker == null ? 0f : tracker.Amount(r.cause)).ToString("0.#")
                + "/" + r.minAmount.ToString("0.#")).ToArray());
            if (risk.progress >= 1f)
                requirements += MMDLocalization.Pick("；每次检查概率：", "; chance per check: ")
                    + risk.chance.ToStringPercent();
            Widgets.Label(new Rect(8f, y, width - 20f, 30f), requirements);
            y += 35f;
        }
    }

}
