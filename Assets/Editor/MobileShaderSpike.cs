// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: batch-mode "will this shader compile for Metal/iOS, and how many samplers does it
// cost" probe. Unity has no public per-target shader compile API, so this drives the same
// path the Shader inspector's "Compile and show code" button uses (ShaderUtil.OpenCompiledShader
// via reflection, custom-platform mask = Metal), then reads the Temp/Compiled-*.shader it
// writes: that file is the actual Metal source, so counting `sampler ... [[sampler(n)]]`
// declarations in it answers the sampler-budget question that decides whether a shader can
// ship on iOS at all (Metal allows 16 texture/sampler slots per stage).
//
// CLI:
//   -executeMethod DaggerfallWorkshop.Game.Mobile.EditorTools.MobileShaderSpike.CompileForMetal
//   DFU_SPIKE_SHADER=Assets/Shaders/.../Foo.shader   (default: the Distant Terrain far-terrain shader)
//   DFU_SPIKE_OUT=<dir>                              (default: Temp/; the compiled dump is copied here)
//
// Place in Assets/Editor/

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace DaggerfallWorkshop.Game.Mobile.EditorTools
{
    public static class MobileShaderSpike
    {
        const string defaultShader = "Assets/Shaders/DistantTerrain/DistantTerrainTilemap.shader";

        /// <summary>
        /// Compiles one shader for Metal and logs the verdict: compiled or not, every message
        /// verbatim, the number of generated Metal programs, and the unique sampler count.
        /// </summary>
        public static void CompileForMetal()
        {
            string path = Environment.GetEnvironmentVariable("DFU_SPIKE_SHADER");
            if (string.IsNullOrEmpty(path)) path = defaultShader;
            string outDir = Environment.GetEnvironmentVariable("DFU_SPIKE_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = "Temp";

            var log = new StringBuilder();
            log.AppendLine("[ShaderSpike] shader: " + path);
            log.AppendLine("[ShaderSpike] active build target: " + EditorUserBuildSettings.activeBuildTarget);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                Debug.LogError("[ShaderSpike] shader asset did not load: " + path);
                return;
            }
            log.AppendLine("[ShaderSpike] Shader.name: " + shader.name);
            log.AppendLine("[ShaderSpike] isSupported (editor graphics device): " + shader.isSupported);
            log.AppendLine("[ShaderSpike] ShaderUtil.ShaderHasError: " + ShaderUtil.ShaderHasError(shader));
            DumpMessages(shader, "after import", log);

            // Clear any stale dump so the glob below can only find this run's output.
            foreach (string stale in SafeGlob("Temp", "Compiled-*.shader")) File.Delete(stale);

            // ShaderUtil.OpenCompiledShader(shader, mode, customPlatformsMask, includeAllVariants).
            // mode 3 = "Custom" (use the mask); the mask is 1 << (int)ShaderCompilerPlatform.Metal.
            // Internal API, hence reflection - it is what the Shader inspector calls, and it is the
            // only way to make the editor run the Metal compiler for a chosen target in batch mode.
            int metalBit = (int)Enum.Parse(typeof(UnityEditor.Rendering.ShaderCompilerPlatform), "Metal");
            int mask = 1 << metalBit;
            log.AppendLine("[ShaderSpike] ShaderCompilerPlatform.Metal = " + metalBit + ", mask = " + mask);
            var open = typeof(ShaderUtil).GetMethod("OpenCompiledShader",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (open == null)
            {
                Debug.LogError("[ShaderSpike] ShaderUtil.OpenCompiledShader not found; API changed. Methods: " +
                    string.Join(", ", typeof(ShaderUtil).GetMethods(
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic).Select(m => m.Name).Distinct()));
                return;
            }
            log.AppendLine("[ShaderSpike] OpenCompiledShader signature: " +
                string.Join(", ", open.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)));
            try
            {
                object[] args = BuildArgs(open, shader, mask);
                open.Invoke(null, args);
            }
            catch (Exception ex)
            {
                log.AppendLine("[ShaderSpike] OpenCompiledShader threw: " + (ex.InnerException ?? ex));
            }

            DumpMessages(shader, "after Metal compile", log);

            string dump = SafeGlob("Temp", "Compiled-*.shader").FirstOrDefault();
            if (dump == null)
            {
                log.AppendLine("[ShaderSpike] no Temp/Compiled-*.shader was produced.");
            }
            else
            {
                string text = File.ReadAllText(dump);
                log.AppendLine("[ShaderSpike] dump: " + dump + " (" + text.Length + " bytes)");
                Report(text, log);
                try
                {
                    Directory.CreateDirectory(outDir);
                    string copy = Path.Combine(outDir, Path.GetFileName(dump));
                    if (Path.GetFullPath(copy) != Path.GetFullPath(dump)) File.Copy(dump, copy, true);
                    log.AppendLine("[ShaderSpike] copied to: " + copy);
                }
                catch (Exception ex) { log.AppendLine("[ShaderSpike] copy failed: " + ex.Message); }
            }
            Debug.Log(log.ToString());
        }

        static object[] BuildArgs(System.Reflection.MethodInfo open, Shader shader, int mask)
        {
            var ps = open.GetParameters();
            var args = new object[ps.Length];
            args[0] = shader;
            for (int i = 1; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(bool)) args[i] = false;          // includeAllVariants
                else if (i == 1) args[i] = 3;                                      // mode: custom platforms
                else args[i] = mask;                                               // platform mask
            }
            return args;
        }

        static string[] SafeGlob(string dir, string pattern)
        {
            try { return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : new string[0]; }
            catch (Exception) { return new string[0]; }
        }

        static void DumpMessages(Shader shader, string when, StringBuilder log)
        {
            var msgs = ShaderUtil.GetShaderMessages(shader);
            log.AppendLine("[ShaderSpike] messages " + when + ": " + msgs.Length);
            foreach (var m in msgs)
                log.AppendLine("[ShaderSpike]   " + m.severity + " | " + m.platform + " | " + m.file + ":" + m.line +
                               " | " + m.message + (string.IsNullOrEmpty(m.messageDetails) ? "" : " | " + m.messageDetails.Replace("\n", " ")));
        }

        /// <summary>
        /// Per-program binding counts: the max over all Metal programs in the dump (the figure that
        /// must stay under Metal's 16 per-stage limit) and the histogram of counts across programs.
        /// </summary>
        static void PerProgramMax(string text, string what, string pattern, StringBuilder log)
        {
            // Programs are separated by the "-- Vertex/Fragment shader for ..." headers Unity writes.
            string[] chunks = Regex.Split(text, @"(?m)^\s*(?://\s*)?(?:-- )?(?:Vertex|Fragment|Hull|Domain|Geometry) shader for ");
            var histogram = new System.Collections.Generic.SortedDictionary<int, int>();
            int max = 0;
            for (int i = 1; i < chunks.Length; i++)   // chunk 0 is the preamble before the first header
            {
                int n = Regex.Matches(chunks[i], pattern).Count;
                if (n > max) max = n;
                histogram[n] = (histogram.TryGetValue(n, out int c) ? c : 0) + 1;
            }
            log.AppendLine("[ShaderSpike] MAX " + what + "s in any single program: " + max
                + "  (programs: " + (chunks.Length - 1) + ", histogram count->programs: "
                + string.Join(", ", histogram.Select(kv => kv.Key + "->" + kv.Value)) + ")");
        }

        /// <summary>Counts Metal programs and unique sampler/texture bindings in a compiled dump.</summary>
        static void Report(string text, StringBuilder log)
        {
            foreach (Match m in Regex.Matches(text, @"^\s*(//\s*)?(-- )?(Vertex|Fragment|Hull|Domain|Geometry) shader for \""?(\w+)\""?.*$", RegexOptions.Multiline))
                log.AppendLine("[ShaderSpike]   program: " + m.Value.Trim());
            log.AppendLine("[ShaderSpike] 'metal' mentions: " + Regex.Matches(text, "metal").Count);
            log.AppendLine("[ShaderSpike] compile-failure markers: " +
                Regex.Matches(text, "(?i)(compilation failed|error:|<compile failed)").Count);
            foreach (Match m in Regex.Matches(text, @"(?im)^.*(compilation failed|error:|<compile failed).*$"))
                log.AppendLine("[ShaderSpike]   FAIL LINE: " + m.Value.Trim());

            var samplers = new System.Collections.Generic.SortedSet<string>();
            foreach (Match m in Regex.Matches(text, @"sampler\s+(\w+)\s*\[\[\s*sampler\s*\(\s*(\d+)\s*\)\s*\]\]"))
                samplers.Add(m.Groups[1].Value + "@" + m.Groups[2].Value);
            var textures = new System.Collections.Generic.SortedSet<string>();
            foreach (Match m in Regex.Matches(text, @"texture\d\w*<[^>]*>\s+(\w+)\s*\[\[\s*texture\s*\(\s*(\d+)\s*\)\s*\]\]"))
                textures.Add(m.Groups[1].Value + "@" + m.Groups[2].Value);
            log.AppendLine("[ShaderSpike] unique Metal samplers (UNION over all programs/variants - NOT the budget): "
                + samplers.Count + " -> " + string.Join(", ", samplers));
            log.AppendLine("[ShaderSpike] unique Metal textures (union): " + textures.Count + " -> " + string.Join(", ", textures));

            // The number that decides whether a shader can ship is per-PROGRAM, not the union: Metal's
            // limit of 16 texture/sampler slots applies to one program at a time, and a dump holds
            // dozens of keyword variants that never coexist. So split on the per-program headers the
            // dump writes and report the worst single program plus the distribution.
            PerProgramMax(text, "sampler", @"sampler\s+\w+\s*\[\[\s*sampler\s*\(\s*\d+\s*\)\s*\]\]", log);
            PerProgramMax(text, "texture", @"texture\d\w*<[^>]*>\s+\w+\s*\[\[\s*texture\s*\(\s*\d+\s*\)\s*\]\]", log);

            // Fallback for non-Metal (DX/GL) dumps or a different Metal codegen style.
            var setTex = new System.Collections.Generic.SortedSet<string>();
            foreach (Match m in Regex.Matches(text, @"(?m)^SetTexture\s+\d+\s+\[(\w+)\]"))
                setTex.Add(m.Groups[1].Value);
            if (setTex.Count > 0)
                log.AppendLine("[ShaderSpike] SetTexture bindings: " + setTex.Count + " -> " + string.Join(", ", setTex));
        }
    }
}
