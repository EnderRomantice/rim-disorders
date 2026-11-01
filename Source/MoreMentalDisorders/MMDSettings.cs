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

        public List<bool> psylinkEnabledBySeverity = new List<bool> { false, false, false, true };
        public List<int> psylinkLevelsBySeverity = new List<int> { 1, 1, 1, 6 };
        public float initialDiseaseChancePercent = 69.5f;
        public List<float> severityWeights = DefaultSeverityWeights.ToList();
        public Dictionary<string, float> diseaseWeights = new Dictionary<string, float>();

        public bool PsylinkEnabled(int stage)
        {
            EnsurePsylinkSettings();
            return stage >= 0 && stage < 4 && psylinkEnabledBySeverity[stage];
        }

        public int ConfiguredPsylinkLevel(int stage)
        {
            EnsurePsylinkSettings();
            return stage >= 0 && stage < 4 ? psylinkLevelsBySeverity[stage] : 1;
        }

        public int PsylinkLevel(int stage)
        {
            return PsylinkEnabled(stage) ? ConfiguredPsylinkLevel(stage) : 0;
        }

        public void SetPsylinkGrant(int stage, bool enabled, int level)
        {
            if (stage < 0 || stage >= 4) return;
            EnsurePsylinkSettings();
            psylinkEnabledBySeverity[stage] = enabled;
            psylinkLevelsBySeverity[stage] = Math.Max(1, Math.Min(6, level));
        }

        public void ResetPsylinkSettings()
        {
            psylinkEnabledBySeverity = new List<bool> { false, false, false, true };
            psylinkLevelsBySeverity = new List<int> { 1, 1, 1, 6 };
        }

        private void EnsurePsylinkSettings()
        {
            // Older settings lack these lists; adopt the new extreme-only default.
            if (psylinkEnabledBySeverity == null) psylinkEnabledBySeverity = new List<bool>();
            if (psylinkLevelsBySeverity == null) psylinkLevelsBySeverity = new List<int>();
            while (psylinkEnabledBySeverity.Count < 4)
                psylinkEnabledBySeverity.Add(psylinkEnabledBySeverity.Count == 3);
            while (psylinkLevelsBySeverity.Count < 4)
                psylinkLevelsBySeverity.Add(psylinkLevelsBySeverity.Count == 3 ? 6 : 1);
            if (psylinkEnabledBySeverity.Count > 4)
                psylinkEnabledBySeverity.RemoveRange(4, psylinkEnabledBySeverity.Count - 4);
            if (psylinkLevelsBySeverity.Count > 4)
                psylinkLevelsBySeverity.RemoveRange(4, psylinkLevelsBySeverity.Count - 4);
            for (int i = 0; i < 4; i++)
                psylinkLevelsBySeverity[i] = Math.Max(1, Math.Min(6, psylinkLevelsBySeverity[i]));
        }

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
            Scribe_Collections.Look(ref psylinkEnabledBySeverity, "psylinkEnabledBySeverity", LookMode.Value);
            Scribe_Collections.Look(ref psylinkLevelsBySeverity, "psylinkLevelsBySeverity", LookMode.Value);
            Scribe_Values.Look(ref initialDiseaseChancePercent,
                "initialDiseaseChancePercent", 69.5f);
            Scribe_Collections.Look(ref severityWeights, "severityWeights", LookMode.Value);
            Scribe_Collections.Look(ref diseaseWeights, "diseaseWeights", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (diseaseWeights == null) diseaseWeights = new Dictionary<string, float>();
                initialDiseaseChancePercent = Mathf.Clamp(initialDiseaseChancePercent, 0f, 100f);
                EnsureSeverityWeights();
                EnsurePsylinkSettings();
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
            float mainWidth = (inRect.width - 8f) / 3f;
            string[] labels = {
                MMDLocalization.Pick("总体概率", "Overall weights"),
                MMDLocalization.Pick("具体配置", "Disease weights"),
                MMDLocalization.Pick("灵能赠送", "Psylink grants")
            };
            for (int page = 0; page < labels.Length; page++)
            {
                if (ITab_Pawn_Trauma.DrawTabButton(
                    new Rect(inRect.x + page * (mainWidth + 4f), inRect.y, mainWidth, 34f),
                    labels[page], mainPage == page))
                {
                    mainPage = page;
                    scrollPosition = Vector2.zero;
                }
            }
            Rect body = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
            if (mainPage == 0) DrawOverall(body);
            else if (mainPage == 1) DrawSpecific(body);
            else DrawPsylinks(body);
        }

        private void DrawOverall(Rect rect)
        {
            Rect view = new Rect(0f, 0f, rect.width - 20f, Mathf.Max(rect.height, 600f));
            Widgets.BeginScrollView(rect, ref scrollPosition, view);
            DrawOverallContents(view);
            Widgets.EndScrollView();
        }

        private void DrawOverallContents(Rect rect)
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

        private void DrawPsylinks(Rect rect)
        {
            Rect view = new Rect(0f, 0f, rect.width - 20f, Mathf.Max(rect.height, 530f));
            Widgets.BeginScrollView(rect, ref scrollPosition, view);
            Widgets.Label(new Rect(0f, 0f, view.width, 64f),
                MMDLocalization.Pick("默认仅极重疾病赠送6级灵能。每档可独立开启，并设置最低灵能等级（1至6级）。关闭或调低不会移除已有等级。",
                    "Only extreme disorders grant level 6 by default. Enable each severity independently and choose a minimum level (1–6). Disabling or lowering a grant preserves existing levels."));
            float y = 76f;
            for (int stage = 0; stage < 4; stage++)
            {
                bool enabled = Settings.PsylinkEnabled(stage);
                int level = Settings.ConfiguredPsylinkLevel(stage);
                Widgets.CheckboxLabeled(new Rect(0f, y, view.width, 28f),
                    ITab_Pawn_Trauma.SeverityTabLabel(stage)
                        + MMDLocalization.Pick("：赠送灵能", ": grant psylink"), ref enabled);
                y += 32f;
                Widgets.Label(new Rect(0f, y, view.width, 24f),
                    enabled ? MMDLocalization.Pick("最低灵能等级：", "Minimum psylink level: ") + level
                        : MMDLocalization.Pick("不赠送", "No grant"));
                y += 24f;
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && enabled;
                level = Mathf.RoundToInt(Widgets.HorizontalSlider(
                    new Rect(0f, y, view.width - 8f, 24f), level, 1f, 6f,
                    false, null, null, null, 1f));
                GUI.enabled = oldEnabled;
                Settings.SetPsylinkGrant(stage, enabled, level);
                y += 36f;
            }
            if (Widgets.ButtonText(new Rect(0f, y + 12f, 240f, 32f),
                MMDLocalization.Pick("恢复默认：仅极重6级", "Reset: extreme level 6 only")))
                Settings.ResetPsylinkSettings();
            Widgets.EndScrollView();
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
