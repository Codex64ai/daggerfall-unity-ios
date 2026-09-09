// MOBILE PORT - source: github.com/drcarademono/wod-terrain @ 9aeb1bcc5de5343ccb7a6b09062559ad871e9d40
// File Scripts/Models/Noise Params/SwissTurbulence.cs, copied unchanged for iOS except lines marked MOBILE.
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
    public class SwissTurbulence : NoiseParams
    {
        public string name;
        public int octaves = 12;
        public float frequency = 0.01f;
        public float amplitude = 0.93f;
        public float lacunarity = 1.54f;
        public float persistence = 0.7f;
        public Vector2 offset = new Vector2();
        public float ridgeOffset = 0.47f;
        public float warp = 30.86f;
        public float maxHeight = 4000f;

        public SwissTurbulence(string name)
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
            cs.SetVector(n + "offset", offset);
            cs.SetFloat(n + "ridgeOffset", ridgeOffset);
            cs.SetFloat(n + "warp", warp);
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
            mat.SetVector(n + "offset", offset);
            mat.SetFloat(n + "ridgeOffset", ridgeOffset);
            mat.SetFloat(n + "warp", warp);
            mat.SetFloat(n + "maxHeight", maxHeight);
        }

        public void DeserializeSection(string sectionName, KeyDataCollection keyData)
        {
            name = sectionName;
            foreach (var kd in keyData)
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
            lines.AddRange(new string[]
                {
                    IniSection(name),
                    IniProperty("octaves", octaves.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("frequency", frequency.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("amplitude", amplitude.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("lacunarity", lacunarity.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("persistence", persistence.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("offset", SerializeVector2(offset)),
                    IniProperty("ridgeOffset", ridgeOffset.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("warp", warp.ToString(CultureInfo.InvariantCulture)),
                    IniProperty("maxHeight", maxHeight.ToString(CultureInfo.InvariantCulture)),
                });

            return lines.ToArray();
        }

        private void DeserializeAndAddProperty(string serialized)
        {
            var kvp = UnpackIniProperty(serialized);
            var v = kvp.Item2;

            switch (kvp.Item1)
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
                case "offset":
                    offset = DeserializeVector2(v);
                    break;
                case "ridgeOffset":
                    ridgeOffset = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "warp":
                    warp = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                case "maxHeight":
                    maxHeight = float.Parse(v, CultureInfo.InvariantCulture);
                    break;
                default:
                    Debug.LogWarning("Property \"" + v + "\" does not exist on " + GetType().Name);
                    break;
            }
        }
    }
}