// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Models/Noise Params/JordanTurbulence.cs, copied unchanged for iOS except lines marked MOBILE.
// Upstream carries no licence header; shipped on the private draft only.
using IniParser.Model;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using static IniUtils;

namespace Monobelisk
{
    [System.Serializable]
    public class JordanTurbulence : NoiseParams
    {
        public string name;
        public int octaves = 8;
        public float frequency = 88.3f;
        public float amplitude = 0.2f;
        public float lacunarity = 2.307f;
        public float persistence = 0.4f;
        public float persistence1 = 0.4f;
        public Vector2 offset = new Vector2();
        public float warp0 = 0.008f;
        public float warp = 0.008f;
        public float damp0 = 0.008f;
        public float damp = 0.008f;
        public float damp_scale = 1f;
        public float maxHeight = 1000f;

        public JordanTurbulence(string name)
        {
            this.name = name;
        }

        public void ApplyToCS(ComputeShader cs)
        {
            string n = name + "_";
            cs.SetInt(n + "octaves", octaves);
            cs.SetFloat(n + "frequency", frequency);
            cs.SetFloat(n + "amplitude", amplitude);
            cs.SetFloat(n + "lacunarity", lacunarity);
            cs.SetFloat(n + "persistence", persistence);
            cs.SetFloat(n + "persistence1", persistence1);
            cs.SetVector(n + "offset", offset);
            cs.SetFloat(n + "warp0", warp0);
            cs.SetFloat(n + "warp", warp);
            cs.SetFloat(n + "damp0", damp0);
            cs.SetFloat(n + "damp", damp);
            cs.SetFloat(n + "damp_scale", damp_scale);
            cs.SetFloat(n + "maxHeight", maxHeight);
        }

        public void ApplyToMaterial(Material mat)
        {
            string n = name + "_";
            mat.SetInt(n + "octaves", octaves);
            mat.SetFloat(n + "frequency", frequency);
            mat.SetFloat(n + "amplitude", amplitude);
            mat.SetFloat(n + "lacunarity", lacunarity);
            mat.SetFloat(n + "persistence", persistence);
            mat.SetFloat(n + "persistence1", persistence1);
            mat.SetVector(n + "offset", offset);
            mat.SetFloat(n + "warp0", warp0);
            mat.SetFloat(n + "warp", warp);
            mat.SetFloat(n + "damp0", damp0);
            mat.SetFloat(n + "damp", damp);
            mat.SetFloat(n + "damp_scale", damp_scale);
            mat.SetFloat(n + "maxHeight", maxHeight);
        }

        public void DeserializeSection(string sectionName, KeyDataCollection keyData)
        {
            name = sectionName;
            foreach(var kd in keyData)
            {
                DeserializeAndAddProperty(kd.KeyName + "=" + kd.Value);
            }
        }

        public string[] GetSerializedSection(string comment = "")
        {
            List<string> lines = new List<string>();

            if (comment != "")
            {
                lines.Add(IniComment(comment));
            }
            lines.AddRange( new string[]
                {
                    IniSection(name),
                    IniProperty("octaves", octaves.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("frequency", frequency.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("amplitude", amplitude.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("lacunarity", lacunarity.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("persistence", persistence.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("persistence1", persistence1.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("offset", SerializeVector2(offset)),
                    IniProperty("warp0", warp0.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("warp", warp.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("damp0", damp0.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("damp", damp.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("damp_scale", damp_scale.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("maxHeight", maxHeight.ToString(CultureInfo.InvariantCulture)),
                });

            return lines.ToArray();
        }

        private void DeserializeAndAddProperty(string serialized)
        {
            var kvp = UnpackIniProperty(serialized);
            var v = kvp.Item2;

            switch(kvp.Item1)
            {
                case "octaves":
                    octaves = int.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "frequency":
                    frequency = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "amplitude":
                    amplitude = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "lacunarity":
                    lacunarity = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "persistence":
                    persistence = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "persistence1":
                    persistence1 = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "offset":
                    offset = DeserializeVector2(v);
                    break;
                case "warp0":
                    warp0 = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "warp":
                    warp = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "damp0":
                    damp0 = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "damp":
                    damp = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "damp_scale":
                    damp_scale = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "maxHeight":
                    maxHeight = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                default:
                    Debug.LogWarning("==> Interesting Terrains: Property \"" + v + "\" does not exist on " + GetType().Name);
                    break;
            }
        }
    }
}