using CitiesHarmony.API;
using ICities;

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
}
