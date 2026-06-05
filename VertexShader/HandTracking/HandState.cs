// Scripts/HandTracking/HandState.cs

using UnityEngine;

[System.Serializable]
public class HandState
{
    // Joint positions (26 joints)
    public Vector3[] jointPositions = new Vector3[26];

    // Palm data
    public Vector3 palmCenter;
    public Vector3 palmNormal;
    public Quaternion palmRotation;

    // Curl values (0~1)
    public float thumbCurl;
    public float indexCurl;
    public float middleCurl;
    public float ringCurl;
    public float pinkyCurl;

    // Grip state
    public GripType currentGrip;
    public float gripStrength;
}

public enum GripType
{
    None,
    PowerGrip,      // Full grip
    PrecisionGrip,  // Precision grip
    PinchGrip,      // Pinch grip
    CustomGrip
}