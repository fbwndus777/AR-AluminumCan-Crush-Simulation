// Scripts/HandTracking/GripPatternRecognizer.cs

using UnityEngine;

public class GripPatternRecognizer
{
    /// <summary>
    /// Checks if the hand is in a can-holding pose (relaxed grip)
    /// </summary>
    public bool IsCanHoldingPose(HandState hand)
    {
        // Thumb: 40~85%
        bool thumbOK = hand.thumbCurl > 0.40f && hand.thumbCurl < 0.85f;

        // Index: 30~70%
        bool indexOK = hand.indexCurl > 0.30f && hand.indexCurl < 0.70f;

        // Middle: 30~70%
        bool middleOK = hand.middleCurl > 0.30f && hand.middleCurl < 0.70f;

        // Not a full fist (ring/pinky < 70%)
        bool notFullFist = hand.ringCurl < 0.70f && hand.pinkyCurl < 0.70f;

        return thumbOK && indexOK && middleOK && notFullFist;
    }

    /// <summary>
    /// Checks if the hand is squeezing hard
    /// </summary>
    public bool IsSqueezingHard(HandState hand)
    {
        bool indexHard = hand.indexCurl > 0.70f;
        bool middleHard = hand.middleCurl > 0.70f;
        bool thumbHard = hand.thumbCurl > 0.70f;

        return indexHard && middleHard && thumbHard;
    }

    /// <summary>
    /// Calculates squeeze amount (0~1)
    /// </summary>
    public float CalculateSqueezeAmount(HandState hand)
    {
        float avgCurl = (hand.indexCurl + hand.middleCurl + hand.thumbCurl) / 3f;
        return Mathf.Clamp01(avgCurl - 0.5f) * 2f;
    }

    /// <summary>
    /// Classifies grip type
    /// </summary>
    public GripType ClassifyGrip(HandState hand)
    {
        // Pinch grip (thumb + index)
        if (hand.thumbCurl > 0.50f &&
            hand.indexCurl > 0.50f &&
            hand.middleCurl < 0.30f &&
            hand.ringCurl < 0.30f)
        {
            return GripType.PinchGrip;
        }

        // Precision grip (thumb + index + middle)
        if (hand.thumbCurl > 0.40f &&
            hand.indexCurl > 0.40f &&
            hand.middleCurl > 0.40f &&
            hand.ringCurl < 0.40f)
        {
            return GripType.PrecisionGrip;
        }

        // Power grip (all fingers curled)
        if (hand.thumbCurl > 0.60f &&
            hand.indexCurl > 0.60f &&
            hand.middleCurl > 0.60f &&
            hand.ringCurl > 0.50f)
        {
            return GripType.PowerGrip;
        }

        return GripType.None;
    }

    /// <summary>
    /// Calculates grip strength - overload 1 (auto classify)
    /// </summary>
    public float CalculateGripStrength(HandState hand)
    {
        GripType gripType = ClassifyGrip(hand);
        return CalculateGripStrength(hand, gripType);
    }

    /// <summary>
    /// Calculates grip strength - overload 2 (with grip type)
    /// </summary>
    public float CalculateGripStrength(HandState hand, GripType gripType)
    {
        switch (gripType)
        {
            case GripType.PinchGrip:
                return (hand.thumbCurl + hand.indexCurl) / 2f;

            case GripType.PrecisionGrip:
                return (hand.thumbCurl + hand.indexCurl + hand.middleCurl) / 3f;

            case GripType.PowerGrip:
                return (hand.thumbCurl + hand.indexCurl + hand.middleCurl +
                       hand.ringCurl + hand.pinkyCurl) / 5f;

            default:
                return 0f;
        }
    }
}