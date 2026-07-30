using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

namespace MoreMentalDisorders
{
    // Soft integration: this assembly never references RimTalk at compile time.
    // When RimTalk is present, its public context API receives an extra pawn section.
    public static class RimTalkCompatibility
    {
        private const string RimTalkPackageId = "cj.rimtalk";
        private const string ModId = "ender.morementaldisorders";

        public static void TryRegister()
        {
            if (!LoadedModManager.RunningModsListForReading.Any(m =>
                string.Equals(m.PackageId, RimTalkPackageId,
                    StringComparison.OrdinalIgnoreCase))) return;

            try
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                if (assembly == null) return;

                Type api = assembly.GetType("RimTalk.API.RimTalkPromptAPI");
                Type pawnCategories = assembly.GetType("RimTalk.API.ContextCategories+Pawn");
                Type positions = assembly.GetType(
                    "RimTalk.API.ContextHookRegistry+InjectPosition");
                MethodInfo inject = api?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "InjectPawnSection"
                        && m.GetParameters().Length == 6);
                object health = pawnCategories?.GetField("Health",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                object after = positions == null ? null : Enum.Parse(positions, "After");
                if (inject == null || health == null || after == null) return;

                Func<Pawn, string> provider = BuildPsychologicalContext;
                inject.Invoke(null, new object[]
                {
                    ModId,
                    "Mental disorders",
                    health,
                    after,
                    provider,
                    100
                });
                Log.Message("[边缘精神疾病] RimTalk compatibility registered.");
            }
            catch (Exception exception)
            {
                Log.Warning("[边缘精神疾病] RimTalk compatibility could not be registered: "
                    + exception.GetBaseException().Message);
            }
        }

        private static string BuildPsychologicalContext(Pawn pawn)
        {
            if (pawn == null) return null;
            var disorders = pawn.Disorders();
            if (disorders.Count == 0) return null;

            StringBuilder text = new StringBuilder();
            if (MMDLocalization.English)
            {
                text.Append("Psychological condition (important dialogue direction): ");
                AppendDisorders(text, disorders, true);
                text.Append(" Portray these conditions naturally through the pawn's priorities, "
                    + "interpretations, emotional reactions, word choice, and current phase. "
                    + "Do not make the pawn repeatedly announce a diagnosis and do not invent "
                    + "symptoms absent from the descriptions. ");
                AppendBias(text, pawn, disorders[0], true);
            }
            else
            {
                text.Append("精神状态（重要对话指导）：");
                AppendDisorders(text, disorders, false);
                text.Append(" 请通过该角色的关注重点、对事件的理解、情绪反应、措辞和当前阶段，"
                    + "自然表现疾病影响；不要让角色反复自报诊断，也不要添加描述中不存在的症状。");
                AppendBias(text, pawn, disorders[0], false);
            }
            return text.ToString();
        }

        private static void AppendDisorders(StringBuilder text,
            System.Collections.Generic.List<Hediff_MentalDisorder> disorders, bool english)
        {
            for (int i = 0; i < disorders.Count; i++)
            {
                Hediff_MentalDisorder disorder = disorders[i];
                if (i > 0) text.Append(english ? " Also: " : " 另有：");
                text.Append(disorder.LabelBase).Append(" (")
                    .Append(MentalDisorderUtility.SeverityLabel(disorder.def)).Append("). ");
                text.Append(disorder.Description.Replace("\n", " "));
                if (disorder.pawn.InMentalState)
                    text.Append(english
                        ? " The condition is currently erupting under pressure."
                        : " 该疾病目前正在高压下病发。");
            }
        }

        private static void AppendBias(StringBuilder text, Pawn pawn,
            Hediff_MentalDisorder disorder, bool english)
        {
            int offset = MentalDisorderUtility.UniversalOpinionOffset(disorder.def);
            string degree = english
                ? (offset <= -20 ? "strong" : offset <= -15 ? "marked"
                    : offset <= -10 ? "moderate" : "mild")
                : (offset <= -20 ? "强烈" : offset <= -15 ? "明显"
                    : offset <= -10 ? "中等" : "轻微");
            if (english)
            {
                text.Append(" Social prejudice: other speaking pawns without a mental disorder "
                    + "generally hold " + degree + " prejudice toward " + pawn.LabelShortCap
                    + " (game opinion modifier " + offset
                    + ", regarded as \"a lunatic\"). Express it contextually through distrust, "
                    + "discomfort, condescension, avoidance, or guarded sympathy; do not force "
                    + "an explicit insult into every line.");
            }
            else
            {
                text.Append(" 社会偏见：参与对话且未患精神疾病的角色通常对")
                    .Append(pawn.LabelShortCap).Append("抱有").Append(degree)
                    .Append("偏见（游戏评价修正").Append(offset)
                    .Append("，视其为“疯子”）。根据情境通过不信任、不适、居高临下、回避或带戒心的同情表现，")
                    .Append("不要强迫每句话都直接辱骂。");
            }
        }
    }
}
