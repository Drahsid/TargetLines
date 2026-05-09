using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using Dalamud.Interface.Textures;
using DrahsidLib;
using System;
using System.Numerics;

namespace TargetLines;

internal class Globals {
    public static double Runtime = 0.0;
    public static double HandlePvPTime = 0.0;
    public static bool HandlePvP = false;
    public static Configuration Config { get; set; } = null!;
    public static ISharedImmediateTexture LineTexture { get; set; } = null!;
    public static ISharedImmediateTexture OutlineTexture { get; set; } = null!;
    public static ISharedImmediateTexture EdgeTexture { get; set; } = null!;

    private static unsafe CSFramework* _Framework { get; set; } = null!;
    public static unsafe CSFramework* Framework {
        get {
            if (_Framework == null) {
                _Framework = CSFramework.Instance();
            }
            return _Framework;
        }
    }

    public static unsafe Vector3 WorldCamera_GetPos() {
        if (Service.CameraManager->Camera == null) {
            return new Vector3(0, 0, 0);
        }

        return new Vector3(
            Service.CameraManager->Camera->CameraBase.X,
            Service.CameraManager->Camera->CameraBase.Z,
            Service.CameraManager->Camera->CameraBase.Y
        );
    }

    public static unsafe Vector3 WorldCamera_GetLookAtPos() {
        if (Service.CameraManager->Camera == null) {
            return new Vector3(0, 0, 0);
        }

        return new Vector3(Service.CameraManager->Camera->CameraBase.LookAtX, Service.CameraManager->Camera->CameraBase.LookAtZ, Service.CameraManager->Camera->CameraBase.LookAtY);
    }

    public static unsafe Vector3 WorldCamera_GetForward() {
        if (Service.CameraManager->Camera == null) {
            return new Vector3(0, 0, 1);
        }

        return MathUtils.NormalizeOrDefault(WorldCamera_GetPos() - WorldCamera_GetLookAtPos(), new Vector3(0, 0, 1));
    }

    public static unsafe bool IsInFirstPerson() {
        if (Service.CameraManager->Camera != null) {
            if (Service.CameraManager->Camera->Mode == 0) {
                return true;
            }
        }

        return false;
    }

    public static unsafe bool IsAngleThetaInsidePerspective(float dot) {
        if (Service.CameraManager->Camera == null) {
            return false;
        }

        var device = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        if (device == null) {
            return false;
        }

        var cam = Service.CameraManager->Camera;
        if (!float.IsFinite(dot)) {
            return false;
        }

        dot = Math.Clamp(dot, -1.0f, 1.0f);
        float angle = MathF.Acos(dot);
        float fovy = cam->FoV;
        float fovx = 2.0f * MathF.Atan(MathF.Tan(cam->FoV * 0.5f) * device->AspectRatio);
        if (MathF.Abs(angle) <= fovy * 0.5f || MathF.Abs(angle) <= fovx * 0.5f) {
            return true;
        }

        return false;
    }

    public static unsafe float GetAngleThetaToCamera(Vector3 position) {
        Vector3 cam = WorldCamera_GetPos();
        Vector3 forward = WorldCamera_GetForward();
        Vector3 to_camera = MathUtils.NormalizeOrDefault(cam - position, forward);

        return Vector3.Dot(to_camera, forward);
    }

    public static unsafe bool IsVisible(Vector3 position, bool occlusion) {
        Vector3 cam = WorldCamera_GetPos();

        float dot = GetAngleThetaToCamera(position);
        // behind camera
        if (dot < 0) {
            return false;
        }

        if (occlusion) {
            var direction = position - cam;
            var length = direction.Length();

            if (length != 0) {
                if (!MathUtils.TryNormalize(direction, out Vector3 dir)) {
                    return false;
                }

                var flags = stackalloc int[] { 0x4000, 0x4000 }; // should probably figure out what these mean
                var hit = stackalloc RaycastHit[1];
                var result = Framework->BGCollisionModule->RaycastMaterialFilter(hit, &cam, &dir, length, 1, flags);
                return result == false;
            }

            return false;
        }
        else {
            return true;
        }
    }
}

