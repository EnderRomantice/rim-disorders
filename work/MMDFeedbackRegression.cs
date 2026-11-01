using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using MoreMentalDisorders;
using RimWorld;
using Verse;
using Verse.AI;

// Focused managed-runtime checks; no game saves or map state are needed.
internal static class MMDFeedbackRegression
{
    private static int checks;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        checks++;
        Console.WriteLine("PASS: " + name);
    }
    private static T Empty<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    private static Pawn PawnAtStage(DevelopmentalStage stage)
    {
        Pawn pawn = Empty<Pawn>();
        pawn.def = Empty<ThingDef>();
        LifeStageAge lifeStage = Empty<LifeStageAge>();
        lifeStage.def = new LifeStageDef { developmentalStage = stage };
        pawn.def.race = new RaceProperties {
            intelligence = Intelligence.Humanlike,
            lifeStageAges = new List<LifeStageAge> { lifeStage } };
        pawn.ageTracker = Empty<Pawn_AgeTracker>();
        AccessTools.Field(typeof(Pawn_AgeTracker), "pawn").SetValue(pawn.ageTracker, pawn);
        AccessTools.Field(typeof(Pawn_AgeTracker), "cachedLifeStageIndex").SetValue(pawn.ageTracker, 0);
        pawn.health = Empty<Pawn_HealthTracker>();
        pawn.health.hediffSet = new HediffSet(pawn);
        return pawn;
    }
    public static int Main()
    {
        // In the game the DefOf binder sets this while initializing these fields.
        AccessTools.Field(typeof(DefOfHelper), "bindingNow").SetValue(null, true);
        foreach (FieldInfo field in typeof(MMDDefOf).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (typeof(Def).IsAssignableFrom(field.FieldType))
            {
                Def def = (Def)Activator.CreateInstance(field.FieldType);
                def.defName = field.Name;
                field.SetValue(null, def);
            }
        AccessTools.Field(typeof(DefOfHelper), "bindingNow").SetValue(null, false);
        return RunChecks();
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int RunChecks()
    {
        MentalStateDef berserk = new MentalStateDef { defName = "Berserk" };
        MentalStateDef giggle = new MentalStateDef { defName = "Giggling" };
        MentalStateDef cry = new MentalStateDef { defName = "Crying" };
        DefDatabase<MentalBreakDef>.Add(new MentalBreakDef { defName = "Berserk", mentalState = berserk });
        Check(MentalDisorderUtility.IsDisorderMentalBreak(berserk), "registered break is retained");
        Check(MentalDisorderUtility.IsDisorderMentalBreak(MMDDefOf.MMD_PanicEpisode), "mod panic is retained");
        Check(!MentalDisorderUtility.IsDisorderMentalBreak(giggle), "giggling is not a break");
        Check(!MentalDisorderUtility.IsDisorderMentalBreak(cry), "crying is not a break");
        Check(!MentalDisorderUtility.IsDisorderMentalBreak(new MentalStateDef { defName = "Roaming" }), "ordinary state is not a break");
        Check(!MentalDisorderUtility.IsDisorderMentalBreak(null), "null state is safe");
        Pawn baby = PawnAtStage(DevelopmentalStage.Baby);
        Pawn newborn = PawnAtStage(DevelopmentalStage.Newborn);
        Pawn child = PawnAtStage(DevelopmentalStage.Child);
        Pawn adult = PawnAtStage(DevelopmentalStage.Adult);
        Check(!MentalDisorderUtility.EligibleForMentalDisorders(baby), "baby excluded");
        Check(!MentalDisorderUtility.EligibleForMentalDisorders(newborn), "newborn excluded");
        Check(!MentalDisorderUtility.IsInfant(child), "child is not treated as an infant");
        Check(!MentalDisorderUtility.IsInfant(adult), "adult is not treated as an infant");
        MentalState_Recover_MMDPatch.Postfix(new MentalState { pawn = baby, def = giggle });
        MentalState_Recover_MMDPatch.Postfix(new MentalState { pawn = baby, def = berserk });
        MentalState_Recover_MMDPatch.Postfix(new MentalState { pawn = adult, def = cry });
        Check(baby.health.hediffSet.hediffs.Count == 0 && adult.health.hediffSet.hediffs.Count == 0,
            "ignored recoveries create no trauma tracker");
        Check(MentalEtiologyUtility.Tracker(baby, true) == null, "baby trauma cannot be created");
        Insomnia_Sleep_MMDPatch.Postfix(null, baby, 1);
        Check(true, "baby sleep exits before job interruption");
        MMDMod.Settings = new MMDSettings();
        Check(MentalDisorderUtility.AllDefs.All(d => MentalDisorderUtility.RequiredPsylinkLevel(d)
            == (MentalDisorderUtility.SeverityStage(d) == 3 ? 6 : 0)), "defaults grant level 6 only to extreme disorders");
        MMDMod.Settings = null;
        Check(MentalDisorderUtility.RequiredPsylinkLevel(MMDDefOf.MMD_Mania) == 6
            && MentalDisorderUtility.RequiredPsylinkLevel(MMDDefOf.MMD_PTSD) == 0,
            "settings-not-loaded fallback is extreme only");
        MMDMod.Settings = new MMDSettings();
        for (int stage = 0; stage < 4; stage++) MMDMod.Settings.SetPsylinkGrant(stage, true, stage + 1);
        Check(MentalDisorderUtility.AllDefs.All(d => MentalDisorderUtility.RequiredPsylinkLevel(d)
            == MentalDisorderUtility.SeverityStage(d) + 1), "all disorders follow their severity's custom level");
        MMDMod.Settings.SetPsylinkGrant(3, false, 4);
        Check(MentalDisorderUtility.RequiredPsylinkLevel(MMDDefOf.MMD_Mania) == 0
            && MentalDisorderUtility.RequiredPsylinkLevel(MMDDefOf.MMD_PTSD) == 3,
            "extreme can be disabled independently of other severities");
        Check(MMDMod.Settings.ConfiguredPsylinkLevel(3) == 4, "disabled severity remembers its chosen level");
        MMDMod.Settings.SetPsylinkGrant(3, true, 2);
        Check(MentalDisorderUtility.PsylinkGrantDescription(MMDDefOf.MMD_Schizophrenia).EndsWith("2"),
            "identity description follows custom level");
        MMDMod.Settings.SetPsylinkGrant(0, true, 99);
        MMDMod.Settings.SetPsylinkGrant(1, true, -3);
        Check(MMDMod.Settings.PsylinkLevel(0) == 6 && MMDMod.Settings.PsylinkLevel(1) == 1,
            "grant levels stay within 1 through 6");
        MMDMod.Settings.ResetPsylinkSettings();
        Check(Enumerable.Range(0, 4).All(i => MMDMod.Settings.PsylinkLevel(i) == (i == 3 ? 6 : 0)),
            "reset restores extreme-only grants");
        MMDMod.Settings.psylinkEnabledBySeverity = null;
        MMDMod.Settings.psylinkLevelsBySeverity = null;
        Check(Enumerable.Range(0, 4).All(i => MMDMod.Settings.PsylinkLevel(i) == (i == 3 ? 6 : 0)),
            "older settings without severity lists get the new defaults");
        MMDMod.Settings.psylinkEnabledBySeverity = new List<bool> { true };
        MMDMod.Settings.psylinkLevelsBySeverity = new List<int> { 5 };
        Check(MMDMod.Settings.PsylinkLevel(0) == 5 && MMDMod.Settings.PsylinkLevel(3) == 6,
            "partial settings preserve existing choices and fill missing severities");
        Check(MMDMod.Settings.PsylinkLevel(-1) == 0 && MMDMod.Settings.PsylinkLevel(4) == 0,
            "invalid severities grant nothing");
        Hediff_MentalDisorder mild = new Hediff_MentalDisorder { def = MMDDefOf.MMD_ADHD };
        Hediff_MentalDisorder severe = new Hediff_MentalDisorder { def = MMDDefOf.MMD_PTSD };
        adult.health.hediffSet.hediffs.Add(mild);
        adult.health.hediffSet.hediffs.Add(severe);
        Check(adult.Has(MMDDefOf.MMD_ADHD) && adult.Disorder() == severe, "allocation-free lookup preserves severity ordering");
        Check(!adult.Has(MMDDefOf.MMD_Insomnia), "absent disorder lookup is false");
        Check(typeof(MentalCauseDef).Assembly.GetType("MentalCauseDef") == null
            && typeof(MentalCauseDef).Assembly.GetType("MoreMentalDisorders.MentalCauseDef") == typeof(MentalCauseDef), "custom translation directory requires full type name");
        Harmony harmony = new Harmony("mmd.feedback.regression");
        foreach (Type type in new[] { typeof(Insomnia_Sleep_MMDPatch), typeof(MentalStateHandler_TryStart_MMDPatch), typeof(MentalState_Recover_MMDPatch) })
        {
            harmony.CreateClassProcessor(type).Patch();
            Check(true, "Harmony patch installs: " + type.Name);
        }
        harmony.UnpatchAll(harmony.Id);
        Console.WriteLine("Passed " + checks + " feedback regression checks.");
        return 0;
    }
}
