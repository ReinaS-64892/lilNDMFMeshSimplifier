using UnityEngine;

namespace jp.lilxyzw.ndmfmeshsimplifier.runtime
{
    [DisallowMultipleComponent]
    internal class NDMFMeshSimplifierOverallManager : MonoBehaviour
#if LIL_VRCSDK3
    , VRC.SDKBase.IEditorOnly
#endif
    {
        public int TargetPolygonCount = 70000;
    }
}
