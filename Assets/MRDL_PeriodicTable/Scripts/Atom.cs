using UnityEngine;

public class Atom : MonoBehaviour
{
    const int AtomLayer = 9;

    public float Radius = 1f;
    [Range(0.1f, 10)]
    public float NucleusFlowSpeed = 5f;
    [Range(0.001f, 1f)]
    public float NucleusJitter = 0.05f;
    [Range(0.1f, 1f)]
    public float NucleusHoldShape = 0.5f;
    [Range(0.001f, 1f)]
    public float NucleusChangeSpeedOdds = 0.25f;

    public Transform ScaleTransform;
    public bool Collapse = false;
    public int NumProtons;
    public int NumNeutrons;
    public int NumElectrons;
    public int Frame = 0;
    public bool Instanced = false;
    public int[] ActiveElectronShells;
    public Mesh Mesh;
    public Mesh[] ElectronShellMeshes;
    public Material ProtonMat;
    public Material NeutronMat;

    Vector3[] nucleusTargetPositions;
    Vector3[] nucleusCurrentPositions;
    Matrix4x4[] protonMatrixes;
    Matrix4x4[] neutronMatrixes;
    Quaternion[] randomRotations;
    MaterialPropertyBlock propertyBlock;
    Vector3 atomScale;
    Vector3 finalScale;

    private void OnEnable()
    {
        RefreshProperties();
        atomScale = Vector3.one * 0.001f;
        transform.localScale = Vector3.one * 0.01f;
        if (ScaleTransform == null)
            ScaleTransform = transform.parent;
    }

    private void Update()
    {
        // ... (기존 Update 코드 상단은 유지)
        RefreshProperties();

        Vector3 pos = transform.position;
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z)) return;

        for (int i = 0; i < nucleusTargetPositions.Length; i++)
        {
            if (Random.value < NucleusChangeSpeedOdds)
            {
                Vector3 newPos = nucleusTargetPositions[i] + Random.insideUnitSphere * (1f - NucleusHoldShape);
                newPos = Vector3.MoveTowards(Vector3.zero, newPos, 1f);
                nucleusTargetPositions[i] = newPos;
            }
        }

        if (Collapse)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * 0.01f, Time.deltaTime * 5);
            atomScale = Vector3.Lerp(atomScale, Vector3.one * 0.001f, Time.deltaTime * 5);
        }
        else
        {
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.deltaTime);
            atomScale = Vector3.Lerp(atomScale, Vector3.one, Time.deltaTime);
        }

        // lossyScale 절댓값 + 최솟값 보정
        Vector3 raw = ScaleTransform != null ? ScaleTransform.lossyScale : Vector3.one;
        Vector3 safeScale = new Vector3(
            Mathf.Max(Mathf.Abs(raw.x), 0.0001f),
            Mathf.Max(Mathf.Abs(raw.y), 0.0001f),
            Mathf.Max(Mathf.Abs(raw.z), 0.0001f));

        finalScale = Vector3.Scale(atomScale, safeScale);
        if (finalScale.x <= 0f) return;

        for (int i = 0; i < NumProtons + NumNeutrons; i++)
        {
            nucleusCurrentPositions[i] = Vector3.Lerp(
                nucleusCurrentPositions[i],
                nucleusTargetPositions[i],
                Time.deltaTime * NucleusFlowSpeed);

            Vector3 jitter = Random.insideUnitSphere * NucleusJitter;
            Vector3 atomPos = pos + (nucleusCurrentPositions[i] + jitter) * Radius * finalScale.x;

            if (i < NumProtons)
                protonMatrixes[i] = Matrix4x4.TRS(atomPos, randomRotations[i % randomRotations.Length], finalScale);
            else
                neutronMatrixes[i - NumProtons] = Matrix4x4.TRS(atomPos, randomRotations[i % randomRotations.Length], finalScale);
        }

        Graphics.DrawMeshInstanced(Mesh, 0, ProtonMat, protonMatrixes, protonMatrixes.Length,
            propertyBlock, UnityEngine.Rendering.ShadowCastingMode.Off, false, AtomLayer);
        Graphics.DrawMeshInstanced(Mesh, 0, NeutronMat, neutronMatrixes, neutronMatrixes.Length,
            propertyBlock, UnityEngine.Rendering.ShadowCastingMode.Off, false, AtomLayer);
    }

    // [중요] 애니메이션이 끝난 후 스케일을 다시 한번 검사하여 물리 엔진 오류 방지
    private void LateUpdate()
    {
        if (ScaleTransform != null)
        {
            Vector3 localScale = ScaleTransform.localScale;
            // 스케일이 0에 너무 가깝거나 음수이면 강제로 최소값으로 고정 (물리 엔진 오류 방지)
            if (localScale.x < 0.01f || localScale.y < 0.01f || localScale.z < 0.01f)
            {
                ScaleTransform.localScale = new Vector3(
                    Mathf.Max(Mathf.Abs(localScale.x), 0.01f),
                    Mathf.Max(Mathf.Abs(localScale.y), 0.01f),
                    Mathf.Max(Mathf.Abs(localScale.z), 0.01f));
            }
        }
    }

    private void RefreshProperties()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
            ProtonMat.enableInstancing = true;
            NeutronMat.enableInstancing = true;
        }

        if (randomRotations == null)
        {
            randomRotations = new Quaternion[10];
            for (int i = 0; i < randomRotations.Length; i++)
                randomRotations[i] = Quaternion.Euler(Random.value * 360, Random.value * 360, Random.value * 360);
        }

        if (nucleusTargetPositions == null || nucleusTargetPositions.Length < NumProtons + NumNeutrons)
        {
            nucleusTargetPositions = new Vector3[NumProtons + NumNeutrons];
            nucleusCurrentPositions = new Vector3[NumProtons + NumNeutrons];
            protonMatrixes = new Matrix4x4[NumProtons];
            neutronMatrixes = new Matrix4x4[NumNeutrons];
            for (int i = 0; i < nucleusTargetPositions.Length; i++)
            {
                nucleusTargetPositions[i] = Random.onUnitSphere;
                nucleusCurrentPositions[i] = nucleusTargetPositions[i] * 5f;
            }
        }
    }
}