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
        /// Exits non-zero in batch mode unless the compile was clean - at least one fragment
        /// program in the dump, no compile-failure markers, no error-severity messages and
        /// ShaderHasError false. A run that measured nothing must not look like a comfortable pass.
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
                Fail("shader asset did not load: " + path, log);
                return;
            }
            log.AppendLine("[ShaderSpike] Shader.name: " + shader.name);
            log.AppendLine("[ShaderSpike] isSupported (editor graphics device): " + shader.isSupported);
            log.AppendLine("[ShaderSpike] ShaderUtil.ShaderHasError: " + ShaderUtil.ShaderHasError(shader));
            int errorMessages = DumpMessages(shader, "after import", log);

            // Clear any stale dump so the glob below can only find this run's output.
            foreach (string stale in SafeGlob("Temp", "Compiled-*.shader")) File.Delete(stale);

            // ShaderUtil.OpenCompiledShader(Shader shader, int mode, int externPlatformsMask,
            //   bool includeAllVariants, bool preprocessOnly, bool stripLineDirectives) - six
            // parameters on this editor (6000.3); earlier versions carried the first four. mode 3 =
            // "Custom" (use the mask); the mask is 1 << (int)ShaderCompilerPlatform.Metal.
            // Internal API, hence reflection - it is what the Shader inspector calls, and it is the
            // only way to make the editor run the Metal compiler for a chosen target in batch mode.
            // BuildArgs fills it positionally, so the shape below is asserted rather than assumed:
            // an inserted or reordered parameter would otherwise compile for the wrong platform, or
            // for all of them, and still print a plausible-looking sampler count.
            int metalBit = (int)Enum.Parse(typeof(UnityEditor.Rendering.ShaderCompilerPlatform), "Metal");
            int mask = 1 << metalBit;
            log.AppendLine("[ShaderSpike] ShaderCompilerPlatform.Metal = " + metalBit + ", mask = " + mask);
            var open = typeof(ShaderUtil).GetMethod("OpenCompiledShader",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (open == null)
            {
                Fail("ShaderUtil.OpenCompiledShader not found; API changed. Methods: " +
                    string.Join(", ", typeof(ShaderUtil).GetMethods(
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic).Select(m => m.Name).Distinct()), log);
                return;
            }
            string signature = string.Join(", ", open.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
            log.AppendLine("[ShaderSpike] OpenCompiledShader signature: " + signature);
            string shapeProblem = SignatureProblem(open);
            if (shapeProblem != null)
            {
                Fail("OpenCompiledShader signature is not the shape BuildArgs fills: " + shapeProblem +
                     " (got: " + signature + ")", log);
                return;
            }
            try
            {
                object[] args = BuildArgs(open, shader, mask);
                open.Invoke(null, args);
            }
            catch (Exception ex)
            {
                Fail("OpenCompiledShader threw: " + (ex.InnerException ?? ex), log);
                return;
            }

            // The post-compile message list supersedes the import one (Unity reports both through the
            // same store), so the verdict takes the larger of the two rather than their sum.
            errorMessages = Math.Max(errorMessages, DumpMessages(shader, "after Metal compile", log));

            string dump = SafeGlob("Temp", "Compiled-*.shader").FirstOrDefault();
            if (dump == null)
            {
                Fail("no Temp/Compiled-*.shader was produced - nothing was compiled, so any count " +
                     "this run could report would be zero rather than a pass.", log);
                return;
            }

            string text = File.ReadAllText(dump);
            log.AppendLine("[ShaderSpike] dump: " + dump + " (" + text.Length + " bytes)");
            int failureMarkers, fragmentPrograms, programs;
            Report(text, log, out failureMarkers, out fragmentPrograms, out programs);
            try
            {
                Directory.CreateDirectory(outDir);
                string copy = Path.Combine(outDir, Path.GetFileName(dump));
                if (Path.GetFullPath(copy) != Path.GetFullPath(dump)) File.Copy(dump, copy, true);
                log.AppendLine("[ShaderSpike] copied to: " + copy);
            }
            catch (Exception ex) { log.AppendLine("[ShaderSpike] copy failed: " + ex.Message); }

            // MOBILE: the verdict, and the whole reason this tool exists. Everything above only
            // GATHERS - and until this block landed, a Metal compile failure produced a green run
            // with a plausible sampler count, because the failure markers and the error messages went
            // into the same StringBuilder as the good news and out through Debug.Log. This is the
            // gate the device build is meant to trust, so a run passes only when the Metal compiler
            // actually produced at least one fragment program and said nothing wrong about it.
            var problems = new System.Collections.Generic.List<string>();
            if (ShaderUtil.ShaderHasError(shader)) problems.Add("ShaderUtil.ShaderHasError is true");
            if (errorMessages > 0) problems.Add(errorMessages + " error-severity shader message(s) - see the messages above");
            if (failureMarkers > 0) problems.Add(failureMarkers + " compile-failure marker(s) in the dump - see the FAIL LINEs above");
            if (programs == 0) problems.Add("the dump holds no program headers at all (their wording may have changed - the counts above would all read 0)");
            else if (fragmentPrograms == 0) problems.Add("no FRAGMENT program in the dump (" + programs + " program header(s) of other stages)");
            if (problems.Count > 0)
            {
                Fail("the shader did not compile clean for Metal: " + string.Join("; ", problems), log);
                return;
            }
            log.AppendLine("[ShaderSpike] PASS: " + fragmentPrograms + " fragment program(s) of " + programs +
                           ", no compile-failure markers, no error-severity messages, ShaderHasError false.");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Flushes what was gathered so far, then fails the run loudly. Every path that reaches
        /// here means the spike measured NOTHING - and a spike that measures nothing must not look
        /// like one that measured a comfortable zero. In batch mode that means a non-zero exit, the
        /// way MobileModBuilder fails a bundle build; the log is emitted first so the CI artifact
        /// still carries the signature and the shader messages that explain what broke.
        /// </summary>
        static void Fail(string message, StringBuilder log)
        {
            if (log != null && log.Length > 0) Debug.Log(log.ToString());
            Debug.LogError("[ShaderSpike] FAILED: " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }

        /// <summary>
        /// The positional contract BuildArgs relies on: (Shader, int mode, int mask, then bools).
        /// Returns null when the reflected method matches it, or the first thing that does not.
        /// Trailing bools are open-ended on purpose - Unity has appended two of them since this
        /// tool was written, and appending another is harmless; changing one of the first three is
        /// not, and is exactly what would silently compile for a platform nobody asked for.
        /// </summary>
        static string SignatureProblem(System.Reflection.MethodInfo open)
        {
            var ps = open.GetParameters();
            if (ps.Length < 4) return "expected at least 4 parameters, found " + ps.Length;
            if (ps[0].ParameterType != typeof(Shader)) return "parameter 0 is " + ps[0].ParameterType.Name + ", expected Shader";
            if (ps[1].ParameterType != typeof(int)) return "parameter 1 (mode) is " + ps[1].ParameterType.Name + ", expected Int32";
            if (ps[2].ParameterType != typeof(int)) return "parameter 2 (platform mask) is " + ps[2].ParameterType.Name + ", expected Int32";
            for (int i = 3; i < ps.Length; i++)
                if (ps[i].ParameterType != typeof(bool))
                    return "parameter " + i + " is " + ps[i].ParameterType.Name + ", expected Boolean";
            return null;
        }

        static object[] BuildArgs(System.Reflection.MethodInfo open, Shader shader, int mask)
        {
            // The shape is guaranteed by SignatureProblem, which runs first: positions 0/1/2 are
            // Shader / mode / mask and everything after them is a bool, so this is a direct fill
            // rather than the type sniff it used to be.
            var ps = open.GetParameters();
            var args = new object[ps.Length];
            args[0] = shader;
            args[1] = 3;                                                           // mode: custom platforms
            args[2] = mask;                                                        // platform mask
            for (int i = 3; i < ps.Length; i++)
                args[i] = false;                                                   // includeAllVariants, preprocessOnly, stripLineDirectives
            return args;
        }

        static string[] SafeGlob(string dir, string pattern)
        {
            try { return Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : new string[0]; }
            catch (Exception) { return new string[0]; }
        }

        /// <summary>Logs every shader message and returns how many carry Error severity.</summary>
        static int DumpMessages(Shader shader, string when, StringBuilder log)
        {
            var msgs = ShaderUtil.GetShaderMessages(shader);
            int errors = msgs.Count(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
            log.AppendLine("[ShaderSpike] messages " + when + ": " + msgs.Length + " (" + errors + " error-severity)");
            foreach (var m in msgs)
                log.AppendLine("[ShaderSpike]   " + m.severity + " | " + m.platform + " | " + m.file + ":" + m.line +
                               " | " + m.message + (string.IsNullOrEmpty(m.messageDetails) ? "" : " | " + m.messageDetails.Replace("\n", " ")));
            return errors;
        }

        /// <summary>
        /// Per-program binding counts: the max over all Metal programs in the dump (the figure that
        /// must stay under Metal's 16 per-stage limit) and the histogram of counts across programs.
        /// </summary>
        /// <remarks>Returns the number of programs the dump holds. Zero means the header wording
        /// changed under this tool: every count it printed would then read 0, which is exactly the
        /// shape of a comfortable pass - so the caller fails on it.</remarks>
        static int PerProgramMax(string text, string what, string pattern, StringBuilder log)
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
            return chunks.Length - 1;
        }

        /// <summary>
        /// Counts Metal programs and unique sampler/texture bindings in a compiled dump.
        /// Out: how many compile-failure markers the dump carries, how many FRAGMENT programs it
        /// holds, and how many programs of any stage - the three figures the verdict is made of.
        /// </summary>
        static void Report(string text, StringBuilder log, out int failureMarkers, out int fragmentPrograms, out int programs)
        {
            fragmentPrograms = 0;
            foreach (Match m in Regex.Matches(text, @"^\s*(//\s*)?(-- )?(Vertex|Fragment|Hull|Domain|Geometry) shader for \""?(\w+)\""?.*$", RegexOptions.Multiline))
            {
                if (m.Groups[3].Value == "Fragment") fragmentPrograms++;
                log.AppendLine("[ShaderSpike]   program: " + m.Value.Trim());
            }
            log.AppendLine("[ShaderSpike] 'metal' mentions: " + Regex.Matches(text, "metal").Count);
            failureMarkers = Regex.Matches(text, "(?i)(compilation failed|error:|<compile failed)").Count;
            log.AppendLine("[ShaderSpike] compile-failure markers: " + failureMarkers);
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
            programs = PerProgramMax(text, "sampler", @"sampler\s+\w+\s*\[\[\s*sampler\s*\(\s*\d+\s*\)\s*\]\]", log);
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
