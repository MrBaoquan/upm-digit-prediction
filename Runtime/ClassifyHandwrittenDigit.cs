using UnityEngine;
using Unity.Sentis;
using Unity.Sentis.Layers;
using System.Linq;
using System.Collections.Generic;

namespace DigitPrediction
{
    public class ClassifyHandwrittenDigit : MonoBehaviour
    {
        public ModelAsset mnistONNX;

        // engine type
        Worker worker;

        Model runtimeModel;

        // This small model works just as fast on the CPU as well as the GPU:
        static BackendType backendType = BackendType.GPUCompute;

        // input tensor
        Tensor<float> inputTensor = null;

        // op to manipulate Tensors
        // Ops ops;

        void Start()
        {
            // load the neural network model from the asset:
            Model sourceModel = ModelLoader.Load(mnistONNX);

            // Create a functional graph that runs the input model and then applies softmax to the output.
            FunctionalGraph graph = new FunctionalGraph();
            FunctionalTensor[] inputs = graph.AddInputs(sourceModel);
            FunctionalTensor[] outputs = Functional.Forward(sourceModel, inputs);
            FunctionalTensor softmax = Functional.Softmax(outputs[0]);

            // Create a model with softmax by compiling the functional graph.
            runtimeModel = graph.Compile(softmax);

            // create the neural network engine:
            // worker = WorkerFactory.CreateWorker(backendType, sourceModel);

            // // CreateOps allows direct operations on tensors.
            // ops = WorkerFactory.CreateOps(backendType, null);
            worker = new Worker(runtimeModel, backendType);
        }

        public class RecogResult
        {
            public float probability;
            public int predictedNumber;
            public List<float> probabilities;
            public Texture2D previewTex;
            public Texture2D paddedTex;
            public Texture2D croppedTex;
        }

        // Sends the image to the neural network model and returns the probability that the image is each particular digit.
        public RecogResult GetMostLikelyDigitProbability(Texture2D inputTex)
        {
            inputTensor?.Dispose();

            var cropedTexture = DigitPrediction.Utils.TextureTool.CropTextureWithPadding(inputTex);
            if (cropedTexture.paddedTexture is null)
            {
                return new RecogResult
                {
                    probability = 0,
                    predictedNumber = -1,
                    probabilities = new List<float>()
                };
            }
            var _inputTexParam = preprocessImage(cropedTexture.paddedTexture);

            // Convert the texture into a tensor, it has width=W, height=W, and channels=1:
            inputTensor = TextureConverter.ToTensor(_inputTexParam, 28, 28, 1);

            // run the neural network:
            worker.Schedule(inputTensor);

            // We get a reference to the output of the neural network while keeping it on the GPU
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;

            var results = outputTensor.DownloadToArray();

            var predictedNumber = results.ToList().IndexOf(results.Max());
            var probability = results.Max();

            return new RecogResult
            {
                probability = probability,
                predictedNumber = predictedNumber,
                probabilities = results.ToList(),
                previewTex = inputTex,
                paddedTex = cropedTexture.paddedTexture,
                croppedTex = cropedTexture.croppedTexture
            };
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
            worker?.Dispose();
            // ops?.Dispose();
        }
    }
}
