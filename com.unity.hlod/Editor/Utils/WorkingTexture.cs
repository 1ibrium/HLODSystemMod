using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Color = UnityEngine.Color;
using File = UnityEngine.Windows.File;
using Graphics = UnityEngine.Graphics;

namespace Unity.HLODSystem.Utils
{

    public static class TextureExtensions
    {
        public static WorkingTexture ToWorkingTexture(this Texture2D texture, Allocator allocator)
        {
            var wt = new WorkingTexture(allocator, texture);
           
            return wt;
            
        }
    }
    public class WorkingTexture : IDisposable
    {
        private NativeArray<int> m_detector = new NativeArray<int>(1, Allocator.Persistent);
        
        private WorkingTextureBuffer m_buffer;

        public string Name
        {
            set { m_buffer.Name = value; }
            get { return m_buffer.Name; }
        }

        public TextureFormat Format => m_buffer.Format;
        public int Width => m_buffer.Widht;

        public int Height => m_buffer.Height;

        public bool Linear
        {
            set => m_buffer.Linear = value;
            get => m_buffer.Linear;
        }

        public TextureWrapMode WrapMode
        {
            set => m_buffer.WrapMode = value;
            get => m_buffer.WrapMode;
        }
        
        private WorkingTexture()
        {
            
        }
        public WorkingTexture(Allocator allocator, TextureFormat format, int width, int height, bool linear)
        {
            m_buffer = WorkingTextureBufferManager.Instance.Create(allocator, format, width, height, linear);
        }

        public WorkingTexture(Allocator allocator, Texture2D source)
        {
            m_buffer = WorkingTextureBufferManager.Instance.Get(allocator, source);
        }

        public void Dispose()
        {
            m_buffer.Release();
            m_buffer = null;

            m_detector.Dispose();
        }

        public WorkingTexture Clone()
        {
            WorkingTexture nwt = new WorkingTexture();
            nwt.m_buffer = m_buffer;
            nwt.m_buffer.AddRef();

            return nwt;
        }

        public Texture2D ToTexture()
        {
            return m_buffer.ToTexture();
        }
        
        public Guid GetGUID()
        {
            return m_buffer.GetGUID(); 
                
        }

        public void SetPixel(int x, int y, Color color)
        {
            MakeWriteable();
            
            m_buffer.SetPixel(x, y, color);

        }
   

        public Color GetPixel(int x, int y)
        {
            return m_buffer.GetPixel(x, y);
        }

        public Color GetPixel(float u, float v)
        {
            float x = u * (Width - 1);
            float y = v * (Height - 1);
            
            int x1 = Mathf.FloorToInt(x);
            int x2 = Mathf.CeilToInt(x);

            int y1 = Mathf.FloorToInt(y);
            int y2 = Mathf.CeilToInt(y);

            float xWeight = x - x1;
            float yWeight = y - y1;

            Color c1 = Color.Lerp(GetPixel(x1, y1), GetPixel(x2, y1), xWeight);
            Color c2 = Color.Lerp(GetPixel(x1, y2), GetPixel(x2, y2), xWeight);

            return Color.Lerp(c1, c2, yWeight);
        }

        public void Blit(WorkingTexture source, int x, int y)
        {
            MakeWriteable();
           
            m_buffer.Blit(source.m_buffer, x, y);
        }
        

        
      

        public WorkingTexture Resize(Allocator allocator, int newWidth, int newHeight)
        {
            WorkingTexture wt = new WorkingTexture(allocator, m_buffer.Format, newWidth, newHeight, m_buffer.Linear);

            float xWeight = (float) (m_buffer.Widht - 1) / (float) (newWidth - 1);
            float yWeight = (float) (m_buffer.Height - 1) / (float) (newHeight - 1);

            for (int y = 0; y < newHeight; ++y)
            {
                for (int x = 0; x < newWidth; ++x)
                {
                    float xpos = x * xWeight;
                    float ypos = y * yWeight;

                    float u = xpos / Width;
                    float v = ypos / Height;

                    wt.SetPixel(x, y, GetPixel(u, v));
                }
            }
            
            return wt;
        }

       

        private void MakeWriteable()
        {
            if (m_buffer.GetRefCount() > 1)
            {
                WorkingTextureBuffer newBuffer = WorkingTextureBufferManager.Instance.Clone(m_buffer);
                m_buffer.Release();
                m_buffer = newBuffer;
            }
        }
    }

    public class WorkingTextureBufferManager
    {
        private static WorkingTextureBufferManager s_instance;
        public static WorkingTextureBufferManager Instance
        {
            get
            {
                if ( s_instance == null )
                    s_instance = new WorkingTextureBufferManager();

                return s_instance;
            }
        }


        private Dictionary<Texture2D, WorkingTextureBuffer> m_cache = new Dictionary<Texture2D, WorkingTextureBuffer>();
        public WorkingTextureBuffer Get(Allocator allocator, Texture2D texture)
        {
            WorkingTextureBuffer buffer = null;
            if (m_cache.ContainsKey(texture) == true)
            {
                buffer = m_cache[texture];
                
            }
            else
            {
                buffer = new WorkingTextureBuffer(allocator, texture);
                m_cache.Add(texture, buffer);
            }
            buffer.AddRef();
            return buffer;
        }

        public WorkingTextureBuffer Create(Allocator allocator, TextureFormat format, int width, int height, bool linear)
        {
            WorkingTextureBuffer buffer = new WorkingTextureBuffer(allocator, format, width, height, linear);
            buffer.AddRef();
            return buffer;
        }

        public WorkingTextureBuffer Clone(WorkingTextureBuffer buffer)
        {
            WorkingTextureBuffer nb = buffer.Clone();
            nb.AddRef();
            return nb;
        }

        public void Destroy(WorkingTextureBuffer buffer)
        {
            if (buffer.HasSource())
            {
                m_cache.Remove(buffer.GetSource());
            }
        }
    }
    
    public class WorkingTextureBuffer : IDisposable
    {
        private Allocator m_allocator;
        private TextureFormat m_format;
        private int m_width;
        private int m_height;
        private bool m_linear;
        
        private NativeArray<Color> m_pixels;
        
        private int m_refCount;
        private Texture2D m_source;

        private Guid m_guid;
        
        private TextureWrapMode m_wrapMode = TextureWrapMode.Repeat;

        public string Name { set; get; }

        public TextureFormat Format => m_format;
        public int Widht => m_width;
        public int Height => m_height;
        public bool Linear
        {
            set => m_linear = value;
            get => m_linear;
        }

        public TextureWrapMode WrapMode
        {
            get => m_wrapMode;
            set => m_wrapMode = value;
        }


        public WorkingTextureBuffer(Allocator allocator, TextureFormat format, int width, int height, bool linear)
        {
            m_allocator = allocator;
            m_format = format;
            m_width = width;
            m_height = height;
            m_linear = linear;
            m_pixels = new NativeArray<Color>( width * height, allocator);
            m_refCount = 0;
            m_source = null;
            m_guid = Guid.NewGuid();
        }

        public WorkingTextureBuffer(Allocator allocator, Texture2D source) 
            : this(allocator, source.format, source.width, source.height, !GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat))
        {
            Name = source.name;
            m_source = source;
            CopyFrom(source);
            m_guid = GUIDUtils.ObjectToGUID(source);
        }

        public WorkingTextureBuffer Clone()
        {
            WorkingTextureBuffer buffer = new WorkingTextureBuffer(m_allocator, m_format, m_width, m_height, m_linear);
            buffer.Blit(this, 0, 0);
            return buffer;
        }
        public Texture2D ToTexture()
        {
            Texture2D texture = new Texture2D(m_width, m_height, m_format, false, m_linear);
            texture.name = Name;
            texture.SetPixels(m_pixels.ToArray());
            texture.wrapMode = m_wrapMode;
            texture.Apply();
            return texture;
        }
        
        public Guid GetGUID()
        {
            return m_guid;
        }

        public bool HasSource()
        {
            return m_source != null;
        }
        public Texture2D GetSource()
        {
            return m_source;
        }

        public void AddRef()
        {
            m_refCount += 1;
        }

        public void Release()
        {
            m_refCount -= 1;

            if (m_refCount == 0)
            {
                WorkingTextureBufferManager.Instance.Destroy(this);
                Dispose();
            }
        }

        public int GetRefCount()
        {
            return m_refCount;
        }
        
        public void Dispose()
        {
            if( m_pixels.IsCreated )
                m_pixels.Dispose();
        }

        public void SetPixel(int x, int y, Color color)
        {
            m_pixels[y * m_width + x] = color;
        }

        public Color GetPixel(int x, int y)
        {
            return m_pixels[y * m_width + x];
        }

        public void Blit(WorkingTextureBuffer source, int x, int y)
        {
            int width = source.m_width;
            int height = source.m_height;

            for (int sy = 0; sy < height; ++sy)
            {
                int ty = y + sy;
                if ( ty < 0 || ty >= m_height )
                    continue;

                for (int sx = 0; sx < width; ++sx)
                {
                    int tx = x + sx;
                    if (tx < 0 || tx >= m_width)
                        continue;

                    SetPixel(tx, ty, source.GetPixel(sx, sy));
                }
            }
        }
        
        private void CopyFrom(Texture2D texture)
        {
            //make to texture readable.
            var t2dPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", AssetDatabase.GetAssetPath(texture)));
            var assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            var textureImporter = assetImporter as TextureImporter;
            TextureImporterType type = TextureImporterType.Default;

            m_linear = !GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat);
            m_wrapMode = texture.wrapMode;

            // try
            // {
                Color[] texturePixels = null;
                int count = m_width * m_height;
                if (textureImporter)
                {
                    var pathExtension = Path.GetExtension(t2dPath);
                    bool isTiff = pathExtension.StartsWith(".tif", System.StringComparison.OrdinalIgnoreCase);
                    bool isTga = pathExtension.StartsWith(".tga", System.StringComparison.OrdinalIgnoreCase);
                    //handle tiff case：
                    if (isTiff)
                    {

                        var riffTex = LoadTiffTexture(t2dPath);

                        if (riffTex.width != texture.width || riffTex.height != texture.height)
                        {
                            riffTex = ScaleTexture(riffTex, texture.width, texture.height);
                            riffTex.Apply(true, false);
                        }

                        texturePixels = riffTex.GetPixels();
                    }
                    else if (isTga)
                    {
                        Texture2D t2d = FreeImage.LoadImageToTexture(t2dPath, m_linear);
                        if (t2d.width != texture.width || t2d.height != texture.height)
                        {
                            t2d = ScaleTexture(t2d, texture.width, texture.height);
                            t2d.Apply(true, false);
                        }
                        texturePixels = t2d.GetPixels();

                        // var tagTex2d = LoadTGA(t2dPath);
                        // if (tagTex2d.width != texture.width || tagTex2d.height != texture.height)
                        // {
                        //     tagTex2d = ScaleTexture(tagTex2d, texture.width, texture.height);
                        //     tagTex2d.Apply(true, false);
                        // }
                        // texturePixels = tagTex2d.GetPixels();
                    }
                    else
                    {
                        var readHolder = new Texture2D(m_width, m_height, texture.format, true, m_linear);
                        var rawBytes = File.ReadAllBytes(t2dPath);
                        if (!ImageConversion.LoadImage(readHolder, rawBytes))
                        {
                            Debug.LogError($"ImageConversion.LoadImage failed @ {rawBytes.Length}-{t2dPath}");
                        }
                        readHolder.Apply(true, false);
                        texturePixels = readHolder.GetPixels();
                    }
                }
                else
                {
                    //maybe code texture?
//                    Debug.LogError($"textureImporter == false @ {t2dPath}", texture);    
                    texturePixels = texture.GetPixels();
                }

                if (texturePixels.Length != count)
                {
                    //TODO: logging
                    return;
                }

                m_pixels.Slice(0, count).CopyFrom(texturePixels);
            // }
            // catch (Exception e)
            // {
            //     Debug.LogException(e);
            // }
        }
        
        Color32 IntToColor(int aCol)
        {
            Color32 c = default;
            c.b = (byte)((aCol) & 0xFF);
            c.g = (byte)((aCol>>8) & 0xFF);
            c.r = (byte)((aCol>>16) & 0xFF);
            c.a = (byte)((aCol>>24) & 0xFF);
            return c;
        }

        private Texture2D LoadTiffTexture(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError("File not found: " + path);
                return null;
            }

            try
            {
                using (Bitmap image = (Bitmap)Image.FromFile(path))
                {
                    if (image == null)
                    {
                        Debug.LogError("Failed to load TIFF image.");
                        return null;
                    }

                    int width = image.Width;
                    int height = image.Height;
                    Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, m_linear);

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            System.Drawing.Color pixel = image.GetPixel(x, y);
                            texture.SetPixel(x, height - 1 - y, new Color32(pixel.R, pixel.G, pixel.B, pixel.A)); // Flip vertically
                        }
                    }

                    texture.Apply(true, false);
                    return texture;
                }
            }
            catch
            {
                Debug.LogError($"Failed to load @ {path}.");
                throw;
            }
        }
        
        public static Texture2D ScaleTexture(Texture2D source, int width, int height)
        {
            var oldRt = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(width, height);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(width, height, source.format, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = oldRt;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }
        
        
        public static Texture2D LoadTGA(string fileName)
        {
            using (var imageFile = System.IO.File.OpenRead(fileName))
            {
                return LoadTGA(imageFile);
            }
        }
     
        public static Texture2D LoadTGA(Stream TGAStream)
        {
       
            using (BinaryReader r = new BinaryReader(TGAStream))
            {
                // Skip some header info we don't care about.
                // Even if we did care, we have to move the stream seek point to the beginning,
                // as the previous method in the workflow left it at the end.
                r.BaseStream.Seek(12, SeekOrigin.Begin);
     
                short width = r.ReadInt16();
                short height = r.ReadInt16();
                int bitDepth = r.ReadByte();
     
                // Skip a byte of header information we don't care about.
                r.BaseStream.Seek(1, SeekOrigin.Current);
     
                Texture2D tex = new Texture2D(width, height);
                Color32[] pulledColors = new Color32[width * height];
     
                if (bitDepth == 32)
                {
                    for (int i = 0; i < width * height; i++)
                    {
                        byte red = r.ReadByte();
                        byte green = r.ReadByte();
                        byte blue = r.ReadByte();
                        byte alpha = r.ReadByte();
     
                        pulledColors [i] = new Color32(blue, green, red, alpha);
                    }
                } else if (bitDepth == 24)
                {
                    for (int i = 0; i < width * height; i++)
                    {
                        byte red = r.ReadByte();
                        byte green = r.ReadByte();
                        byte blue = r.ReadByte();
                       
                        pulledColors [i] = new Color32(blue, green, red, 1);
                    }
                } else
                {
                    throw new Exception("TGA texture had non 32/24 bit depth.");
                }
     
                tex.SetPixels32(pulledColors);
                tex.Apply();
                return tex;
     
            }
        }
    }

}