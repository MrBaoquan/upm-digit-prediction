using UnityEngine;
using Unity.Sentis;
using Unity.Sentis.Layers;
using System.Linq;
using System.Collections.Generic;

public class ClassifyHandwrittenDigit : MonoBehaviour
{
    public ModelAsset mnistONNX;

    // engine type
    IWorker engine;

    // This small model works just as fast on the CPU as well as the GPU:
    static BackendType backendType = BackendType.GPUCompute;

    // input tensor
    TensorFloat inputTensor = null;

    // op to manipulate Tensors
    Ops ops;

    void Start()
    {
        // load the neural network model from the asset:
        Model model = ModelLoader.Load(mnistONNX);
        // create the neural network engine:
        engine = WorkerFactory.CreateWorker(backendType, model);

        // CreateOps allows direct operations on tensors.
        ops = WorkerFactory.CreateOps(backendType, null);
    }

    // Sends the image to the neural network model and returns the probability that the image is each particular digit.
    public (
        float probability,
        int predictedNumber,
        Texture2D previewTex,
        List<float> probabilities
    ) GetMostLikelyDigitProbability(Texture2D inputTex)
    {
        var _inputTexParam = preprocessImage(cropTextureWithPadding(inputTex));
        inputTensor?.Dispose();

        // Convert the texture into a tensor, it has width=W, height=W, and channels=1:
        inputTensor = TextureConverter.ToTensor(_inputTexParam, 28, 28, 1);

        // run the neural network:
        engine.Execute(inputTensor);

        // We get a reference to the output of the neural network while keeping it on the GPU
        TensorFloat result = engine.PeekOutput() as TensorFloat;

        // convert the result to probabilities between 0..1 using the softmax function:
        var probabilities = ops.Softmax(result);
        var indexOfMaxProba = ops.ArgMax(probabilities, -1, false);

        // We need to make the result from the GPU readable on the CPU
        probabilities.MakeReadable();
        indexOfMaxProba.MakeReadable();

        var predictedNumber = indexOfMaxProba[0];
        var probability = probabilities[predictedNumber];

        List<float> probabilitiesList = new List<float>();
        for (int i = 0; i < 10; i++)
        {
            probabilitiesList.Add(probabilities[i]);
        }

        return (probability, predictedNumber, _inputTexParam, probabilitiesList);
    }

    private Texture2D cropTextureWithPadding(Texture2D source)
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

        // 如果宽度小于高度，则调整宽度以匹配高度，并居中内容
        if (width < height)
        {
            int diff = height - width;
            xmin -= diff / 2;
            xmax += diff / 2;

            // 确保裁剪区域不超出源图像的范围
            xmin = Mathf.Clamp(xmin, 0, source.width - 1);
            xmax = Mathf.Clamp(xmax, 0, source.width - 1);

            width = xmax - xmin + 1;
        }

        // 创建新纹理并复制像素数据
        Texture2D croppedTexture = new Texture2D(width, height);
        Color[] pixels = source.GetPixels(xmin, ymin, width, height);
        croppedTexture.SetPixels(pixels);
        croppedTexture.Apply();

        return croppedTexture;
    }

    public Texture2D ResizeByBlit(
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

    private Texture2D preprocessImage(Texture2D source)
    {
        int targetWidth = 28;
        int targetHeight = 28;

        RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(targetWidth, targetHeight);
        result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
        result.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    // Clean up all our resources at the end of the session so we don't leave anything on the GPU or in memory:
    private void OnDestroy()
    {
        inputTensor?.Dispose();
        engine?.Dispose();
        ops?.Dispose();
    }
}
