using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace TargetLines;

public static class TargetLineManager
{
    public static TargetLine[] TargetLineArray { get; set; } = null!;
    public static TargetLine FocusTargetLine { get; set; } = null!;

    public static int RenderedLineCount { get; set; } = 0;
    public static int ProcessedLineCount { get; set; } = 0;

    private static LineActor? TestLine = null;

    private static bool TestWindowDraw = true;

    public static void InitializeTargetLines()
    {
        TargetLineArray = new TargetLine[Service.ObjectTable.Length];
        for (int index = 0; index < TargetLineArray.Length; index++) {
            TargetLineArray[index] = new TargetLine();
        }

        FocusTargetLine = new TargetLine(true);

        if (Globals.Config.saved.DebugDXLines) {
            InitializeDirectXLines();
        }
    }


    public static unsafe void InitializeLine(TargetLine line, IGameObject gameObject) {
        if (line.Sleeping || line.Self.EntityId != gameObject.EntityId) {
            var group = GroupManager.Instance();
            switch (Globals.Config.saved.LinePartyMode) {
                default:
                case LinePartyMode.None:
                    line.InitializeTargetLine(gameObject);
                    break;
                case LinePartyMode.PartyOnly:
                    if (group->MainGroup.IsEntityIdInParty(gameObject.EntityId) || group->MainGroup.IsEntityIdInParty((uint)gameObject.TargetObjectId)) {
                        line.InitializeTargetLine(gameObject);
                    }
                    break;
                case LinePartyMode.PartyOnlyInAlliance:
                    if (group->MainGroup.IsAlliance) {
                        if (group->MainGroup.IsEntityIdInParty(gameObject.EntityId) || group->MainGroup.IsEntityIdInParty((uint)gameObject.TargetObjectId))
                        {
                            line.InitializeTargetLine(gameObject);
                        }
                    }
                    else {
                        line.InitializeTargetLine(gameObject);
                    }
                    break;
                case LinePartyMode.AllianceOnly:
                    if (group->MainGroup.IsEntityIdInAlliance(gameObject.EntityId) || group->MainGroup.IsEntityIdInAlliance((uint)gameObject.TargetObjectId)) {
                        line.InitializeTargetLine(gameObject);
                    }
                    break;
            }
        }
    }

    public static void InitializeDirectXLines() {
        Vector3Extensions.Tests();

        try {
            SwapChainResolver.Setup();
        }
        catch (Exception ex) {
            Service.Logger.Error(ex.ToString());
        }

        try {
            SwapChainHook.Setup();
        }
        catch (Exception ex) {
            Service.Logger.Error(ex.ToString());
        }
    }

    public static void DrawOverlay() {
        DrawOverlay_ImGui();
        DrawOverlay_DX();
    }

    private static unsafe void DrawOverlay_ImGui() {
        int renderedLineCount = 0;
        int processedLineCount = 0;

        if (Service.ObjectTable.LocalPlayer == null || TargetLineArray == null) {
            return;
        }

#if !PROBABLY_BAD
    if (Service.ClientState.IsPvP)
    {
        return;
    }
#endif

        List<int> lineDrawIndices = new List<int>();

        var target = TargetSystem.Instance();
        if (target != null)
        {
            if (FocusTargetLine.Sleeping && !Service.ObjectTable.LocalPlayer.IsDead)
            {
                if (target->FocusTarget != null && target->FocusTarget->EntityId != Service.ObjectTable.LocalPlayer.EntityId)
                {
                    InitializeLine(FocusTargetLine, Service.ObjectTable.LocalPlayer);
                }
            }

            if (!FocusTargetLine.Sleeping)
            {
                FocusTargetLine.Update();
            }
        }

        for (int index = 0; index < Service.ObjectTable.Length; index++)
        {
            var gameObject = Service.ObjectTable[index];
            var line = TargetLineArray[index];

            if (gameObject != null && gameObject.IsValid())
            {
                var csGameObject = gameObject.GetClientStructGameObject();
                bool hasTarget = gameObject.TargetObject != null && gameObject.TargetObject.IsValid();
                bool initializeLine = hasTarget && line.Sleeping && !gameObject.IsDead;

#if !PROBABLY_BAD
                if (!gameObject.IsTargetable) {
                    initializeLine = false;
                }

                if (hasTarget && !gameObject.TargetObject.IsTargetable) {
                    initializeLine = false;
                }
#endif

                if (initializeLine)
                {
                    InitializeLine(line, gameObject);
                }
            }

            if (!line.Sleeping)
            {
                line.Update();

                if (line.LineSettings.LineColor.Visible)
                {
                    lineDrawIndices.Add(index);
                }

                processedLineCount++;
            }
        }

        if (Globals.Config.saved.UIOcclusion)
        {
            var dlist = ImGui.GetWindowDrawList();
            UICollision.CollectUIRects();

            foreach (int index in lineDrawIndices)
            {
                UICollision.DrawWithClipping(dlist, TargetLineArray[index].GetBoundingBox(), () =>
                {
                    if (TargetLineArray[index].Draw())
                    {
                        renderedLineCount++;
                    }
                });
                if (Globals.Config.saved.DebugUICollision)
                {
                    UICollision.DrawDebugResultOfClipping();
                }
            }

            if (!FocusTargetLine.Sleeping)
            {
                UICollision.DrawWithClipping(dlist, FocusTargetLine.GetBoundingBox(), () =>
                {
                    if (FocusTargetLine.Draw())
                    {
                        renderedLineCount++;
                    }
                });
                if (Globals.Config.saved.DebugUICollision)
                {
                    UICollision.DrawDebugResultOfClipping();
                }
            }
        }
        else
        {
            foreach (int index in lineDrawIndices)
            {
                if (TargetLineArray[index].Draw())
                {
                    renderedLineCount++;
                }
            }

            if (!FocusTargetLine.Sleeping)
            {
                if (FocusTargetLine.Draw())
                {
                    renderedLineCount++;
                }
            }
        }
        

        UICollision.DrawDebugOcclusionOutlines();
        RenderedLineCount = renderedLineCount;
        ProcessedLineCount = processedLineCount;
    }

    private static void DrawOverlay_DX() {
        if (Service.ObjectTable.LocalPlayer == null || Service.ClientState.IsPvP || !ShaderSingleton.Initialized) {
            return;
        }


        if (TestLine == null && !ShaderSingleton.Fail) {
            try
            {
                TestLine = new LineActor(SwapChainHook.Scene.Device, SwapChainHook.Scene.SwapChain);
            }
            catch (Exception ex)
            {
                Service.Logger.Error(ex.ToString());
            }
        }

        if (Globals.Config.saved.DebugDXLines && TestLine != null) {
            TestLine.Source = Service.ObjectTable.LocalPlayer.GetHeadPosition();

            if (Service.ObjectTable.LocalPlayer.TargetObject != null) {
                TestLine.Destination = Service.ObjectTable.LocalPlayer.TargetObject.GetHeadPosition();
            }
            else {
                TestLine.Destination = new Vector3(1.0f, 0.1f, 5.0f);
            }

            Service.GameGui.WorldToScreen(TestLine.Source, out Vector2 source);
            Service.GameGui.WorldToScreen(TestLine.Destination, out Vector2 dest);
            ImGui.GetWindowDrawList().AddCircleFilled(source, 9, 0xFF00FF00);
            ImGui.GetWindowDrawList().AddCircleFilled(dest, 7, 0xFF0000FF);

            foreach (var point in TestLine.linePoints) {
                Service.GameGui.WorldToScreen(point, out Vector2 xyz);
                //ImGui.GetWindowDrawList().AddCircleFilled(xyz, 3, 0xFFFFFFFF);
            }
        }
    }
}
