using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MoreMentalDisorders
{
    public class MMDSettings : ModSettings
    {
        private static readonly float[] DefaultSeverityWeights =
            { 79.13669f, 14.38849f, 5.755396f, 0.7194245f };

        public float initialDiseaseChancePercent = 69.5f;
        public List<float> severityWeights = DefaultSeverityWeights.ToList();
        public Dictionary<string, float> diseaseWeights = new Dictionary<string, float>();

        public float SeverityWeight(int stage)
        {
            EnsureSeverityWeights();
            return stage >= 0 && stage < 4 ? severityWeights[stage] : 0f;
        }

        public void SetSeverityWeight(int stage, float value)
        {
            EnsureSeverityWeights();
            Redistribute(severityWeights, stage, value);
        }

        public float DiseaseWeight(HediffDef disease)
        {
            if (disease == null) return 0f;
            float value;
            if (diseaseWeights.TryGetValue(disease.defName, out value)) return Mathf.Max(0f, value);
            List<HediffDef> group = DiseasesAtStage(MentalDisorderUtility.SeverityStage(disease));
            return group.Count == 0 ? 0f : 100f / group.Count;
        }

        public void SetDiseaseWeight(HediffDef disease, float value)
        {
            if (disease == null) return;
            List<HediffDef> group = DiseasesAtStage(MentalDisorderUtility.SeverityStage(disease));
            if (group.Count == 0) return;
            List<float> weights = group.Select(DiseaseWeight).ToList();
            int index = group.IndexOf(disease);
            Redistribute(weights, index, value);
            for (int i = 0; i < group.Count; i++)
                diseaseWeights[group[i].defName] = weights[i];
        }

        public void ResetSeverityWeights()
        {
            severityWeights = DefaultSeverityWeights.ToList();
        }

        public void ResetDiseaseStage(int stage)
        {
            foreach (HediffDef disease in DiseasesAtStage(stage))
                diseaseWeights.Remove(disease.defName);
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref initialDiseaseChancePercent,
                "initialDiseaseChancePercent", 69.5f);
            Scribe_Collections.Look(ref severityWeights, "severityWeights", LookMode.Value);
            Scribe_Collections.Look(ref diseaseWeights, "diseaseWeights", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (diseaseWeights == null) diseaseWeights = new Dictionary<string, float>();
                initialDiseaseChancePercent = Mathf.Clamp(initialDiseaseChancePercent, 0f, 100f);
                EnsureSeverityWeights();
                Normalize(severityWeights);
            }
            base.ExposeData();
        }

        private void EnsureSeverityWeights()
        {
            if (severityWeights == null || severityWeights.Count != 4)
                severityWeights = DefaultSeverityWeights.ToList();
        }

        private static void Redistribute(List<float> weights, int changedIndex, float requested)
        {
            if (weights == null || weights.Count == 0 || changedIndex < 0
                || changedIndex >= weights.Count) return;
            requested = Mathf.Clamp(requested, 0f, 100f);
            float remaining = 100f - requested;
            float otherTotal = weights.Where((w, i) => i != changedIndex).Sum();
            weights[changedIndex] = requested;
            if (weights.Count == 1) { weights[0] = 100f; return; }
            if (otherTotal <= 0.0001f)
            {
                float each = remaining / (weights.Count - 1);
                for (int i = 0; i < weights.Count; i++)
                    if (i != changedIndex) weights[i] = each;
            }
            else
            {
                for (int i = 0; i < weights.Count; i++)
                    if (i != changedIndex) weights[i] = weights[i] / otherTotal * remaining;
            }
            Normalize(weights);
        }

        private static void Normalize(List<float> weights)
        {
            if (weights == null || weights.Count == 0) return;
            float total = weights.Sum();
            if (total <= 0.0001f)
            {
                float each = 100f / weights.Count;
                for (int i = 0; i < weights.Count; i++) weights[i] = each;
                return;
            }
            for (int i = 0; i < weights.Count; i++)
                weights[i] = Mathf.Max(0f, weights[i]) / total * 100f;
        }

        public static List<HediffDef> DiseasesAtStage(int stage)
        {
            return DefDatabase<HediffDef>.AllDefsListForReading
                .Where(d => (MentalDisorderUtility.AllDefs.Contains(d)
                    || d.GetModExtension<DiseaseAcquisitionExtension>() != null)
                    && MentalDisorderUtility.SeverityStage(d) == stage)
                .OrderBy(d => d.label).ToList();
        }
    }

    public class MMDMod : Mod
    {
        public static MMDSettings Settings;
        private int mainPage;
        private int severityPage;
        private Vector2 scrollPosition;

        public MMDMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MMDSettings>();
        }

        public override string SettingsCategory()
        {
            return MMDLocalization.Pick("边缘精神疾病", "Rim Mental Disorders");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float mainWidth = (inRect.width - 4f) / 2f;
            if (ITab_Pawn_Trauma.DrawTabButton(new Rect(inRect.x, inRect.y, mainWidth, 34f),
                MMDLocalization.Pick("总体概率", "Overall weights"), mainPage == 0))
            {
                mainPage = 0;
                scrollPosition = Vector2.zero;
            }
            if (ITab_Pawn_Trauma.DrawTabButton(new Rect(inRect.x + mainWidth + 4f, inRect.y,
                mainWidth, 34f), MMDLocalization.Pick("具体配置", "Disease weights"), mainPage == 1))
            {
                mainPage = 1;
                scrollPosition = Vector2.zero;
            }
            Rect body = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
            if (mainPage == 0) DrawOverall(body);
            else DrawSpecific(body);
        }

        private void DrawOverall(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 42f),
                MMDLocalization.Pick("初始患病概率决定新生成角色是否患病；严重度权重决定患病后的总体分布。",
                    "Initial disease chance controls whether a newly generated pawn has a disorder; severity weights control its distribution."));
            float y = rect.y + 50f;

            Widgets.Label(new Rect(rect.x, y, rect.width * 0.7f, 24f),
                MMDLocalization.Pick("初始患病概率", "Initial disease chance"));
            Widgets.Label(new Rect(rect.xMax - 90f, y, 86f, 24f),
                Settings.initialDiseaseChancePercent.ToString("0.#") + "%");
            y += 26f;
            float initialChance = Widgets.HorizontalSlider(
                new Rect(rect.x, y, rect.width - 8f, 24f),
                Settings.initialDiseaseChancePercent, 0f, 100f,
                false, null, null, null, 1f);
            if (Mathf.Abs(initialChance - Settings.initialDiseaseChancePercent) > 0.001f)
                Settings.initialDiseaseChancePercent = initialChance;
            y += 42f;

            Widgets.Label(new Rect(rect.x, y, rect.width, 38f),
                MMDLocalization.Pick("调整任意严重度时，其他项目会自动重新分配，合计始终为100%。",
                    "Changing one severity redistributes the others so the total always remains 100%."));
            y += 42f;
            for (int stage = 0; stage < 4; stage++)
            {
                float oldValue = Settings.SeverityWeight(stage);
                Widgets.Label(new Rect(rect.x, y, rect.width * 0.5f, 24f),
                    ITab_Pawn_Trauma.SeverityTabLabel(stage));
                Widgets.Label(new Rect(rect.xMax - 90f, y, 86f, 24f), oldValue.ToString("0.##") + "%");
                y += 24f;
                float newValue = Widgets.HorizontalSlider(new Rect(rect.x, y, rect.width - 8f, 24f),
                    oldValue, 0f, 100f, false, null, null, null, 1f);
                if (Mathf.Abs(newValue - oldValue) > 0.001f)
                    Settings.SetSeverityWeight(stage, newValue);
                y += 38f;
            }
            Widgets.Label(new Rect(rect.x, y, rect.width, 26f),
                MMDLocalization.Pick("权重合计：100%", "Total weight: 100%"));
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 36f, 180f, 32f),
                MMDLocalization.Pick("恢复默认分布", "Restore defaults")))
                Settings.ResetSeverityWeights();
        }

        private void DrawSpecific(Rect rect)
        {
            float tabWidth = (rect.width - 12f) / 4f;
            for (int stage = 0; stage < 4; stage++)
            {
                int captured = stage;
                if (ITab_Pawn_Trauma.DrawTabButton(
                    new Rect(rect.x + stage * (tabWidth + 4f), rect.y, tabWidth, 34f),
                    ITab_Pawn_Trauma.SeverityTabLabel(stage), severityPage == stage))
                {
                    severityPage = captured;
                    scrollPosition = Vector2.zero;
                }
            }
            List<HediffDef> diseases = MMDSettings.DiseasesAtStage(severityPage);
            Rect outer = new Rect(rect.x, rect.y + 44f, rect.width, rect.height - 88f);
            Rect view = new Rect(0f, 0f, outer.width - 18f,
                Mathf.Max(outer.height, 56f + diseases.Count * 56f));
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            float y = 0f;
            Widgets.Label(new Rect(4f, y, view.width - 8f, 42f),
                MMDLocalization.Pick("本严重度内的疾病使用相对权重；调整一项时，其余疾病自动重新分配，合计始终为100%。",
                    "Diseases in this severity use relative weights. Changing one redistributes the others so the total remains 100%."));
            y += 48f;
            foreach (HediffDef disease in diseases)
            {
                float oldValue = Settings.DiseaseWeight(disease);
                Widgets.Label(new Rect(4f, y, view.width * 0.6f, 24f), disease.LabelCap);
                Widgets.Label(new Rect(view.width - 88f, y, 80f, 24f), oldValue.ToString("0.##") + "%");
                y += 24f;
                float newValue = Widgets.HorizontalSlider(new Rect(4f, y, view.width - 12f, 24f),
                    oldValue, 0f, 100f, false, null, null, null, 1f);
                if (Mathf.Abs(newValue - oldValue) > 0.001f)
                    Settings.SetDiseaseWeight(disease, newValue);
                y += 32f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 36f, 210f, 32f),
                MMDLocalization.Pick("重置当前严重度权重", "Reset current severity")))
                Settings.ResetDiseaseStage(severityPage);
        }
    }

    public static class MMDChanceSettings
    {
        private static readonly float[] DefaultSeverityWeights =
            { 79.13669f, 14.38849f, 5.755396f, 0.7194245f };

        public static float SeverityWeight(int stage)
        {
            return MMDMod.Settings == null ? DefaultSeverityWeights[Mathf.Clamp(stage, 0, 3)]
                : MMDMod.Settings.SeverityWeight(stage);
        }

        public static float InitialDiseaseChance
        {
            get
            {
                return MMDMod.Settings == null ? 0.695f
                    : Mathf.Clamp01(MMDMod.Settings.initialDiseaseChancePercent / 100f);
            }
        }

        public static float DiseaseWeight(HediffDef disease)
        {
            if (disease == null) return 0f;
            List<HediffDef> group = MMDSettings.DiseasesAtStage(
                MentalDisorderUtility.SeverityStage(disease));
            return MMDMod.Settings == null
                ? (group.Count == 0 ? 0f : 100f / group.Count)
                : MMDMod.Settings.DiseaseWeight(disease);
        }

        public static float ChanceMultiplier(HediffDef disease)
        {
            if (disease == null) return 1f;
            int stage = MentalDisorderUtility.SeverityStage(disease);
            float severityBase = DefaultSeverityWeights[Mathf.Clamp(stage, 0, 3)];
            List<HediffDef> group = MMDSettings.DiseasesAtStage(stage);
            float diseaseBase = group.Count == 0 ? 100f : 100f / group.Count;
            return severityBase <= 0f || diseaseBase <= 0f ? 0f
                : SeverityWeight(stage) / severityBase * DiseaseWeight(disease) / diseaseBase;
        }
    }
}
