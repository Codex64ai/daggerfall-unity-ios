// Diagnostic: list what each built mod bundle really contains beyond its declared files -
// embedded shader copies, materials and their shaders. Unity pulls a material's shader into
// the bundle unless the shader lives in a bundle of its own, and a stripped duplicate of
// Daggerfall/Default found by Shader.Find at runtime is a device-only failure mode.
//   Unity -batchmode -quit -projectPath . -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileBundleInspector.Run
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileBundleInspector
    {
        public static void Run()
        {
            string dir = System.Environment.GetEnvironmentVariable("DFU_INSPECT_DIR");
            if (string.IsNullOrEmpty(dir)) dir = "Assets/StreamingAssets/Mods";
            foreach (string path in Directory.GetFiles(dir, "*.dfmod"))
            {
                var ab = AssetBundle.LoadFromFile(path);
                if (ab == null) { Debug.Log("[BundleInspector] " + Path.GetFileName(path) + ": FAILED TO LOAD"); continue; }
                var sb = new StringBuilder();
                sb.Append("[BundleInspector] ").Append(Path.GetFileName(path)).Append(": assets=").Append(ab.GetAllAssetNames().Length);
                var shaders = ab.LoadAllAssets<Shader>();
                sb.Append(" shaders=").Append(shaders.Length);
                foreach (var s in shaders) sb.Append(" {").Append(s.name).Append(" supported=").Append(s.isSupported).Append(" passes=").Append(s.passCount).Append('}');
                var mats = ab.LoadAllAssets<Material>();
                sb.Append(" materials=").Append(mats.Length);
                Shader project = Shader.Find("Daggerfall/Default");
                foreach (var m in mats)
                {
                    sb.Append(" [").Append(m.name).Append(" -> ").Append(m.shader ? m.shader.name : "null").Append(m.mainTexture ? "" : " NO-TEX");
                    if (m.shader) sb.Append(m.shader == project ? " PROJECT-SHADER" : " BUNDLE-COPY(id " + m.shader.GetInstanceID() + " vs project " + project.GetInstanceID() + ", path '" + AssetDatabase.GetAssetPath(m.shader) + "')");
                    sb.Append(']');
                }
                var texs = ab.LoadAllAssets<Texture2D>();
                sb.Append(" textures=").Append(texs.Length);
                if (texs.Length > 0) sb.Append(" e.g. ").Append(texs[0].name).Append(' ').Append(texs[0].format).Append(texs[0].isReadable ? " readable" : " not-readable");
                var gos = ab.LoadAllAssets<GameObject>();
                sb.Append(" prefabs=").Append(gos.Length);
                Debug.Log(sb.ToString());
                ab.Unload(true);
            }
            string det = System.Environment.ExpandEnvironmentVariables("%HOME%/dev/dfu-mods/iOS/daggerfall expanded textures.dfmod").Replace("%HOME%", System.Environment.GetEnvironmentVariable("HOME"));
            if (File.Exists(det))
            {
                var ab = AssetBundle.LoadFromFile(det);
                if (ab != null)
                {
                    Debug.Log("[BundleInspector] DET: assets=" + ab.GetAllAssetNames().Length + " shaders=" + ab.LoadAllAssets<Shader>().Length + " materials=" + ab.LoadAllAssets<Material>().Length + " textures=" + ab.LoadAllAssets<Texture2D>().Length);
                    ab.Unload(true);
                }
            }
            Debug.Log("[BundleInspector] done");
        }
    }
}
