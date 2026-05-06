using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace SledFishMod.FishDiary;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("sledfishmod.webfishing")]   // ensures SledFishMod loads first
public class Plugin : BasePlugin
{
    public static ManualLogSource Log { get; private set; }

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo($"SledFishMod.FishDiary {MyPluginInfo.PLUGIN_VERSION} loading...");

        // Initialise the diary save file — BepInEx standard config directory
        FishDiaryStore.Init(BepInEx.Paths.ConfigPath, Log);

        // Register our custom MonoBehaviour with the IL2CPP runtime before using AddComponent
        ClassInjector.RegisterTypeInIl2Cpp<DiaryUi>();

        // Build the overlay canvas (DontDestroyOnLoad, lives for the entire session)
        DiaryUi.Create(Log);

        // Apply Harmony patches (CatchPatch + MenuPatch)
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Log.LogInfo("SledFishMod.FishDiary loaded successfully.");
    }
}
