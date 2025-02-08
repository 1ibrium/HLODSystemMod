    using System;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Runtime.InteropServices;
    using UnityEngine;

    public enum FREE_IMAGE_FORMAT
    {
        FIF_UNKNOWN = -1,
        FIF_BMP = 0,
        FIF_ICO = 1,
        FIF_JPEG = 2,
        FIF_JNG = 3,
        FIF_KOALA = 4,
        FIF_LBM = 5,
        FIF_MNG = 6,
        FIF_PBM = 7,
        FIF_PBMRAW = 8,
        FIF_PCD = 9,
        FIF_PCX = 10,
        FIF_PGM = 11,
        FIF_PGMRAW = 12,
        FIF_PNG = 13,
        FIF_PPM = 14,
        FIF_PPMRAW = 15,
        FIF_RAS = 16,
        FIF_TARGA = 17,
        FIF_TIFF = 18,
        FIF_WBMP = 19,
        FIF_PSD = 20,
        FIF_CUT = 21,
        FIF_IFF = FIF_LBM,
        FIF_XBM = 22,
        FIF_XPM = 23
    }

    public enum FREE_IMAGE_FILTER
    {
        FILTER_BOX = 0,
        FILTER_BICUBIC = 1,
        FILTER_BILINEAR = 2,
        FILTER_BSPLINE = 3,
        FILTER_CATMULLROM = 4,
        FILTER_LANCZOS3 = 5
    }

    public class FreeImage
    {
#if USE_INTERNAL_FREEIMAGE
        private const string FreeImageLibrary = "__Internal";
#else
        private const string FreeImageLibrary = "FreeImage";
#endif

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_Load")]
        public static extern IntPtr FreeImage_Load(FREE_IMAGE_FORMAT format, string filename, int flags);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_OpenMemory")]
        public static extern IntPtr FreeImage_OpenMemory(IntPtr data, uint size_in_bytes);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_CloseMemory")]
        public static extern IntPtr FreeImage_CloseMemory(IntPtr data);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_AcquireMemory")]
        public static extern bool FreeImage_AcquireMemory(IntPtr stream, ref IntPtr data, ref uint size_in_bytes);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_LoadFromMemory")]
        public static extern IntPtr FreeImage_LoadFromMemory(FREE_IMAGE_FORMAT format, IntPtr stream, int flags);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_Unload")]
        public static extern void FreeImage_Unload(IntPtr dib);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_Save")]
        public static extern bool FreeImage_Save(FREE_IMAGE_FORMAT format, IntPtr handle, string filename, int flags);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_SaveToMemory")]
        public static extern bool FreeImage_SaveToMemory(FREE_IMAGE_FORMAT format, IntPtr dib, IntPtr stream, int flags);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_ConvertToRawBits")]
        public static extern void FreeImage_ConvertToRawBits(IntPtr bits, IntPtr dib, int pitch, uint bpp, uint red_mask, uint green_mask, uint blue_mask, bool topdown);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_ConvertToRawBits")]
        public static extern void FreeImage_ConvertToRawBits(byte[] bits, IntPtr dib, int pitch, uint bpp, uint red_mask, uint green_mask, uint blue_mask, bool topdown);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_ConvertTo32Bits")]
        public static extern IntPtr FreeImage_ConvertTo32Bits(IntPtr handle);
        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_ConvertTo24Bits")]

        public static extern IntPtr FreeImage_ConvertTo24Bits(IntPtr handle);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_Rescale")]
        public static extern IntPtr FreeImage_Rescale(IntPtr dib, int dst_width, int dst_height, FREE_IMAGE_FILTER filter);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_GetWidth")]
        public static extern uint FreeImage_GetWidth(IntPtr handle);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_GetHeight")]
        public static extern uint FreeImage_GetHeight(IntPtr handle);
        
        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_GetBits")]
        public static extern IntPtr FreeImage_GetBits(IntPtr handle);

        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_GetBPP")]
        private static extern int FreeImage_GetBPP(IntPtr dib);
        
        [DllImport(FreeImageLibrary, EntryPoint = "FreeImage_GetPitch")]
        private static extern uint FreeImage_GetPitch(IntPtr dib);

        public static Texture2D LoadImageToTexture(string path, bool linear)
        {
            // Load from file
            IntPtr texHandle = FreeImage.FreeImage_Load(FREE_IMAGE_FORMAT.FIF_TARGA, path, 0);
            // Import texture data
            RawTextureData texData = ImportTextureData(texHandle);
            if (texData == null)
                return null;

            Texture2D tex = new Texture2D(texData.width, texData.height, TextureFormat.BGRA32, false, linear);
            tex.filterMode = FilterMode.Bilinear;
            Debug.Log($"{texData.width}-{texData.height}-{texData.data.Length}");
            tex.LoadRawTextureData(texData.data);
            tex.Apply(true, false);

            if (texHandle != IntPtr.Zero)
                FreeImage.FreeImage_Unload(texHandle);
            return tex;
        }
        
        private RawTextureData ImportTextureFromFile(string texturePath, FREE_IMAGE_FORMAT format, int mipLevels)
        {
            if(!File.Exists(texturePath))
            {
                Debug.LogError($"File does not exist: {texturePath}");
                return null;
            }

            // Load from file
            IntPtr texHandle = FreeImage.FreeImage_Load(format, texturePath, 0);
            // Import texture data
            RawTextureData textureData = ImportTextureData(texHandle);

            if (texHandle != IntPtr.Zero)
                FreeImage.FreeImage_Unload(texHandle);

            return textureData;
        }

        private class RawTextureData
        {
            public byte[] data;
            public int width;
            public int height;
        }

        private static RawTextureData ImportTextureData(IntPtr texHandle)
        {
            uint width = FreeImage.FreeImage_GetWidth(texHandle);
            uint height = FreeImage.FreeImage_GetHeight(texHandle);
            uint size = width * height * 4;

            byte[] data = new byte[size];
            FreeImage.FreeImage_ConvertToRawBits(Marshal.UnsafeAddrOfPinnedArrayElement(data, 0), texHandle, (int)width * 4, 32, 0, 0, 0, false);

            RawTextureData texData = new RawTextureData();
            texData.data = data;
            texData.width = (int)width;
            texData.height = (int)height;

         //   GenerateMipMaps(texHandle, texData);

            return texData;
        }
    }
