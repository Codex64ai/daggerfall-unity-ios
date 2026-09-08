// MOBILE PORT - source: github.com/drcarademono/dynamic-skies @ 04506e2ef65aff27e4882ec07ed29a6fc7a4be6b
// File BLBStarsSetting.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using System;
using UnityEngine;

[Serializable]
public struct BLBStarsSetting
{
    [NonSerialized]
    public Texture2D StarsTexture;
    public string StarsTextureFile;
    public float StarsTilingX;
    public float StarsTilingY;
    public float StarsOffsetX;
    public float StarsOffsetY;
    public float StarBending;
    [NonSerialized]
    public Texture2D StarsTwinkleTexture;
    public string StarsTwinkleTextureFile;
    //public float StarBrightness;
    [NonSerialized]
    public Texture2D TwinkleTexture;
    public string TwinkleTextureFile;
    public float TwinkleTilingX;
    public float TwinkleTilingY;
    public float TwinkleOffsetX;
    public float TwinkleOffsetY;
    public float TwinkleBoost;
    public float TwinkleSpeed;
}