// MOBILE PORT - source: github.com/drcarademono/dynamic-skies @ 04506e2ef65aff27e4882ec07ed29a6fc7a4be6b
// File BLBFogSetting.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;
using UnityEngine;

[Serializable]
public struct BLBFogSetting
{
    [NonSerialized]
    public FogMode FogMode;
    public int FogModeInt;
    public float Density;
    public float StartDistance;
    public float EndDistance;
    public bool ExcludeSkybox;
}