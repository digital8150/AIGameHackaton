using UnityEngine;
using UnityEngine.UI;

namespace DuskTwilight
{
    /// <summary>
    /// Gives a procedural UI shader its own material instance and keeps its _Size (rect size in px) current.
    /// Other title scripts animate properties on <see cref="Material"/>.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(Graphic))]
    public class UIShaderSize : MonoBehaviour
    {
        static readonly int SizeId = Shader.PropertyToID("_Size");

        Graphic graphic;
        Material instance;

        public Material Material
        {
            get { Ensure(); return instance; }
        }

        void OnEnable() { Ensure(); Push(); }

        void OnRectTransformDimensionsChange() => Push();

        void Ensure()
        {
            if (!graphic) graphic = GetComponent<Graphic>();
            if (instance || !graphic || !graphic.material) return;
            // Edit mode: write into the shared material preview only (no leaked instances in the scene file).
            if (!Application.isPlaying) { instance = graphic.material; return; }
            instance = new Material(graphic.material) { name = graphic.material.name + " (Instance)" };
            graphic.material = instance;
        }

        void Push()
        {
            if (!Application.isPlaying) return;
            Ensure();
            if (!instance) return;
            var r = ((RectTransform)transform).rect;
            instance.SetVector(SizeId, new Vector4(r.width, r.height, 0, 0));
        }

        void OnDestroy()
        {
            if (Application.isPlaying && instance) Destroy(instance);
        }
    }
}
