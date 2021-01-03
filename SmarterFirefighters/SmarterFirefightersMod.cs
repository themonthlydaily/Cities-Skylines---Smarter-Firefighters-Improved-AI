using CitiesHarmony.API;
using HarmonyLib;
using ICities;
using System;
using System.Reflection;

namespace SmarterFirefighters
{
    public class Mod : IUserMod
    {
        // You can add Harmony 2.0.0.9 as a dependency, but make sure that 0Harmony.dll is not copied to the output directory!
        // (0Harmony.dll is provided by CitiesHarmony workshop item)

        // Also make sure that HarmonyLib is not referenced in any way in your IUserMod implementation!
        // Instead, apply your patches from a separate static patcher class!
        // (otherwise it will fail to instantiate the type when CitiesHarmony is not installed)

        public string Name => "Smarter Firefighters: Improved AI";
        public string Description => "Improves firefighter AI by prioritizing nearby fires to combat fire spread.";
        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(() => Patcher.PatchAll());
        }

        public void OnDisabled()
        {
            if (HarmonyHelper.IsHarmonyInstalled) Patcher.UnpatchAll();
        }
    }

    public static class Patcher
    {
        private const string HarmonyId = "taalbrecht.SmarterFirefighters";

        private static bool patched = false;

        public static void PatchAll()
        {
            if (patched) return;

            UnityEngine.Debug.Log("SmarterFirefighters Activated");

            patched = true;

            // Apply your patches here!
            // Harmony.DEBUG = true;
            var harmony = new Harmony("taalbrecht.SmarterFirefighters");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static void UnpatchAll()
        {
            if (!patched) return;

            var harmony = new Harmony(HarmonyId);
            harmony.UnpatchAll(HarmonyId);

            patched = false;

            UnityEngine.Debug.Log("SmarterFirefighters Deactivated");
        }
    }
}
