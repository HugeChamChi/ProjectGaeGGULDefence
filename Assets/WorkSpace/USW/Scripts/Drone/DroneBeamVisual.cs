using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reusable world-space quads using the teleport laboratory's laser and glow materials.</summary>
public sealed class DroneBeamVisual : MonoBehaviour
{
    private Renderer _beam, _muzzle, _contact;
    private MaterialPropertyBlock _block;

    /// <summary>Builds a fixed visual pool. Materials are shared with the laboratory.</summary>
    public void Initialize(Material beam, Material glow)
    {
        if (_beam == null)
        {
            _block = new MaterialPropertyBlock();
            _beam = Quad("LabBeam", 40);
            _muzzle = Quad("MuzzleGlow", 41);
            _contact = Quad("ContactGlow", 42);
        }
        _beam.sharedMaterial = beam;
        _muzzle.sharedMaterial = _contact.sharedMaterial = glow;
        Clear();
    }

    /// <summary>Draws a muzzle-anchored beam and its endpoint glows without applying damage.</summary>
    public void Draw(Vector3 from, Vector3 to, Color tint, float width, float alpha, float charge, float contact)
    {
        if (_beam == null) return;
        // Keep effects in front of the board and character geometry.
        from.z = to.z = -.8f;
        Vector3 delta = from - to;
        _beam.enabled = alpha > .001f && delta.sqrMagnitude > .000001f && _beam.sharedMaterial != null;
        if (_beam.enabled)
        {
            Pose(_beam.transform, (from + to) * .5f,
                Quaternion.FromToRotation(Vector3.up, delta.normalized), new Vector3(width, delta.magnitude, 1));
            _block.Clear();
            _block.SetColor("_Color", tint);
            _block.SetFloat("_Alpha", alpha);
            _block.SetFloat("_Length", delta.magnitude);
            _block.SetFloat("_Seed", 0);
            _beam.SetPropertyBlock(_block);
        }
        Glow(_muzzle, from, width * 2.5f, tint, charge);
        Glow(_contact, to, width * 3f, tint, contact);
    }

    /// <summary>Hides every quad immediately on cancellation or pool return.</summary>
    public void Clear()
    {
        if (_beam != null) _beam.enabled = _muzzle.enabled = _contact.enabled = false;
    }

    private Renderer Quad(string label, int order)
    {
        var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sortingLayerName = "FX";
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static void Pose(Transform target, Vector3 position, Quaternion rotation, Vector3 size)
    {
        target.SetPositionAndRotation(position, rotation);
        target.localScale = Vector3.one;
        var scale = target.lossyScale;
        target.localScale = new Vector3(size.x / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
            size.y / Mathf.Max(.0001f, Mathf.Abs(scale.y)), size.z / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
    }

    private void Glow(Renderer renderer, Vector3 position, float size, Color color, float alpha)
    {
        renderer.enabled = alpha > .001f && renderer.sharedMaterial != null;
        Pose(renderer.transform, position, Quaternion.identity, Vector3.one * size);
        _block.Clear(); color.a = alpha; _block.SetColor("_Color", color);
        renderer.SetPropertyBlock(_block);
    }

    private void OnDisable() => Clear();
}
