using LiquiLabs.Vankoo.Invoicing.Shared.Domain.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Domain.Exceptions;

public sealed class LowOcrConfidenceException : BusinessRuleViolationException
{
    public float Confidence { get; }
    public float MinimumRequired { get; }

    public LowOcrConfidenceException(float confidence, float minimumRequired = 0.80f)
        : base("LOW_OCR_CONFIDENCE",
               $"OCR confidence {confidence:P0} is below the required minimum of {minimumRequired:P0}")
    {
        Confidence = confidence;
        MinimumRequired = minimumRequired;
    }
}
