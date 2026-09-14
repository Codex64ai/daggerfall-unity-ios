// Project:         Daggerfall Unity - iOS Touch Input Layer
// License:         MIT License
//
// MOBILE: writing one slice of a Texture2DArray when the source does not match the destination.
//
// Graphics.CopyTexture is the fast path and the only one DFU ever used, but it is exact: same width,
// same height, same format, same mip count, or nothing happens. Two places in this port need a slice
// written when those disagree, and they had independently arrived at the same three-step answer -
// blit through a scratch RenderTexture, regenerate the mip chain, CopyTexture the whole element into
// the array. This is that answer, in one place.
//
//   Distant Terrain packs four seasonal tilesets into one array at a single small slice size, so a
//   DREAM pack's 1024x1024 tiles and a Vanilla Enhanced pack's 256x256 tiles have to meet somewhere.
//   That code is device-proven since 2026-09-09; the members there now forward here.
//
//   TextureReplacement.TryMakeTextureArrayCopyTexture builds a terrain tileset array for the engine
//   itself, sized from RECORD 0, and dropped - with an error log and no fallback - any record whose
//   winning mod supplied a different size. An unwritten Texture2DArray slice is uninitialised GPU
//   memory, which Metal draws as magenta. That is the winter-road bug: DREAM wins record 0 of
//   archives 103/303 at 1024x1024, the Vanilla Enhanced Winter Tracks and Masked Roads overlays win
//   the 18 road and track records at 256x256, and every one of those was skipped.
//
// WHY THE DESTINATION IS NEVER RE-ALLOCATED TO MAKE A BLIT POSSIBLE. No driver renders into a
// block-compressed surface, so the scratch RenderTexture cannot carry ASTC - and on iOS every one of
// these arrays is ASTC. The first answer to that was to allocate the whole array in a format a
// RenderTexture can hold, which is correct and unaffordable: 56 slices of 1024x1024 are 26 MB as
// ASTC_6x6 and 235 MB as RGBA32, two of those arrays are live at once in winter rain, and the iPad
// died. So a COMPRESSED array is never widened. A record whose winning asset does not fit it is
// taken from the next mod down the load order that supplies the same record at a size and format the
// array can copy - see Plan, and TextureReplacement.FindMatchingLowerProvider which performs that
// search. The blit path survives only for an array that is ALREADY uncompressed, where resampling
// costs a scratch surface and nothing else. See CanBlitInto.
//
// Place in Assets/Scripts/Game/Mobile/

using UnityEngine;

namespace DaggerfallWorkshop.Game.Mobile
{
    public static class MobileTextureArraySlices
    {
        /// <summary>How one slice has to be written.</summary>
        public enum SlicePack
        {
            /// <summary>Exact match: Graphics.CopyTexture, no GPU work beyond the copy.</summary>
            Copy,
            /// <summary>Anything else: through a scratch RenderTexture, which converts and resamples.</summary>
            Blit,
        }

        /// <summary>
        /// Pure: which of the two a slice needs. CopyTexture demands all four agree - the mip count
        /// included, because it copies every level of an element and refuses a pair that disagree on
        /// how many there are.
        /// </summary>
        public static SlicePack Method(int srcWidth, int srcHeight, int srcMipCount, TextureFormat srcFormat,
                                       int dstWidth, int dstHeight, int dstMipCount, TextureFormat dstFormat)
        {
            return srcWidth == dstWidth && srcHeight == dstHeight
                   && srcMipCount == dstMipCount && srcFormat == dstFormat
                ? SlicePack.Copy : SlicePack.Blit;
        }

        /// <summary>
        /// Pure: can Graphics.Blit WRITE into an array of this format, i.e. can a RenderTexture carry
        /// it? Only the uncompressed colour formats can. It matters because the destination format is
        /// normally the sources' own, and on iOS that is ASTC - a format no driver will render to.
        /// An array of a format this returns false for cannot be resampled into at all, which is why
        /// Plan sends a misfitting record to a lower-priority provider instead.
        /// </summary>
        public static bool CanBlitInto(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.RGB24:
                case TextureFormat.R8:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>How a slice is written when the winning asset does not fit the array.</summary>
        public enum SlicePlan
        {
            /// <summary>The winner fits: Graphics.CopyTexture, no GPU work beyond the copy.</summary>
            Copy,
            /// <summary>
            /// The winner does not fit, but a lower-priority mod supplies the SAME record at a size
            /// and format the array can copy. Take that one. This is the only answer available for a
            /// compressed array, and it is the common one: a partial overlay wins a handful of
            /// records of an archive whose record 0 - and therefore the array - came from a full pack.
            /// </summary>
            LowerProvider,
            /// <summary>
            /// The winner does not fit and nothing below it does either, but the array is already an
            /// uncompressed format, so a scratch RenderTexture can resample the winner straight into
            /// it. Free of the re-allocation blowup, because there is nothing to re-allocate.
            /// </summary>
            Resample,
            /// <summary>
            /// The winner does not fit, nothing below it fits, and the array is compressed. The slice
            /// is left unwritten and the failure logged. Widening the array instead costs 235 MB for
            /// one 1024x1024 tileset, which is how the iPad ran out of memory in winter rain.
            /// </summary>
            Drop,
        }

        /// <summary>
        /// Pure: what to do with one record of a texture array, given the winning asset's dimensions
        /// and the array's own. The array is ALWAYS record 0's size and format - that is the contract
        /// this table exists to keep - so the only question is how a record that disagrees with record
        /// 0 gets in, and the order of the answers is the order of the damage they do:
        ///
        ///   fits                                      -> Copy
        ///   does not fit, a lower provider fits       -> LowerProvider  (different art, same memory)
        ///   does not fit, array is uncompressed       -> Resample       (winner's art, scratch only)
        ///   does not fit, array is compressed         -> Drop           (logged; never widen)
        ///
        /// <paramref name="lowerProviderFits"/> is the caller's search result, not a guess: it walks
        /// the enabled mods below the winner and asks whether any of them supplies this record at a
        /// size and format Method would call a Copy.
        /// </summary>
        public static SlicePlan Plan(int srcWidth, int srcHeight, int srcMipCount, TextureFormat srcFormat,
                                     int dstWidth, int dstHeight, int dstMipCount, TextureFormat dstFormat,
                                     bool lowerProviderFits)
        {
            if (Method(srcWidth, srcHeight, srcMipCount, srcFormat, dstWidth, dstHeight, dstMipCount, dstFormat) == SlicePack.Copy)
                return SlicePlan.Copy;
            if (lowerProviderFits)
                return SlicePlan.LowerProvider;
            if (CanBlitInto(dstFormat))
                return SlicePlan.Resample;
            return SlicePlan.Drop;
        }

        /// <summary>
        /// The scratch surface a blit converts through - one slice of the destination array, in the
        /// destination's exact graphics format (read off the array itself, so colour space is matched
        /// rather than guessed) and with the destination's mip chain, because
        /// Graphics.CopyTexture(rt, 0, dst, slice) copies every mip level of an element and demands
        /// the two agree on how many there are. Its SIZE is the destination's too, which is what makes
        /// the blit a resample: the fullscreen quad reads the source's whole [0,1] UV range into the
        /// destination's viewport whatever the source's resolution is.
        /// Null if the driver cannot create it.
        /// </summary>
        public static RenderTexture CreateSliceScratch(Texture2DArray dst)
        {
            if (dst == null) return null;

            RenderTextureDescriptor desc = new RenderTextureDescriptor(dst.width, dst.height);
            desc.graphicsFormat = dst.graphicsFormat;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
            desc.volumeDepth = 1;
            desc.useMipMap = dst.mipmapCount > 1;
            // Mips are generated once per slice, after the blit - NOT by the RenderTexture itself:
            // autoGenerateMips only regenerates when the surface is resolved, which a CopyTexture out
            // of it does not do.
            desc.autoGenerateMips = false;
            desc.mipCount = Mathf.Max(1, dst.mipmapCount);

            RenderTexture rt = new RenderTexture(desc);
            rt.name = "Texture array slice scratch";
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Clamp;
            if (!rt.Create())
            {
                DestroySliceScratch(rt);
                return null;
            }
            return rt;
        }

        /// <summary>MOBILE: gives the scratch surface's GPU memory back (edit mode included).</summary>
        public static void DestroySliceScratch(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            if (Application.isPlaying)
                Object.Destroy(rt);
            else
                Object.DestroyImmediate(rt);
        }

        /// <summary>
        /// One slice through the scratch surface - blit (converts AND resamples), regenerate the mip
        /// chain the blit only wrote level 0 of, copy the whole element into the array.
        ///
        /// The mips are box-filtered from the CONVERTED level 0 rather than carried across from the
        /// source's own chain; for a same-size source the source's chain is itself a Unity-generated
        /// box filter of the same pixels, so this is the same image, and for a resampled source
        /// carrying them across would not even be possible - they are chains for the wrong dimension.
        ///
        /// <paramref name="srcElement"/> selects the source slice when the source is itself an array;
        /// for a plain Texture2D pass 0.
        /// </summary>
        public static void BlitSlice(Texture src, int srcElement, Texture2DArray dst, int dstSlice, RenderTexture scratch)
        {
            if (src == null || dst == null || scratch == null)
                return;

            RenderTexture wasActive = RenderTexture.active;
            Graphics.Blit(src, scratch, srcElement, 0);
            RenderTexture.active = wasActive;
            if (scratch.useMipMap)
                scratch.GenerateMips();
            Graphics.CopyTexture(scratch, 0, dst, dstSlice);
        }
    }
}
