using System.Reflection;
using HarmonyLib;

namespace SmarterFirefighters
{
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
