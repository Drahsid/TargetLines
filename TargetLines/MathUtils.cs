﻿using System.Numerics;
using System.Runtime.CompilerServices;

namespace TargetLines;

public static class MathUtils {
    public static readonly float RAD2DEG = 57.29577951308232f;
    public static readonly float DEG2RAD = 0.017453292519943f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Lerpf(float lhs, float rhs, float t) {
        return (1 - t) * lhs + t * rhs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuadraticLerpf(float lhs, float rhs, float t) {
        return lhs + (rhs - lhs) * t * t;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CubicLerpf(float lhs, float rhs, float t) {
        float t3 = t * t * t;
        float one_minus_t3 = (1 - t) * (1 - t) * (1 - t);
        return lhs * one_minus_t3 + rhs * t3;
    }

    public static Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
        if (t == 0) {
            return p0;
        }

        if (t == 1) {
            return p3;
        }

        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1 - t;
        float mt2 = mt * mt;
        float mt3 = mt2 * mt;

        Vector3 point =
            mt3 * p0 +
            3 * mt2 * t * p1 +
            3 * mt * t2 * p2 +
            t3 * p3;
        return point;
    }

    public static Vector3 EvaluateQuadratic(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        float mt = 1 - t;

        if (t == 0) {
            return p0;
        }

        if (t == 1) {
            return p2;
        }

        Vector3 point = mt * mt * p0
            + 2 * mt * t * p1
            + t * t * p2;
        return point;
    }
}

