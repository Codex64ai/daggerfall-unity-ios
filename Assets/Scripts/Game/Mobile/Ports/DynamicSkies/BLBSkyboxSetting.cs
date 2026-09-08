// MOBILE PORT - source: github.com/drcarademono/dynamic-skies @ 04506e2ef65aff27e4882ec07ed29a6fc7a4be6b
// File BLBSkyboxSetting.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;

[Serializable]
public struct BLBSkyboxSetting
{
    public float SunSize;
    public int SunSizeConvergence;
    public float AtmosphereLerpDuration;
    public float AtmosphereNormalThickness;
    public float AtmosphereDawnDuskThickness;
    public float AtmosphereLerp;
    public string SkyTint;
    public string GroundColor;
    public string AmbientColor;
    public float AmbientIntensity;
    public float Exposure;
    public float NightStartHeight;
    public float NightEndHeight;
    public float SkyFadeStart;
    public float SkyEndStart;
    public float stepSize;
    public string FogDayColor;
    public string FogNightColor;
    public float FogDistance;
    public string MoonNightColor;
    public float CloudFadeHeight;

    public string TopCloudsFlat;
    public string BottomCloudsFlat;
    public string StarsFlat;
    public string MasserFlat;
    public string SecundaFlat;
    [NonSerialized]
    public BLBCloudsSetting TopClouds;
    [NonSerialized]
    public BLBCloudsSetting BottomClouds;
    [NonSerialized]
    public BLBStarsSetting Stars;
    [NonSerialized]
    public BLBMoonSetting Masser;
    [NonSerialized]
    public BLBMoonSetting Secunda;
}
