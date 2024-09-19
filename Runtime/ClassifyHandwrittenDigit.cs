using UnityEngine;
using Unity.Sentis;
using Unity.Sentis.Layers;
using System.Linq;
using System.Collections.Generic;
using DigitPrediction;

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
        var _inputTexParam = preprocessImage(TextureTool.cropTextureWithPadding(inputTex));
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
