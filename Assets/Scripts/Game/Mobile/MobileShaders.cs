// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// Shader.Find by name is ambiguous once mod bundles are loaded: a bundle that carries a Material
// also carries its own compiled copy of that material's shader (Unity embeds any shader not
// assigned to a bundle of its own), stripped to the variants that bundle's materials use, and
// Shader.Find may hand that copy back for every material the engine creates afterwards. The
// pack's Vanilla Enhanced and UBLaMF Textures bundles both embed Daggerfall/Default this way.
// Capturing the player's own shaders before any bundle loads removes the ambiguity.

using System.Collections.Generic;
using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileShaders
    {
        static readonly Dictionary<string, Shader> captured = new Dictionary<string, Shader>();

        static readonly string[] names =
        {
            MaterialReader._DaggerfallDefaultShaderName,
            MaterialReader._DaggerfallBillboardShaderName,
            MaterialReader._StandardShaderName,
            MaterialReader._DaggerfallTilemapShaderName,
            MaterialReader._DaggerfallTilemapTextureArrayShaderName,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Capture()
        {
            foreach (string name in names)
            {
                Shader shader = Shader.Find(name);
                if (shader)
                    captured[name] = shader;
            }
        }

        /// <summary>The player's own shader of that name when captured at startup, else Shader.Find.</summary>
        public static Shader Find(string name)
        {
            Shader shader;
            if (captured.TryGetValue(name, out shader) && shader)
                return shader;
            return Shader.Find(name);
        }
    }
}
