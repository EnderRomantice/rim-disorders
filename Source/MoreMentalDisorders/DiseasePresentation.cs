using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace MoreMentalDisorders
{
    [StaticConstructorOnStartup]
    public static class DiseasePresentation
    {
        private static readonly Dictionary<HediffDef, string> summaries = new Dictionary<HediffDef, string>();

        static DiseasePresentation()
        {
            foreach (HediffDef def in MentalDisorderUtility.AllDefs.Where(d => d != null))
            {
                summaries[def] = def.description;
                def.description = BuildDetails(def, def.description);
            }
        }

        public static string Summary(HediffDef def)
        {
            string result;
            return def != null && summaries.TryGetValue(def, out result) ? result : def?.description;
        }

        private static string BuildDetails(HediffDef def, string summary)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(summary);
            float positive;
            float negative;
            MentalDisorderUtility.MoodMemoryFactors(def, out positive, out negative);
            text.AppendLine(MMDLocalization.Pick("\n心情记忆", "\nMood memories"));
            text.AppendLine(MMDLocalization.Pick("• 正面心情记忆持续：", "• Positive memory duration: ") + FormatDuration(positive));
            text.AppendLine(MMDLocalization.Pick("• 负面心情记忆持续：", "• Negative memory duration: ") + FormatDuration(negative));
            if (def.stages != null && def.stages.Count > 0)
            {
                text.AppendLine(MMDLocalization.Pick("\n能力影响", "\nStat effects"));
                for (int i = 0; i < def.stages.Count; i++)
                {
                    HediffStage stage = def.stages[i];
                    if (def.stages.Count > 1) text.AppendLine(MMDLocalization.Pick("阶段 ", "Stage ") + (i + 1) + ":");
                    if (stage.statFactors != null)
                        foreach (StatModifier modifier in stage.statFactors)
                            text.AppendLine("• " + modifier.stat.LabelCap + ": ×" + modifier.value.ToString("0.##"));
                }
            }
            DiseaseAcquisitionExtension recipe = def.GetModExtension<DiseaseAcquisitionExtension>();
            if (recipe != null && recipe.specialEffects != null && recipe.specialEffects.Count > 0)
            {
                if (!MMDLocalization.English)
                {
                    text.AppendLine("\n特殊机制");
                    foreach (string effect in recipe.specialEffects) text.AppendLine("• " + effect);
                }
            }
            if (MMDLocalization.English)
            {
                if (def == MMDDefOf.MMD_DependentPersonality)
                    text.AppendLine("\nDependency\n• Prefers a spouse or lover, otherwise the most trusted faction mate.\n• Mood +8 nearby and −12 when separated, missing, or dead.\n• Opinion of the dependent person +40.");
                if (def == MMDDefOf.MMD_ParanoidDelusion)
                    text.AppendLine("\nEpisode and psychic effect\n• Mind-kill has unlimited range and no cooldown, but requires line of sight and an interruptible 2-second cast. A human target must have previously harmed the patient or be rated at −40 or lower; hostile non-humans are also valid.\n• During an episode, pursues the lowest-opinion valid target.");
                else if (def == MMDDefOf.MMD_MajorDepression)
                    text.AppendLine("\nEpisode and psychic effect\n• Psycast cost and neural heat gain: ×0.5; neural heat recovery: ×0.5; no natural psyfocus decay.\n• An episode causes a genuine suicide attempt.");
                else if (def == MMDDefOf.MMD_Schizophrenia)
                    text.AppendLine("\nIdentity\n• Receives one fixed delusional identity with distinct skills, combat, social, research, work refusals, and episode behavior.");
                else if (def == MMDDefOf.MMD_Mania)
                    text.AppendLine("\nEpisode and combat\n• Psycast cost and neural heat gain: ×2; neural heat capacity: ×0.5; ability cooldowns: 5 seconds.\n• Melee attack interval: ×0.1; attacks cause no movement slowdown; indiscriminate attacks include buildings.\n• During an episode, pain and movement slowdown are ignored.");
                else if (def == MMDDefOf.MMD_Cotard)
                    text.AppendLine("\nDenial of death\n• Immediately resurrects after death if the corpse still exists.");
                if (def == MMDDefOf.MMD_Hyperthymesia)
                    text.AppendLine("\nPerfect memory\n• Skills never decay and all positive and negative mood memories are permanent.");
            }
            else if (def == MMDDefOf.MMD_DependentPersonality)
                text.AppendLine("\n依赖关系\n• 优先依赖配偶或恋人；没有伴侣时依赖同阵营中评价最高的人。\n"
                    + "• 依赖对象在身边时心情+8；分离、失联或死亡时心情−12。\n"
                    + "• 患者对依赖对象额外评价+40；对象失效后会重新建立依赖。");
            if (def == MMDDefOf.MMD_ParanoidDelusion)
                text.AppendLine("\n病发与灵能\n• 主动心灵宰杀无距离限制且无冷却，但需要视线和可被打断的2秒前摇；人类目标必须曾伤害患者，或被患者评价低于或等于−40，敌对非人类也可成为目标。\n"
                    + "• 病发时优先追杀评价低于70者；目标不唯一时选择评价最低者。");
            else if (def == MMDDefOf.MMD_MajorDepression)
                text.AppendLine("\n病发与灵能\n• 精神力消耗与精神熵获取：×0.5，精神熵消退：×0.5，精神力不自然降低。\n"
                    + "• 病发时会真实尝试自杀。");
            else if (def == MMDDefOf.MMD_Schizophrenia)
                text.AppendLine("\n病发与人格\n• 生成时固定获得古代灵能大师、特殊部队军官、旧时代总统、隐世剑客或首席科学家人格。\n"
                    + "• 人格决定技能、战斗、社交或研究加成以及病发行为；角色详情会显示当前人格。");
            else if (def == MMDDefOf.MMD_Mania)
                text.AppendLine("\n病发与战斗\n• 灵能消耗与精神熵获取：×2，精神熵上限：×0.5；所有技能冷却统一为5秒。\n"
                    + "• 近战攻击间隔：×0.1，攻击不会减速；病发时无差别攻击人、动物与建筑。\n"
                    + "• 病发期间疼痛感知归零，不会因伤势、疼痛或其他移动减速降到狂躁基础速度以下。");
            else if (def == MMDDefOf.MMD_Cotard)
                text.AppendLine("\n死亡否定\n• 死亡后只要尸体仍存在便立即复活；这是实际复活，不是文字描述。");
            if (def == MMDDefOf.MMD_Hyperthymesia)
                text.AppendLine("\n超常记忆\n• 技能经验不会自然衰减，技能等级不会因自然遗忘下降。\n"
                    + "• 所有正面与负面心情记忆均不会随时间消失。");
            text.AppendLine(MMDLocalization.Pick("\n病因", "\nEtiology"));
            text.Append(MentalEtiologyUtility.DescribeRecipe(def, recipe));
            return text.ToString().TrimEndNewlines();
        }

        private static string FormatDuration(float factor)
        {
            return factor < 0f ? MMDLocalization.Pick("永久", "permanent")
                : MMDLocalization.Pick("原来的 ×", "×") + factor.ToString("0.##");
        }
    }
}
