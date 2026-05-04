using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using FFXIVClientStructs.FFXIV.Common.Math;
using System.Runtime.InteropServices;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

public static class GameObjectExtensions {
    public static unsafe float GetScale(this IGameObject obj) {
        CSGameObject* _obj = (CSGameObject*)obj.Address;
        return _obj->Scale;
    }

    public static bool IsVisible(this IGameObject obj, bool occlusion) {
        Vector3 safePos = obj.Position;
        safePos.Y += 0.1f;

        if (obj.GetScale() == 0.0f) {
            return false;
        }

        return Globals.IsVisible(obj.GetHeadPosition(), occlusion);
    }

    public static unsafe bool TargetIsTargetable(this IGameObject obj) {
        if (obj.TargetObject == null) {
            return false;
        }
        CSGameObject* targetobj = (CSGameObject*)obj.TargetObject.Address;
        return targetobj->GetIsTargetable();
    }

    public static Vector3 GetHeadPosition(this IGameObject obj) {
        Vector3 pos = obj.Position;
        pos.Y += obj.GetCursorHeight() - 0.2f;
        return pos;
    }

    const int CursorHeightOffset = 0x124;

    public static float GetCursorHeight(this IGameObject obj)
    {
        return Marshal.PtrToStructure<float>(obj.Address + CursorHeightOffset);
    }

    public static float GetHeadHeight(this IGameObject obj)
    {
        return Marshal.PtrToStructure<float>(obj.Address + CursorHeightOffset) - 0.2f;
    }

    public static bool GetIsPlayerCharacter(this IGameObject obj) {
        return obj.ObjectKind == ObjectKind.Pc;
    }

    public static bool GetIsBattleNPC(this IGameObject obj) {
        return obj.ObjectKind == ObjectKind.BattleNpc;
    }

    public static bool GetIsBattleChara(this IGameObject obj) {
        return obj is IBattleChara;
    }

    public static IPlayerCharacter GetPlayerCharacter(this IGameObject obj) {
        return obj as IPlayerCharacter;
    }

    public static unsafe CSGameObject* GetClientStructGameObject(this IGameObject obj)
    {
        return (CSGameObject*)obj.Address;
    }

    public static unsafe TargetSettings GetTargetSettings(this IGameObject obj) {
        TargetSettings settings = new TargetSettings();
        settings.Flags = TargetFlags.Any;

        if (Service.ObjectTable.LocalPlayer != null) {
            if (obj.EntityId == Service.ObjectTable.LocalPlayer.EntityId) {
                settings.Flags |= TargetFlags.Self;
            }
        }

        if (obj.GetIsPlayerCharacter()) {
            GroupManager* gm = GroupManager.Instance();
            settings.Flags |= TargetFlags.Player;
            foreach (PartyMember member in gm->MainGroup.PartyMembers) {
                if (member.EntityId == obj.EntityId) {
                    settings.Flags |= TargetFlags.Party;
                }
            }

            if ((gm->MainGroup.AllianceFlags & 1) != 0 && (settings.Flags & TargetFlags.Party) != 0) {
                foreach (PartyMember member in gm->MainGroup.AllianceMembers) {
                    if (member.EntityId == obj.EntityId) {
                        settings.Flags |= TargetFlags.Alliance;
                    }
                }
            }

            ClassJob ID = (ClassJob)obj.GetPlayerCharacter().ClassJob.RowId;
            settings.Jobs = ClassJobToBit(ID);
            if (DPSJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.DPS;
                if (MeleeDPSJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.MeleeDPS;
                }
                else if (PhysicalRangedDPSJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.PhysicalRangedDPS;
                }
                else if (MagicalRangedDPSJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.MagicalRangedDPS;
                }
            }
            else if (HealerJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.Healer;
                if (PureHealerJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.PureHealer;
                }
                else if (ShieldHealerJobs.Contains(ID)) {
                    settings.Flags |= TargetFlags.ShieldHealer;
                }
            }
            else if (TankJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.Tank;
            }
            else if (CrafterGathererJobs.Contains(ID)) {
                settings.Flags |= TargetFlags.CrafterGatherer;
            }
        }
        else if (obj.GetIsBattleNPC()) {
            settings.Flags |= TargetFlags.Enemy;
        }
        else {
            settings.Flags |= TargetFlags.NPC;
        }

        return settings;
    }
}

