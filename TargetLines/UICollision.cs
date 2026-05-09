using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace TargetLines;

public static class UICollision {
    private static float MERGE_THRESHOLD => 14.0f * ImGui.GetWindowDpiScale();
    private static float MIN_RECT_SIZE => 32.0f * ImGui.GetWindowDpiScale();

    private static List<UIRect> uiRects = new List<UIRect>();
    private static List<UIRect> uiRectDebugBeforeMerge = new List<UIRect>();
    private static List<UIRect> uiRectDebugAfterMerge = new List<UIRect>();
    private static List<UIRect> uiRectDebugCleanup = new List<UIRect>();
    private static List<UIRect> drawableAreaDebug = new List<UIRect>();


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool GetNodeVisible(AtkResNode* node)
    {
        if (node == null)
        {
            return false;
        }

        while (node != null)
        {
            if ((node->NodeFlags & NodeFlags.Visible) != NodeFlags.Visible)
            {
                return false;
            }
            if ((node->NodeFlags & NodeFlags.Enabled) != NodeFlags.Enabled)
            {
                return false;
            }
            if (node->Color.A == 0)
            {
                return false;
            }
            if (node->Alpha_2 == 0)
            {
                return false;
            }

            node = node->ParentNode;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector2 GetNodePosition(AtkResNode* node)
    {
        Vector2 pos = new Vector2(node->X, node->Y);
        AtkResNode* parent = node->ParentNode;

        while (parent != null)
        {
            pos *= new Vector2(parent->ScaleX, parent->ScaleY);
            pos += new Vector2(parent->X, parent->Y);
            parent = parent->ParentNode;
        }
        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector2 GetNodeScale(AtkResNode* node)
    {
        if (node == null)
        {
            Service.Logger.Warning("Node is null");
            return new Vector2(1, 1);
        }

        Vector2 scale = new Vector2(node->ScaleX, node->ScaleY);
        while (node->ParentNode != null)
        {
            node = node->ParentNode;
            scale *= new Vector2(node->ScaleX, node->ScaleY);
        }
        return scale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe Vector2 GetNodeScaledSize(AtkResNode* node)
    {
        if (node == null)
        {
            Service.Logger.Warning("Node is null");
            return new Vector2(1, 1);
        }

        Vector2 scale = GetNodeScale(node);
        Vector2 size = new Vector2(node->Width, node->Height) * scale;
        return size;
    }

    private static List<UIRect> MergeRectList(List<UIRect> rects)
    {
        rects.Sort((a, b) =>
        {
            // size
            var compare = b.Area.CompareTo(a.Area);
            if (compare != 0)
            {
                return compare;
            }

            // vertical position
            compare = a.Position.Y.CompareTo(b.Position.Y);
            if (compare != 0)
            {
                return compare;
            }

            // horizontal position
            return a.Position.X.CompareTo(b.Position.X);
        });

        List<UIRect> result = new List<UIRect>();
        foreach (var rect in rects)
        {
            bool merged = false;
            for (int index = 0; index < result.Count; index++)
            {
                if (TryMergeRects(result[index], rect, out UIRect mergedRect))
                {
                    result[index] = mergedRect;
                    merged = true;
                    break;
                }
            }

            if (!merged)
            {
                result.Add(rect);
            }
        }

        if (result.Count < rects.Count)
        {
            return MergeRectList(result);
        }

        return result;
    }

    private static bool TryMergeRects(UIRect a, UIRect b, out UIRect result)
    {
        result = new UIRect();

        // merge rects inside other rects
        if (a.Contains(b))
        {
            result = a;
            return true;
        }
        if (b.Contains(a))
        {
            result = b;
            return true;
        }

        // merge adjacent rects (with some leeway)
        float vertTreshold = MERGE_THRESHOLD * 2;
        bool canMergeHorizontally = Math.Abs(a.Left - b.Left) <= MERGE_THRESHOLD && Math.Abs(a.Right - b.Right) <= MERGE_THRESHOLD;
        bool canMergeVertically = Math.Abs(a.Top - b.Top) <= vertTreshold && Math.Abs(a.Bottom - b.Bottom) <= vertTreshold;

        if (canMergeHorizontally && (a.Top - MERGE_THRESHOLD <= b.Bottom && b.Top - MERGE_THRESHOLD <= a.Bottom))
        {
            float top = Math.Min(a.Top, b.Top);
            float bottom = Math.Max(a.Bottom, b.Bottom);
            result = new UIRect(new Vector2(Math.Min(a.Left, b.Left), top), new Vector2(Math.Max(a.Width, b.Width), bottom - top));
            return true;
        }
        else if (canMergeVertically && (a.Left - MERGE_THRESHOLD <= b.Right && b.Left - MERGE_THRESHOLD <= a.Right))
        {
            float left = Math.Min(a.Left, b.Left);
            float right = Math.Max(a.Right, b.Right);
            result = new UIRect(new Vector2(left, Math.Min(a.Top, b.Top)), new Vector2(right - left, Math.Max(a.Height, b.Height)));
            return true;
        }

        return false;
    }


    public static unsafe void CollectUIRects()
    {
        uiRects.Clear();

        AtkStage* stage = AtkStage.Instance();
        if (stage == null)
        {
            return;
        }

        RaptureAtkUnitManager* manager = stage->RaptureAtkUnitManager;
        if (manager == null)
        {
            return;
        }

        var group = GroupManager.Instance();
        bool isAlliance = group != null && group->MainGroup.IsAlliance;

        List<UIRect> tempRects = new List<UIRect>();

        foreach (var _entry in manager->AtkUnitManager.AllLoadedUnitsList.Entries)
        {
            var entry = _entry.Value;
            if (entry == null || entry->NameString == "NamePlate"
                || (isAlliance && (entry->NameString == "_AllianceList1" || entry->NameString == "_AllianceList2"))) continue;

            for (int index = 0; index < entry->CollisionNodeListCount; index++)
            {
                var node = entry->CollisionNodeList[index];
                if (node == null) continue;
                if (GetNodeVisible(node)
                    && ((node->NodeFlags & NodeFlags.RespondToMouse) != 0
                    || ((node->NodeFlags & NodeFlags.Fill) != 0))
                    )
                {
                    var pos = GetNodePosition(node);
                    var size = GetNodeScaledSize(node);
                    var rect = new UIRect(pos, size);
                    if (rect.Size.X >= MIN_RECT_SIZE && rect.Size.Y >= MIN_RECT_SIZE)
                    {
                        tempRects.Add(rect);
                    }
                }
            }
        }

        if (Globals.Config.saved.DebugUIInitialRectList)
        {
            uiRectDebugBeforeMerge = new List<UIRect>(tempRects.ToArray());
        }

        tempRects = MergeRectList(tempRects);

        if (Globals.Config.saved.DebugUIMergedRectList)
        {
            uiRectDebugAfterMerge = new List<UIRect>(tempRects.ToArray());
        }

        uiRects.AddRange(tempRects);
    }

    public static void DrawWithClipping(ImDrawListPtr drawList, UIRect boundingBox, Action drawContent)
    {
        List<UIRect> drawableAreas = new List<UIRect> { boundingBox };

        foreach (var uiRect in uiRects)
        {
            if (boundingBox.Intersects(uiRect))
            {
                drawableAreas = FindDrawableAreas(drawableAreas, uiRect);
            }
        }

        if (Globals.Config.saved.DebugUICollisionArea)
        {
            drawableAreaDebug = new List<UIRect>(drawableAreas);
        }

        foreach (var rect in drawableAreas)
        {
            if (rect.Width <= 0.0f || rect.Height <= 0.0f)
            {
                continue;
            }

            drawList.PushClipRect(rect.TopLeft, rect.BottomRight, true);
            drawContent();
            drawList.PopClipRect();
        }
    }

    private static List<UIRect> FindDrawableAreas(List<UIRect> currentAreas, UIRect uiRect)
    {
        List<UIRect> newAreas = new List<UIRect>();

        foreach (var area in currentAreas)
        {
            if (!area.Intersects(uiRect))
            {
                newAreas.Add(area);
                continue;
            }

            // Top
            if (area.Top < uiRect.Top)
            {
                newAreas.Add(new UIRect(
                    new Vector2(area.Left, area.Top),
                    new Vector2(area.Width, uiRect.Top - area.Top)
                ));
            }

            // Bottom
            if (area.Bottom > uiRect.Bottom)
            {
                newAreas.Add(new UIRect(
                    new Vector2(area.Left, uiRect.Bottom),
                    new Vector2(area.Width, area.Bottom - uiRect.Bottom)
                ));
            }

            // Left
            if (area.Left < uiRect.Left)
            {
                newAreas.Add(new UIRect(
                    new Vector2(area.Left, Math.Max(area.Top, uiRect.Top)),
                    new Vector2(uiRect.Left - area.Left, Math.Min(area.Bottom, uiRect.Bottom) - Math.Max(area.Top, uiRect.Top))
                ));
            }

            // Right
            if (area.Right > uiRect.Right)
            {
                newAreas.Add(new UIRect(
                    new Vector2(uiRect.Right, Math.Max(area.Top, uiRect.Top)),
                    new Vector2(area.Right - uiRect.Right, Math.Min(area.Bottom, uiRect.Bottom) - Math.Max(area.Top, uiRect.Top))
                ));
            }
        }

        return newAreas;
    }

    public static void DrawDebugOcclusionOutlines()
    {
        if (!Globals.Config.saved.DebugUICollision) return;

        var drawList = ImGui.GetWindowDrawList();
        var viewport = ImGui.GetMainViewport();

        if (Globals.Config.saved.DebugUIFinalRectList)
        {
            foreach (var rect in uiRects)
            {
                drawList.AddRectFilled(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.3f)));
                drawList.AddRect(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 1)), 0, ImDrawFlags.None, 3);
            }
        }

        if (Globals.Config.saved.DebugUIInitialRectList)
        {
            foreach (var rect in uiRectDebugBeforeMerge)
            {
                drawList.AddRectFilled(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 0.3f)));
                drawList.AddRect(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 0, 1)), 0, ImDrawFlags.None, 3);
            }
        }

        if (Globals.Config.saved.DebugUIMergedRectList)
        {
            foreach (var rect in uiRectDebugAfterMerge)
            {
                drawList.AddRectFilled(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, 0.3f)));
                drawList.AddRect(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 1, 1)), 0, ImDrawFlags.None, 3);
            }
        }
    }

    public static void DrawDebugResultOfClipping()
    {
        if (Globals.Config.saved.DebugUICollisionArea && drawableAreaDebug != null)
        {
            var drawList = ImGui.GetWindowDrawList();
            foreach (var rect in drawableAreaDebug)
            {
                drawList.AddRectFilled(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 0.3f)));
                drawList.AddRect(rect.TopLeft, rect.BottomRight, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 1)), 0, ImDrawFlags.None, 2);
            }
        }
    }
}
