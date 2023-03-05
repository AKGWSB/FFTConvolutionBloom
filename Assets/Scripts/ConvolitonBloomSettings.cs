using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.Rendering;

[Serializable]
[CreateAssetMenu(fileName = "ConvolitonBloom", menuName = "ConvolitonBloomSettings")]
public class ConvolitonBloomSettings : ScriptableObject
{
    [SerializeField]
    public Material KernelGenerateMaterial = null;

    [SerializeField]
    public Material SourceGenerateMaterial = null;

    [SerializeField]
    public Material FinalBlitMaterial = null;

    [SerializeField]
    public Texture2D KernelTexture = null;

    [SerializeField]
    public ComputeShader FFTComputeShader = null;

    [SerializeField]
    public float Intensity = 1.0f;

    [SerializeField]
    public float Threshold = 0.0f;

    [HideInInspector]
    public Vector2 KernelPositionOffset = new Vector2(0, 0);

    [SerializeField]
    public Vector2 KernelSizeScale = new Vector2(1, 1);

    [SerializeField]
    public float KernelDistanceExp = 0.0f;

    [SerializeField]
    public float KernelDistanceExpClampMin = 1.0f;

    [SerializeField]
    public float KernelDistanceExpScale = 1.0f;

    [SerializeField]
    public bool KernelImageUseLuminanceAsRGB = false;
}
