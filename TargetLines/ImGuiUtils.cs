using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using System;

namespace TargetLines;

internal class ImGuiUtils {
    public static bool WrapBegin(string name, ImGuiWindowFlags flags, Action fn) {
        bool began = false;

        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().Pos);
        ImGui.SetNextWindowSize(ImGui.GetMainViewport().Size);
        began = ImGui.Begin(name, flags);

        try {
            if (began) {
                fn();
            }
        }
        finally {
            ImGui.End();
        }

        return began;
    }
}
