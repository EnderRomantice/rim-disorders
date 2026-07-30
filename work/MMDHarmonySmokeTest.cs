using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

internal static class MMDHarmonySmokeTest
{
    private const string Owner = "ender.morementaldisorders";

    private static int Main(string[] args)
    {
        Assembly modAssembly = Assembly.LoadFrom(args[0]);
        Type bootstrap = modAssembly.GetType("MoreMentalDisorders.MMDHarmony", true);
        RuntimeHelpers.RunClassConstructor(bootstrap.TypeHandle);

        MethodBase[] expected =
        {
            AccessTools.Method(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState)),
            AccessTools.Method(typeof(Pawn_PsychicEntropyTracker), nameof(Pawn_PsychicEntropyTracker.TryAddEntropy)),
            AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) }),
            AccessTools.Method(typeof(Ability), nameof(Ability.StartCooldown), new[] { typeof(int) })
        };

        bool ok = true;
        foreach (MethodBase method in expected)
        {
            Patches patches = Harmony.GetPatchInfo(method);
            bool patched = patches != null && patches.Owners.Contains(Owner);
            Console.WriteLine(method.DeclaringType.Name + "." + method.Name + " patched: " + patched);
            ok &= patched;
        }
        return ok ? 0 : 1;
    }
}
