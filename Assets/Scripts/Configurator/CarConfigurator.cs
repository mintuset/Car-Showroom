using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the configurable parts of the showroom car: wheel sets, paint, upholstery,
/// engine hood and sunroof.
///
/// Materials embedded in the car FBX are shared assets, so they are never edited directly.
/// On Awake every renderer under <see cref="carRoot"/> gets runtime copies of the paint and
/// upholstery materials; changes made in Play mode therefore never leak into the project.
/// </summary>
public class CarConfigurator : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

    [Header("References")]
    [SerializeField] private Transform carRoot;

    [Header("Wheels")]
    [SerializeField, Tooltip("One GameObject per wheel option, in GleCatalog.Wheels order.")]
    private GameObject[] wheelSets;

    [Header("Paint")]
    [SerializeField] private Material paintMaterial;

    [Header("Upholstery")]
    [SerializeField, Tooltip("Seat and trim leather materials recoloured by the upholstery option.")]
    private Material[] upholsteryMaterials;
    [SerializeField, Tooltip("Seat stitching, tinted slightly lighter than the upholstery.")]
    private Material[] stitchingMaterials;

    [Header("Glass")]
    [SerializeField, Tooltip("Opaque window material from the FBX, swapped for a see-through one so the interior is visible.")]
    private Material windowGlassSource;
    [SerializeField] private Material windowGlassReplacement;

    [Header("Engine Hood")]
    [SerializeField] private Transform hood;
    [SerializeField, Tooltip("Gas struts hinged on the same axis as the hood.")]
    private Transform hoodStruts;
    [SerializeField] private float hoodOpenAngle = -47.7f;

    [Header("Sunroof")]
    [SerializeField] private Transform sunroofPanel;
    [SerializeField, Tooltip("Panel lifts by Y first, then slides by Z.")]
    private Vector3 sunroofOpenOffset = new Vector3(0f, 0.035f, -0.6f);

    [Header("Lighting")]
    [SerializeField, Tooltip("Fades in with the hood so the engine bay is readable.")]
    private Light engineBayLight;
    [SerializeField, Tooltip("Dome light, switched on while the viewer sits inside.")]
    private Light cabinLight;

    [Header("Animation")]
    [SerializeField] private float openDuration = 1.2f;

    private Material paintInstance;
    private readonly List<Material> upholsteryInstances = new List<Material>();
    private readonly List<Material> stitchingInstances = new List<Material>();

    private Quaternion hoodClosedRotation;
    private Quaternion strutsClosedRotation;
    private Vector3 sunroofClosedPosition;
    private float engineBayLightIntensity;
    private float hoodTarget, hoodT;
    private float sunroofTarget, sunroofT;

    public bool HoodOpen => hoodTarget > 0.5f;
    public bool SunroofOpen => sunroofTarget > 0.5f;
    public int WheelIndex { get; private set; }

    private void Awake()
    {
        if (carRoot == null) carRoot = transform;

        CreateMaterialInstances();

        if (hood != null) hoodClosedRotation = hood.localRotation;
        // The struts are authored in their open pose; align them with the closed hood.
        if (hoodStruts != null) strutsClosedRotation = hood != null ? hood.localRotation : hoodStruts.localRotation;
        if (sunroofPanel != null) sunroofClosedPosition = sunroofPanel.localPosition;
        if (engineBayLight != null) engineBayLightIntensity = engineBayLight.intensity;
        SetCabinLight(false);

        ApplyHood(0f);
        ApplySunroof(0f);
    }

    private void OnDestroy()
    {
        if (paintInstance != null) Destroy(paintInstance);
        foreach (Material m in upholsteryInstances) Destroy(m);
        foreach (Material m in stitchingInstances) Destroy(m);
    }

    private void Update()
    {
        float step = Time.deltaTime / Mathf.Max(0.01f, openDuration);

        if (!Mathf.Approximately(hoodT, hoodTarget))
        {
            hoodT = Mathf.MoveTowards(hoodT, hoodTarget, step);
            ApplyHood(hoodT);
        }

        if (!Mathf.Approximately(sunroofT, sunroofTarget))
        {
            sunroofT = Mathf.MoveTowards(sunroofT, sunroofTarget, step);
            ApplySunroof(sunroofT);
        }
    }

    // ---------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------
    public void SetWheels(int index)
    {
        if (wheelSets == null || wheelSets.Length == 0) return;

        WheelIndex = Mathf.Clamp(index, 0, wheelSets.Length - 1);
        for (int i = 0; i < wheelSets.Length; i++)
        {
            if (wheelSets[i] != null) wheelSets[i].SetActive(i == WheelIndex);
        }
    }

    public void SetPaint(GleCatalog.PaintOption paint)
    {
        if (paintInstance == null || paint == null) return;

        bool metallic = paint.Finish == GleCatalog.PaintFinish.Metallic;
        paintInstance.SetColor(BaseColorId, paint.Color);
        // Kept moderate: a high metallic value kills the diffuse base colour and dark paints read as black.
        paintInstance.SetFloat(MetallicId, metallic ? 0.3f : 0.05f);
        paintInstance.SetFloat(SmoothnessId, metallic ? 0.8f : 0.78f);
    }

    public void SetUpholstery(GleCatalog.UpholsteryOption upholstery)
    {
        if (upholstery == null) return;

        foreach (Material m in upholsteryInstances) m.SetColor(BaseColorId, upholstery.Color);

        Color stitch = Color.Lerp(upholstery.Color, Color.white, 0.25f);
        foreach (Material m in stitchingInstances) m.SetColor(BaseColorId, stitch);
    }

    public void SetHoodOpen(bool open) => hoodTarget = open ? 1f : 0f;
    public void SetSunroofOpen(bool open) => sunroofTarget = open ? 1f : 0f;

    public void SetCabinLight(bool on)
    {
        if (cabinLight != null) cabinLight.enabled = on;
    }

    // ---------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------
    private void ApplyHood(float t)
    {
        if (hood == null) return;

        Quaternion swing = Quaternion.AngleAxis(hoodOpenAngle * Mathf.SmoothStep(0f, 1f, t), Vector3.right);
        hood.localRotation = swing * hoodClosedRotation;
        if (hoodStruts != null) hoodStruts.localRotation = swing * strutsClosedRotation;

        if (engineBayLight != null)
        {
            engineBayLight.enabled = t > 0.01f;
            engineBayLight.intensity = engineBayLightIntensity * t;
        }
    }

    private void ApplySunroof(float t)
    {
        if (sunroofPanel == null) return;

        // Tilt up over the first 20% of the travel, then slide back over the roof.
        float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.2f));
        float slide = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.2f) / 0.8f));
        sunroofPanel.localPosition = sunroofClosedPosition + new Vector3(
            sunroofOpenOffset.x * slide,
            sunroofOpenOffset.y * lift,
            sunroofOpenOffset.z * slide);
    }

    private void CreateMaterialInstances()
    {
        var replacements = new Dictionary<Material, Material>();

        if (paintMaterial != null)
        {
            paintInstance = new Material(paintMaterial) { name = paintMaterial.name + " (Runtime)" };
            replacements[paintMaterial] = paintInstance;
        }

        AddInstances(upholsteryMaterials, upholsteryInstances, replacements);
        AddInstances(stitchingMaterials, stitchingInstances, replacements);

        if (windowGlassSource != null && windowGlassReplacement != null)
            replacements[windowGlassSource] = windowGlassReplacement;

        foreach (Renderer r in carRoot.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && replacements.TryGetValue(mats[i], out Material replacement))
                {
                    mats[i] = replacement;
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    private static void AddInstances(Material[] sources, List<Material> instances, Dictionary<Material, Material> replacements)
    {
        if (sources == null) return;

        foreach (Material source in sources)
        {
            if (source == null || replacements.ContainsKey(source)) continue;

            var instance = new Material(source) { name = source.name + " (Runtime)" };
            instances.Add(instance);
            replacements[source] = instance;
        }
    }
}
