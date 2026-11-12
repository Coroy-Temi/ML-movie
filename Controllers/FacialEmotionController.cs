using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ML_MOVIE.ML.Inference;
using ML_MOVIE.Models;
using ML_MOVIE.Services;

namespace ML_MOVIE.Controllers;

[Authorize]
[ApiController]
[Route("api/facial-emotion")]
public sealed class FacialEmotionController : ControllerBase
{
    private readonly IEmotionInferenceService _emotionInferenceService;

    public FacialEmotionController(IEmotionInferenceService emotionInferenceService)
    {
        _emotionInferenceService = emotionInferenceService;
    }

    [HttpPost("predict")]
    public ActionResult<FacialEmotionApiResponse> Predict([FromBody] FacialEmotionApiRequest request)
    {
        var prediction = _emotionInferenceService.PredictFacialEmotionDetailed(new FacialEmotionInputFeatures
        {
            SmileScore = request.SmileScore,
            BrowTension = request.BrowTension,
            EyeOpenness = request.EyeOpenness,
            MouthWidthRatio = request.MouthWidthRatio,
            MouthHeightRatio = request.MouthHeightRatio,
            BrowRaiseRatio = request.BrowRaiseRatio,
            EyeOpenRatio = request.EyeOpenRatio,
            CheekLiftRatio = request.CheekLiftRatio,
            JawDropRatio = request.JawDropRatio
        });

        if (prediction.Emotion is null)
        {
            return Ok(new FacialEmotionApiResponse
            {
                Emotion = "Curious",
                Source = "Fallback prediction",
                Confidence = 0.35f
            });
        }

        return Ok(new FacialEmotionApiResponse
        {
            Emotion = prediction.Emotion.Value.ToString(),
            Source = prediction.Source,
            Confidence = prediction.Confidence
        });
    }
}
