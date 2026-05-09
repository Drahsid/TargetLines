using DrahsidLib;
using Dalamud.Bindings.ImGui;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

internal enum ConfigPerformanceImpact {
    Beneficial,
    None,
    Low,
    Medium,
    High
}

internal class ConfigWindow : WindowWrapper {
    public static string ConfigWindowName = "Target Lines Config";
    private static Vector2 MinSize = new Vector2(240, 240);

    private readonly Vector4 PerformanceHighColor = new Vector4(1, 0, 0, 1);
    private readonly Vector4 PerformanceMedColor = new Vector4(1, 1, 0, 1);
    private readonly Vector4 PerformanceLowColor = new Vector4(1, 1, 0.5f, 0.5f);
    private readonly Vector4 PerformanceNoneColor = new Vector4(1, 1, 1, 1);
    private readonly Vector4 PerformanceBeneficialColor = new Vector4(0, 1, 0, 1);

    public ConfigWindow() : base(ConfigWindowName, MinSize) { }

    private string AddSpacesToCamelCase(string text) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        StringBuilder result = new StringBuilder(text.Length * 2);
        result.Append(text[0]);

        for (int index = 1; index < text.Length; index++) {
            if (char.IsUpper(text[index]) && !char.IsUpper(text[index - 1])) {
                result.Append(' ');
            }
            result.Append(text[index]);
        }

        return result.ToString();
    }


    private bool DrawTargetFlagEditor(ref TargetFlags flags, string guard) {
        int flag_count = Enum.GetValues(typeof(TargetFlags)).Length;
        bool should_save = false;
        float charsize = ImGui.CalcTextSize("F").X * 24;

        for (int index = 0; index < flag_count; index++) {
            TargetFlags current_flag = (TargetFlags)(1 << index);
            string label = AddSpacesToCamelCase(current_flag.ToString());
            int flags_dirty = (int)flags;
            float start = ImGui.GetCursorPosX();
            if (ImGui.CheckboxFlags($"{label}##{guard}{index}", ref flags_dirty, (int)current_flag)) {
                flags = (TargetFlags)flags_dirty;
                should_save = true;
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(TargetFlagDescriptions[index]);
            }

            if (Globals.Config.saved.CompactFlagDisplay) {
                if ((index + 1) % 4 != 0) {
                    ImGui.SameLine();
                }
            }
            else {
                int mod = (index + 1) % 2;
                if (mod != 0) {
                    ImGui.SameLine(start + charsize);
                }
            }
        }

        return should_save;
    }

    private bool DrawJobFlagEditor(ref ulong flags, string guard) {
        bool should_save = false;
        float charsize = ImGui.CalcTextSize("F").X * 24;
        if (ImGui.TreeNode($"Jobs##Jobs{guard}")) {
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("If any of these values are enabled, only these specific jobs will be filtered if the entity is a player. Otherwise, these values are completely ignored");
            }
            for (int index = 0; index < (int)ClassJob.Count; index++) {
                ulong flag = ClassJobToBit(index);
                bool toggled = (flags & flag) != 0;
                string label = $"{(ClassJob)index}##{guard}_{index}";
                float start = ImGui.GetCursorPosX();

                if (ImGui.Checkbox(label, ref toggled)) {
                    should_save = true;
                    if (toggled) {
                        flags |= flag;
                    }
                    else {
                        flags &= ~flag;
                    }
                }

                if (Globals.Config.saved.CompactFlagDisplay) {
                    if ((index + 1) % 4 != 0) {
                        ImGui.SameLine();
                    }
                }
                else {
                    int mod = (index + 1) % 2;
                    if (mod != 0) {
                        ImGui.SameLine(start + charsize);
                    }
                }
            }
            ImGui.NewLine();
            ImGui.TreePop();
        }

        return should_save;
    }

    // returns ImGui.IsItemHovered()
    private bool DrawPerformanceImpact(ConfigPerformanceImpact impact) {
        Vector4 color;
        bool ret = ImGui.IsItemHovered();
        ImGui.SameLine();
        switch(impact)
        {
            default:
            case ConfigPerformanceImpact.None:
                color = PerformanceNoneColor;
                break;
            case ConfigPerformanceImpact.Low:
                color = PerformanceLowColor;
                break;
            case ConfigPerformanceImpact.Medium:
                color = PerformanceMedColor;
                break;
            case ConfigPerformanceImpact.High:
                color = PerformanceHighColor;
                break;
            case ConfigPerformanceImpact.Beneficial:
                color = PerformanceBeneficialColor;
                break;
        }
        ImGui.TextColored(color, $"Performance Impact: {impact}");
        return ret;
    }

    private bool DrawFilters() {
        bool should_save = false;

        int selected = (int)Globals.Config.saved.OnlyInCombat;
        if (ImGui.ListBox("Combat setting", ref selected, Enum.GetNames(typeof(InCombatOption)), (int)InCombatOption.Count)) {
            Globals.Config.saved.OnlyInCombat = (InCombatOption)selected;
            should_save = true;
        }

        should_save |= ImGui.Checkbox("Only show target lines when unsheathed", ref Globals.Config.saved.OnlyUnsheathed);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, target lines will stop drawing if your weapon is sheathed");
        }

        Vector4 color = Globals.Config.saved.LineColor.Color.Color;
        Vector4 ocolor = Globals.Config.saved.LineColor.OutlineColor.Color;

        should_save |= ImGui.Checkbox("Fallback visible", ref Globals.Config.saved.LineColor.Visible);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, whenever none of your filters are met, these settings will be used for the target line");
        }

        if (Globals.Config.saved.LineColor.Visible) {
            if (ImGui.ColorEdit4("Fallback Color", ref color)) {
                Globals.Config.saved.LineColor.Color.Color = color;
                should_save = true;
            }

            if (ImGui.ColorEdit4("Fallback Outline Color", ref ocolor)) {
                Globals.Config.saved.LineColor.OutlineColor.Color = ocolor;
                should_save = true;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        should_save |= ImGui.Checkbox("Compact Flag Display", ref Globals.Config.saved.CompactFlagDisplay);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, 4 flag options will be displayed per line, as opposed to 2");
        }

        ImGui.Text("Filter & Color settings");
        if (ImGui.Button("New")) {
            Globals.Config.LineColors.Add(new TargetSettingsPair(new TargetSettings(), new TargetSettings(), new LineColor()));
            Globals.Config.SortLineColors();
            should_save = true;
        }

        ImGui.Spacing();

        for (int qndex = 0; qndex < Globals.Config.LineColors.Count; qndex++) {
            var settings = Globals.Config.LineColors[qndex];
            if (settings == null || settings.From == null || settings.To == null || settings.LineColor == null) {
                continue;
            }

            var guid = settings.UniqueId.ToString();
            int flag_count = Enum.GetValues(typeof(TargetFlags)).Length;
            List<string> from = new List<string>();
            List<string> to = new List<string>();

            color = settings.LineColor.Color.Color;
            ocolor = settings.LineColor.OutlineColor.Color;

            for (int index = 0; index < flag_count; index++) {
                TargetFlags current_flag = (TargetFlags)(1 << index);
                if (((int)settings.From.Flags & (int)current_flag) != 0) {
                    from.Add(AddSpacesToCamelCase(current_flag.ToString()));
                }
                if (((int)settings.To.Flags & (int)current_flag) != 0) {
                    to.Add(AddSpacesToCamelCase(current_flag.ToString()));
                }
            }

            int priority = settings.GetPairPriority(settings.FocusTarget);
            if (ImGui.TreeNode($"{string.Join('|', from)} -> {string.Join('|', to)} ({priority}{(settings.FocusTarget ? "F" : "")})###LineColorsEntry{guid}")) {
                if (ImGui.TreeNode($"Source Filters###From{guid}")) {
                    if (DrawTargetFlagEditor(ref settings.From.Flags, $"From{guid}Flags")) {
                        should_save = true;
                    }

                    if (DrawJobFlagEditor(ref settings.From.Jobs, $"From{guid}Jobs")) {
                        should_save = true;
                    }
                    ImGui.TreePop();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Conditions that the targeting entity must satisfy to use these settings");
                }

                if (ImGui.TreeNode($"Target Filters###To{guid}")) {
                    if (DrawTargetFlagEditor(ref settings.To.Flags, $"To{guid}Flags")) {
                        should_save = true;
                    }

                    if (DrawJobFlagEditor(ref settings.To.Jobs, $"To{guid}Jobs")) {
                        should_save = true;
                    }
                    ImGui.TreePop();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Conditions that the targeted entity must satisfy to use these settings");
                }

                if (ImGui.Checkbox("Focus Target", ref settings.FocusTarget))
                {
                    should_save = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("If enabled, this only applies to the targets which are focus-targeted by the player.");
                }


                if (ImGui.ColorEdit4($"Color###Color{guid}", ref color)) {
                    settings.LineColor.Color.Color = color;
                    should_save = true;
                }

                if (ImGui.ColorEdit4($"Outline Color###OColor{guid}", ref ocolor)) {
                    settings.LineColor.OutlineColor.Color = ocolor;
                    should_save = true;
                }

                if (ImGui.Checkbox($"Use Quadratic Line###UseQuad{guid}", ref settings.LineColor.UseQuad)) {
                    should_save = true;
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If enabled, this line will use a quadratic formula (as opposed to the default cubic formula). Useful if you would like different lines to have slightly different shapes. Quadratic lines look more like a half circle");
                }

                if (ImGui.Checkbox($"Visible###Visible{guid}", ref settings.LineColor.Visible)) {
                    should_save = true;
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If disabled, this line will not render");
                }

                if (ImGui.InputInt($"Priority###Priority{guid}", ref settings.Priority, 1, 1, default, ImGuiInputTextFlags.EnterReturnsTrue)) {
                    Globals.Config.SortLineColors();
                    should_save = true;
                    break;
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("A higher priority is considered to be more important. Setting this to -1 makes the plugin calculate it.");
                }

                if (ImGui.Button($"Delete###DeleteEntry{guid}")) {
                    Globals.Config.LineColors.RemoveAt(qndex);
                    Globals.Config.SortLineColors();
                    should_save = true;
                    ImGui.TreePop();
                    break;
                }

                ImGui.TreePop();
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("source(s) -> target(s) (priority)");
            }

            ImGui.Separator();
        }

        ImGui.Spacing();
        ImGui.Separator();

        if (ImGui.Button("Reset To Default")) {
            Globals.Config.saved = new SavedConfig();
            Globals.Config.InitializeDefaultLineColorsConfig();
            Globals.Config.Save();
            TargetLineManager.InitializeTargetLines(); // reset lines
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Set all of the values to the plugin defaults. This will delete any custom entries that you have made!");
        }

        ImGui.SameLine();
        if (ImGui.Button("Copy Preset")) {
            ImGui.SetClipboardText(JsonConvert.SerializeObject(Globals.Config.LineColors));
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Copy your rules to the clipboard");
        }

        ImGui.SameLine();
        if (ImGui.Button("Paste Preset")) {
            try {
                var pastedLineColors = JsonConvert.DeserializeObject<List<TargetSettingsPair>>(ImGui.GetClipboardText());
                if (pastedLineColors != null) {
                    Globals.Config.LineColors = pastedLineColors;
                }
                else {
                    Service.ChatGui.Print("Clipboard did not contain a preset.");
                }
            }
            catch (Exception ex) {
                Service.Logger.Warning($"Could not paste Target Lines preset: {ex.Message}");
                Service.ChatGui.Print("Failed to parse clipboard preset.");
            }
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Paste rules from the clipboard. This overwrites your existing rules!");
        }

        return should_save;
    }

    private bool DrawVisuals() {
        bool should_save = false;

        should_save |= ImGui.Checkbox("Use Legacy Line", ref Globals.Config.saved.SolidColor);
        if (DrawPerformanceImpact(ConfigPerformanceImpact.Beneficial)) {
            ImGui.SetTooltip("If enabled, use the original target line effect instead of the newer fancy line effect.\nThis makes lines appear more flat. This does not support the pulsing effect, nor does it support UI collision. This also fixes the sawtooth effect of the fancy lines. This is essentially a simple/clearer lines mode.");
        }

        ImGui.Separator();

        if (ImGui.TreeNode("Occlusion")) {
            should_save |= ImGui.Checkbox("Occlusion Culling", ref Globals.Config.saved.OcclusionCulling);
            if (DrawPerformanceImpact(ConfigPerformanceImpact.High)) {
                ImGui.SetTooltip("If enabled, target lines will stop drawing if both their start, middle, and end points are not visible. Note that this is always enabled for enemies!");
            }

            var level = ConfigPerformanceImpact.High;
            if (Globals.Config.saved.TextureCurveSampleCount < 32) {
                level = ConfigPerformanceImpact.Medium;
            }
            else if (Globals.Config.saved.DynamicSampleCount) {
                level = ConfigPerformanceImpact.Low;
            }
            if (Globals.Config.saved.SolidColor == false) {
                should_save |= ImGui.Checkbox("UI Occlusion Culling", ref Globals.Config.saved.UIOcclusion);

                if (DrawPerformanceImpact(level)) {
                    ImGui.SetTooltip("If enabled, target lines will not draw segments which intersect with most UI elements. The performance cost of this scales with the configured smoothness steps.");
                }
            }
            else {
                ImGui.TextDisabled($" [ {(Globals.Config.saved.UIOcclusion ? 'X' : ' ')} ] UI Occlusion Culling");
                if (DrawPerformanceImpact(level)) {
                    ImGui.SetTooltip("Disable \"Use Legacy Line\" to configure");
                }
            }

            ImGui.TreePop();
        }
        ImGui.Separator();

        if (Globals.Config.saved.SolidColor == false) {
            if (ImGui.TreeNode("Fancy Line Options")) {
                should_save |= ImGui.Checkbox("Use Pulsing Effect", ref Globals.Config.saved.PulsingEffect);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If enabled, while not using solid color lines, the lines will periodically pulse from the source to the target");
                }

                should_save |= ImGui.Checkbox("Fade line as it approaches target", ref Globals.Config.saved.FadeToEnd);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If enabled, the line will become more transparent as it approaches the target");
                }

                if (Globals.Config.saved.FadeToEnd) {
                    should_save |= ImGui.SliderFloat("End point opacity %", ref Globals.Config.saved.FadeToEndScalar, 0.0f, 1.0f);
                }
                else
                {
                    ImGui.TextDisabled(" End point opacity %");
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Enable \"Fade line as it approaches target\" to configure");
                }

                should_save |= ImGui.Checkbox("Dynamic Smoothness", ref Globals.Config.saved.DynamicSampleCount);
                if (DrawPerformanceImpact(ConfigPerformanceImpact.Beneficial)) {
                    ImGui.SetTooltip("When enabled, the sample count will automatically adjust based on target line distance. This may be beneficial to performance.");
                }

                int selected = (int)Globals.Config.saved.LinePartyMode;
                if (ImGui.ListBox("Party Filter Mode", ref selected, Enum.GetNames(typeof(LinePartyMode)), (int)LinePartyMode.Count))
                {
                    Globals.Config.saved.LinePartyMode = (LinePartyMode)selected;
                    should_save = true;
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("PartyOnly = Only draw lines to/from the party\nPartyOnlyInAlliance = Only draw lines to/from the party when in an active alliance\nAllianceOnly = Only draw lines to/from the alliance");
                }

                if (!Globals.Config.saved.DynamicSampleCount) {
                    should_save |= ImGui.SliderInt("Smoothness Steps", ref Globals.Config.saved.TextureCurveSampleCount, 3, 512);
                    if (DrawPerformanceImpact(ConfigPerformanceImpact.Medium)) {
                        ImGui.SetTooltip("This value represents how many samples are used to produce the target line effect. Lower values may have better performance");
                    }

                    ImGui.TextDisabled($"[ {(Globals.Config.saved.UseScreenSpaceLOD ? 'X' : ' ')} ] Use ScreenSpace LOD");
                    if (DrawPerformanceImpact(ConfigPerformanceImpact.Beneficial))
                    {
                        ImGui.SetTooltip("Enable \"Dynamic Smoothness\" to configure.");
                    }

                    ImGui.TextDisabled($"[ {(Globals.Config.saved.ViewAngleSampling ? 'X' : ' ')} ] View Angle Sampling");
                    if (DrawPerformanceImpact(ConfigPerformanceImpact.Beneficial))
                    {
                        ImGui.SetTooltip("Enable \"Dynamic Smoothness\" to configure.");
                    }
                }
                else {
                    should_save |= ImGui.SliderInt("Minimum Smoothness Steps", ref Globals.Config.saved.TextureCurveSampleCountMin, 3, Globals.Config.saved.TextureCurveSampleCountMax - 3);
                    if (DrawPerformanceImpact(ConfigPerformanceImpact.Medium)) {
                        ImGui.SetTooltip("This value represents the minimum number of samples used to produce the target line effect. Lower values may have better performance");
                    }

                    should_save |= ImGui.SliderInt("Maximum Smoothness Steps", ref Globals.Config.saved.TextureCurveSampleCountMax, Globals.Config.saved.TextureCurveSampleCountMin + 3, 512);
                    if (DrawPerformanceImpact(ConfigPerformanceImpact.Low)) {
                        ImGui.SetTooltip("This value represents the maximum number of samples used to produce the target line effect. Lower values may have better performance when there are longer lines");
                    }

                    should_save |= ImGui.Checkbox("Use ScreenSpace LOD", ref Globals.Config.saved.UseScreenSpaceLOD);
                    if (DrawPerformanceImpact(ConfigPerformanceImpact.Beneficial))
                    {
                        ImGui.SetTooltip("If enabled, line LOD will be sampled using screen space instead of 3D space.");
                    }

                    should_save |= ImGui.Checkbox("View Angle Sampling", ref Globals.Config.saved.ViewAngleSampling);
                    if (DrawPerformanceImpact(ConfigPerformanceImpact.Beneficial))
                    {
                        ImGui.SetTooltip("If enabled, line LOD will be reduced when they are not perpendicular to the camera.");
                    }
                }
                ImGui.TreePop();
            }
        }
        else {
            ImGui.TextDisabled(" > Fancy Line Options");
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Disable \"Use Legacy Line\" to unfold");
            }
        }
        ImGui.Separator();

        if (ImGui.TreeNode("General Line Appearance")) {
            should_save |= ImGui.SliderFloat("Height Scale", ref Globals.Config.saved.HeightScale, 0.0f, 1.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("This value scales the height of the source and target. 0 is the feet, 1 is the head");
            }

            should_save |= ImGui.SliderFloat("Player Arc Height Bump", ref Globals.Config.saved.PlayerHeightBump, 0.0f, 10.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("If the source of the line is a player, it's starting point will be moved up by this amount");
            }

            should_save |= ImGui.SliderFloat("Enemy Arc Height Bump", ref Globals.Config.saved.EnemyHeightBump, 0.0f, 10.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("If the source of the line is an enemy, it's starting point will be moved up by this amount");
            }

            should_save |= ImGui.SliderFloat("Arc Scale", ref Globals.Config.saved.ArcHeightScalar, 0.0f, 2.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("This value scales middle point of the line. 0 will make the line flat, 1 will make the middle point of the line the average height of the source and target higher");
            }

            should_save |= ImGui.SliderFloat("Line Thickness", ref Globals.Config.saved.LineThickness, 0.0f, 64.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("The thickness of the line. 0 will disable the line");
            }

            should_save |= ImGui.SliderFloat("Outline Thickness", ref Globals.Config.saved.OutlineThickness, 0.0f, 72.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("The thickness of the outline. 0 will disable the outline");
            }

            ImGui.Spacing();
            ImGui.Spacing();
            if (ImGui.TreeNode("Line FX")) {
                should_save |= ImGui.Checkbox("Use Breathing Effect", ref Globals.Config.saved.BreathingEffect);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If enabled, the opacity of the lines with fade in-and-out based on the alpha values below");
                }

                should_save |= ImGui.SliderFloat("Alpha Fade Amplitude", ref Globals.Config.saved.WaveAmplitudeOffset, 0.0f, 0.5f);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("This value represents the maximum difference that the breathing and pulsing effect will have on the opacity of the line");
                }

                should_save |= ImGui.SliderFloat("Alpha Frequency", ref Globals.Config.saved.WaveFrequencyScalar, 0.0f, 10.0f);
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("This value represents the speed in which the breathing and pulsing effect will happen");
                }

                //// fancy line

                ImGui.TextDisabled("<- Use Pulsing Effect");
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Configurable in Fancy Line Options");
                }

                ImGui.TextDisabled("<- Fade line as it approaches target");
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Configurable in Fancy Line Options");
                }

                ImGui.TextDisabled("<- End point opacity %");
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Configurable in Fancy Line Options");
                }
            }

            ImGui.TreePop();
        }
        ImGui.Separator();

        if (ImGui.TreeNode("Line Animation")) {
            int selected = (int)Globals.Config.saved.DeathAnimation;
            if (ImGui.ListBox("No Target Animation", ref selected, Enum.GetNames(typeof(LineDeathAnimation)), (int)LineDeathAnimation.Count)) {
                Globals.Config.saved.DeathAnimation = (LineDeathAnimation)selected;
                should_save = true;
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("The formula to use for flattening the line when there is no longer a target");
            }

            should_save |= ImGui.SliderFloat("New Target Easing Time", ref Globals.Config.saved.NewTargetEaseTime, 0.0f, 5.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("When switching targets, this represents the time (in seconds) the line will spend shifting to the new target");
            }

            should_save |= ImGui.SliderFloat("No Target Fading Time", ref Globals.Config.saved.NoTargetFadeTime, 0.0f, 5.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("When there is no longer a target, this represents the time (in seconds) the line will spend fading out");
            }

            should_save |= ImGui.SliderFloat("No Target Animation Time Scale", ref Globals.Config.saved.DeathAnimationTimeScale, 1.0f, 4.0f);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("A scalar for how quickly the line flattens when there is no target. 1 means the line will be flat at the end of the animation, 2 means it will be flat when 50 percent of the animation has completed");
            }
            ImGui.TreePop();
        }
        ImGui.Separator();

        return should_save;
    }

    private bool DrawDebug() {
        bool should_save = false;

        should_save |= ImGui.Checkbox("Debug Dynamic Sample Count", ref Globals.Config.saved.DebugDynamicSampleCount);
        should_save |= ImGui.Checkbox("Debug UI Collision", ref Globals.Config.saved.DebugUICollision);
        should_save |= ImGui.Checkbox("Draw Initial UI Collision Rects (0)", ref Globals.Config.saved.DebugUIInitialRectList);
        should_save |= ImGui.Checkbox("Draw Merged UI Collision Rects (1)", ref Globals.Config.saved.DebugUIMergedRectList);
        should_save |= ImGui.Checkbox("Draw Final UI Collision Rects (2)", ref Globals.Config.saved.DebugUIFinalRectList);
        should_save |= ImGui.Checkbox("Draw the result of clipping (drawable areas)", ref Globals.Config.saved.DebugUICollisionArea);
        should_save |= ImGui.Checkbox("Debug DX Lines", ref Globals.Config.saved.DebugDXLines);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Toggling this requires a plugin restart (I am lazy)");
        }

        ImGui.TextDisabled($"Number of rendered lines: {TargetLineManager.RenderedLineCount.ToString()}");
        ImGui.TextDisabled($"Number of processed lines: {TargetLineManager.ProcessedLineCount.ToString()}");

        return should_save;
    }

    public override void Draw() {
        bool should_save = false;
        bool node_hover = false;
        bool nest = false;
        if (ImGui.BeginTabBar("ConfigTabs")) {
            if (ImGui.BeginTabItem("Filters")) {
                nest = true;
                node_hover = ImGui.IsItemHovered();
                should_save |= DrawFilters();
                ImGui.EndTabItem();
            }
            if (!nest) {
                node_hover = ImGui.IsItemHovered();
            }
            if (node_hover) {
                ImGui.SetTooltip("Configure how and when target lines appear");
            }

            node_hover = false;
            nest = false;
            if (ImGui.BeginTabItem("Visuals")) {
                nest = true;
                node_hover = ImGui.IsItemHovered();
                should_save |= DrawVisuals();
                ImGui.EndTabItem();
            }
            if (!nest) {
                node_hover = ImGui.IsItemHovered();
            }
            if (node_hover) {
                ImGui.SetTooltip("The appearance and performance of target lines");
            }

            node_hover = false;
            nest = false;
            if (ImGui.BeginTabItem("Debug")) {
                nest = true;
                node_hover = ImGui.IsItemHovered();
                should_save |= DrawDebug();
                ImGui.EndTabItem();
            }
            if (!nest) {
                node_hover = ImGui.IsItemHovered();
            }
            if (node_hover) {
                ImGui.SetTooltip("Sometimes things are broken");
            }
            ImGui.EndTabBar();
        }

        if (should_save) {
            Globals.Config.Save();
            TargetLineManager.InitializeTargetLines(); // reset lines
        }
    }
}

