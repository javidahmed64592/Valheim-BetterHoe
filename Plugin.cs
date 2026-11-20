using BepInEx;

using BetterHoe.Core;
using BetterHoe.Grid;
using BetterHoe.Pieces;
using BetterHoe.Terrain;

using HarmonyLib;

using Jotunn.Utils;

namespace BetterHoe;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
public class BetterHoePlugin : BaseUnityPlugin
{
    public const string PluginGUID = "javidahmed64592.betterhoe";
    public const string PluginName = "Better Hoe";
    public const string PluginVersion = "0.1.0";

    private Harmony _harmony;

    private void Awake()
    {
        AreaTool.BindConfig(Config);
        GridOverlay.BindConfig(Config);
        Translations.Load();
        HoePieces.Register();

        _harmony = new Harmony(PluginGUID);
        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        AreaTool.Destroy();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Unity message")]
    private void Update()
    {
        AreaTool.Update(Player.m_localPlayer);
    }
}
