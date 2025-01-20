using UnityEngine;

namespace DigitPrediction.Utils
{
    public static class TextureTool
    {
        public static Texture2D ToTexture2D(this Texture texture)
        {
            RenderTexture _rt = new RenderTexture(texture.width, texture.height, 0);
            Graphics.Blit(texture, _rt);

            Texture2D _texture2D = new Texture2D(texture.width, texture.height);
            RenderTexture.active = _rt;
            _texture2D.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
            _texture2D.Apply();

            RenderTexture.active = null;
            _rt.Release();
            return _texture2D;
        }

        public static (Texture2D croppedTexture, Texture2D paddedTexture) CropTextureWithPadding(
            Texture2D source
        )
        {
            int xmin = source.width;
            int xmax = 0;
            int ymin = source.height;
            int ymax = 0;

            // 寻找手写内容的边界
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    Color color = source.GetPixel(x, y);
                    if (color.r > 0.5f) // 白色或者近白色
                    {
                        if (x < xmin)
                            xmin = x;
                        if (x > xmax)
                            xmax = x;
                        if (y < ymin)
                            ymin = y;
                        if (y > ymax)
                            ymax = y;
                    }
                }
            }

            if (xmin == source.width || xmax == 0 || ymin == source.height || ymax == 0)
            {
                return (null, null);
            }

            // 计算最小包围矩形的宽度和高度
            int width = xmax - xmin + 1;
            int height = ymax - ymin + 1;

            // 裁剪的内容（没有 padding 的版本）
            Texture2D croppedTexture = new Texture2D(width, height);
            Color[] croppedPixels = source.GetPixels(xmin, ymin, width, height);
            croppedTexture.SetPixels(croppedPixels);
            croppedTexture.Apply();

            // 添加 1/4 宽度的边距
            var padding = Mathf.Max(width, height) / 6;
            xmin -= padding;
            xmax += padding;
            ymin -= padding;
            ymax += padding;

            // 确保裁剪区域不超出源图像的范围
            xmin = Mathf.Clamp(xmin, 0, source.width - 1);
            xmax = Mathf.Clamp(xmax, 0, source.width - 1);
            ymin = Mathf.Clamp(ymin, 0, source.height - 1);
            ymax = Mathf.Clamp(ymax, 0, source.height - 1);

            width = xmax - xmin + 1;
            height = ymax - ymin + 1;

            // 计算要创建的最终纹理大小（确保宽度和高度相等，以实现居中）
            int maxDimension = Mathf.Max(width, height);
            int extendedSize = maxDimension + padding * 2;

            // 创建新纹理（带 padding 的版本）
            Texture2D paddedTexture = new Texture2D(extendedSize, extendedSize);
            Color[] blackPixels = new Color[extendedSize * extendedSize];
            for (int i = 0; i < blackPixels.Length; i++)
            {
                blackPixels[i] = Color.black;
            }
            paddedTexture.SetPixels(blackPixels);

            // 计算水平和垂直方向的偏移，使内容居中
            int xOffset = (extendedSize - width) / 2;
            int yOffset = (extendedSize - height) / 2;

            // 获取裁剪区域的像素（包括 padding 的版本）
            Color[] paddedCroppedPixels = source.GetPixels(xmin, ymin, width, height);

            // 将裁剪的内容复制到新纹理的居中位置
            paddedTexture.SetPixels(xOffset, yOffset, width, height, paddedCroppedPixels);
            paddedTexture.Apply();

            // 返回两个纹理
            return (croppedTexture, paddedTexture);
        }

        public static Texture2D ResizeByBlit(
            Texture texture,
            int width,
            int height,
            FilterMode filterMode = FilterMode.Bilinear
        )
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default,
                1
            );
            temporary.filterMode = FilterMode.Bilinear;
            RenderTexture.active = temporary;
            GL.Clear(clearDepth: false, clearColor: true, new Color(1f, 1f, 1f, 0f));
            bool sRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = false;
            Graphics.Blit(texture, temporary);
            Texture2D texture2D = new Texture2D(
                width,
                height,
                TextureFormat.ARGB32,
                mipChain: true,
                linear: false
            );
            texture2D.filterMode = filterMode;
            texture2D.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(temporary);
            GL.sRGBWrite = sRGBWrite;
            return texture2D;
        }
    }
}
