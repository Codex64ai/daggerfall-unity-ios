// MOBILE PORT - source: github.com/drcarademono/dynamic-skies @ 04506e2ef65aff27e4882ec07ed29a6fc7a4be6b
// File BLBMoonSetting.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;
using UnityEngine;

[Serializable]
public struct BLBMoonSetting
{
    public string MoonColor;
    [NonSerialized]
    public Texture2D MoonTexture;
    public string MoonTextureFile;
    public float TilingX;
    public float TilingY;
    public float OffsetX;
    public float OffsetY;
    public float MinSize;
    public float MaxSize;
    public Vector4 OrbitAngle;
    public float OrbitOffset;
    public float OrbitSpeed;
    public float SemiMinAxis;
    public float SemiMajAxis;
    public float AutoPhase;
    public Vector4 Phase;
    public float Spin;
    public Vector4 TidalAngle;
    public Vector4 SpinSpeed;
}