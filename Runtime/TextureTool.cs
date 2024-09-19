using UnityEngine;

namespace DigitPrediction
{
    public static class TextureTool
    {
        public static Texture2D cropTextureWithPadding(Texture2D source)
        {
            source = ResizeByBlit(source, 1000, 1000);
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

            // 计算最小包围矩形的宽度和高度
            int width = xmax - xmin + 1;
            int height = ymax - ymin + 1;

            // 添加 1/4 宽度的边距
            var padding = Mathf.Max(width, height) / 5;
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

            // 创建新纹理，并将其填充为黑色
            Texture2D resultTexture = new Texture2D(extendedSize, extendedSize);
            Color[] blackPixels = new Color[extendedSize * extendedSize];
            for (int i = 0; i < blackPixels.Length; i++)
            {
                blackPixels[i] = Color.black;
            }
            resultTexture.SetPixels(blackPixels);

            // 计算水平和垂直方向的偏移，使内容居中
            int xOffset = (extendedSize - width) / 2;
            int yOffset = (extendedSize - height) / 2;

            // 获取裁剪区域的像素
            Color[] croppedPixels = source.GetPixels(xmin, ymin, width, height);

            // 将裁剪的内容复制到新纹理的居中位置
            resultTexture.SetPixels(xOffset, yOffset, width, height, croppedPixels);
            resultTexture.Apply();

            return resultTexture;
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
