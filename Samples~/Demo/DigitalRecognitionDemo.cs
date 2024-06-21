using System.Collections;
using System.Collections.Generic;
using UNIHper;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DigitalRecognitionDemo : MonoBehaviour
{
    public Painting painting;
    public ClassifyHandwrittenDigit classifyHandwrittenDigit;
    public TextMeshProUGUI debugText;
    public RawImage previewImage;

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update()
    {
        // Press space key to recognize
        if (Input.GetKeyDown(KeyCode.Space))
        {
            var _result = classifyHandwrittenDigit.GetMostLikelyDigitProbability(
                ToTexture2D(painting.raw.texture)
            );

            var _debugText =
                "Predicted number: "
                + _result.predictedNumber
                + " with probability: "
                + _result.probability;

            debugText.text = _debugText;
            previewImage.texture = _result.previewTex;
        }
    }

    Texture2D ToTexture2D(Texture texture)
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
}
