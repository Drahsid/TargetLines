using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DrahsidLib;
using System.IO;

namespace TargetLines;

public class Plugin : IDalamudPlugin {
    private IDalamudPluginInterface PluginInterface;
    private IChatGui Chat { get; init; }
    private IClientState ClientState { get; init; }
    private ICommandManager CommandManager { get; init; }

    public string Name => "TargetLines";

    private bool WasInPvP = false;
    private bool PlayerWasNull = true;

    private const ImGuiWindowFlags OVERLAY_WINDOW_FLAGS =
          ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoInputs
        | ImGuiWindowFlags.NoMove
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.AlwaysUseWindowPadding;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IChatGui chat, IClientState clientState) {
        PluginInterface = pluginInterface;
        Chat = chat;
        ClientState = clientState;
        CommandManager = commandManager;

        DrahsidLib.DrahsidLib.Initialize(pluginInterface, DrawTooltip);

        InitializeCommands();
        InitializeConfig();
        InitializeUI();

        // as it turns out there's some folks making "true pvp" builds of this plugin, so let's have some fun with them
        if (pluginInterface.InternalName.ToLower().Contains("pvp")) {
            Globals.HandlePvP = true;
        }

        var texture_line_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLine.png");
        var texture_outline_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetLineOutline.png");
        var texture_edge_path = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "Data/TargetEdge.png");


        Globals.LineTexture = Service.TextureProvider.GetFromFile(texture_line_path);
        Globals.OutlineTexture = Service.TextureProvider.GetFromFile(texture_outline_path);
        Globals.EdgeTexture = Service.TextureProvider.GetFromFile(texture_edge_path);

        TargetLineManager.InitializeTargetLines();
    }

    public static void DrawTooltip(string text) {
        if (ImGui.IsItemHovered() && Globals.Config.HideTooltips == false) {
            ImGui.SetTooltip(text);
        }
    }

    private void InitializeCommands() {
        Commands.Initialize();
    }

    private void InitializeConfig() {
        Globals.Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Globals.Config.Initialize();
    }

    private void InitializeUI() {
        Windows.Initialize();
        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += Commands.ToggleConfig;
    }

    private unsafe void DrawOverlay() {
        Globals.Runtime += Globals.Framework->FrameDeltaTime;

        if (Globals.HandlePvP) {
            if (WasInPvP != Service.ClientState.IsPvP) {
                Globals.HandlePvPTime = 0.0f;
                WasInPvP = Service.ClientState.IsPvP;
            }

            if (Service.ClientState.IsPvP) {
                Globals.HandlePvPTime += Globals.Framework->FrameDeltaTime;
            }
        }

        TargetLineManager.DrawOverlay();
    }

    private void OnDraw() {
        Windows.System.Draw();

        if (Service.ObjectTable.LocalPlayer == null) {
            PlayerWasNull = true;
            return;
        }

        if (PlayerWasNull)
        {
            TargetLineManager.InitializeTargetLines();
            PlayerWasNull = false;
        }

        bool combat_flag = Service.Condition[ConditionFlag.InCombat];

        if (Service.Condition[ConditionFlag.OccupiedInCutSceneEvent] || Service.Condition[ConditionFlag.WatchingCutscene])
        {
            return;
        }

        if (!Globals.Config.saved.OnlyUnsheathed
            || (Service.ObjectTable.LocalPlayer.StatusFlags & Dalamud.Game.ClientState.Objects.Enums.StatusFlags.WeaponOut) != 0) {
            if ((Globals.Config.saved.OnlyInCombat == InCombatOption.None
                || (Globals.Config.saved.OnlyInCombat == InCombatOption.InCombat && combat_flag))
                || (Globals.Config.saved.OnlyInCombat == InCombatOption.NotInCombat && !combat_flag)) {
                if (Globals.Config.saved.ToggledOff == false) {
                    ImGuiUtils.WrapBegin("##TargetLinesOverlay", OVERLAY_WINDOW_FLAGS, DrawOverlay);
                }
            }
        }
    }

#region IDisposable Support
    protected virtual void Dispose(bool disposing) {
        if (!disposing) {
            return;
        }

        PluginInterface.SavePluginConfig(Globals.Config);

        PluginInterface.UiBuilder.Draw -= OnDraw;
        Windows.Dispose();
        PluginInterface.UiBuilder.OpenConfigUi -= Commands.ToggleConfig;

        Commands.Dispose();
        SwapChainHook.Dispose();
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
#endregion
}
