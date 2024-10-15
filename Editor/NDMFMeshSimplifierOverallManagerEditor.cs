using System.Collections.Generic;
using System.Linq;
using jp.lilxyzw.ndmfmeshsimplifier.runtime;
using UnityEditor;
using UnityEngine;

namespace jp.lilxyzw.ndmfmeshsimplifier
{
    [CustomEditor(typeof(NDMFMeshSimplifierOverallManager))]
    internal class NDMFMeshSimplifierOverallManagerEditor : Editor
    {
        bool includeDisableRenderers = false;
        NDMFMeshSimplifier[] meshSimplifiers;
        Dictionary<NDMFMeshSimplifier, ManageTarget> manageStates;
        SerializedProperty sTargetPolygonCount;
        SerializedObject serializedMeshSimplifiers;
        bool OptionsFoldout = false;

        GUIStyle redStyle;

        GUIContent[] intDisplayOptions = new GUIContent[]{
            new("PC-Poor"),
            new("Quest-Poor"),
        };
        int[] intOptions = new int[]{
            70000,
            20000,
        };
        public sealed override void OnInspectorGUI()
        {
            L10n.SelectLanguageGUI();
            serializedObject.UpdateIfRequiredOrScript();
            sTargetPolygonCount ??= serializedObject.FindProperty(nameof(NDMFMeshSimplifierOverallManager.TargetPolygonCount));
            var targetObj = target as NDMFMeshSimplifierOverallManager;

            includeDisableRenderers = GUILayout.Toggle(includeDisableRenderers, "IncludeDisableRenderers");
            if (GUILayout.Button("Generate lilNDMFMeshSimplifiers!"))
            {
                foreach (var r in targetObj.GetComponentsInChildren<Renderer>(includeDisableRenderers))
                {
                    if (r.GetComponent<NDMFMeshSimplifier>() == null && ((r is SkinnedMeshRenderer smr && smr.sharedMesh != null) || (r is MeshRenderer && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null)))
                    {
                        var meshSimplifier = Undo.AddComponent<NDMFMeshSimplifier>(r.gameObject);
                        meshSimplifier.quality = 1f;
                    }
                }

                meshSimplifiers = null;
                manageStates = null;
                serializedMeshSimplifiers = null;
            }
            if (meshSimplifiers == null || manageStates == null) { FindMeshSimplifiers(targetObj); }

            if (GUILayout.Button(NDMF.PreviewNDMFMeshSimplifier.EnableNode.IsEnabled.Value ? "NDMFMeshSimplifier Preview is Enable" : "NDMFMeshSimplifier Preview is Disable"))
            {
                NDMF.PreviewNDMFMeshSimplifier.EnableNode.IsEnabled.Value = !NDMF.PreviewNDMFMeshSimplifier.EnableNode.IsEnabled.Value;
            }

            if (meshSimplifiers.Any(i => i != null) is false)
            {
                EditorGUILayout.LabelField("NDMFMeshSimplifier is not Found");
                serializedObject.ApplyModifiedProperties();
                if (GUILayout.Button("Re find")) { FindMeshSimplifiers(targetObj); }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(sTargetPolygonCount);
                EditorGUILayout.IntPopup(sTargetPolygonCount, intDisplayOptions, intOptions, GUIContent.none, GUILayout.Width(128f));
            }

            var nowEstimatedPolygonCount = manageStates.Sum(s => s.Value.EstimatedPolygonCountFromQuality);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Estimated TotalPolygonCount", GUILayout.MinWidth(32f));
                var isOverflow = sTargetPolygonCount.intValue < nowEstimatedPolygonCount;
                if (isOverflow)
                {
                    redStyle ??= new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red } };
                    EditorGUILayout.LabelField(nowEstimatedPolygonCount + "-Overflow!", redStyle, GUILayout.MinWidth(32f));
                }
                else { EditorGUILayout.LabelField(nowEstimatedPolygonCount.ToString(), GUILayout.MinWidth(32f)); }
                using (new EditorGUI.DisabledGroupScope(isOverflow is false))
                    if (GUILayout.Button("Force reduce quality")) { ForceReduceQuality(); }
            }

            for (var i = 0; meshSimplifiers.Length > i; i += 1)
            {
                var ms = meshSimplifiers[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    manageStates[ms].SObj.Update();
                    manageStates[ms].LockQuality = GUILayout.Toggle(manageStates[ms].LockQuality, "", GUILayout.Width(18f));

                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(manageStates[ms].SQuality, GUIContent.none);
                    var Changed = EditorGUI.EndChangeCheck();

                    EditorGUILayout.ObjectField("", ms, typeof(NDMFMeshSimplifier), true, GUILayout.Width(128f));
                    manageStates[ms].SObj.ApplyModifiedProperties();

                    if (Changed) AdjustQuality(ms);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("All Half")) { SetAll(0.5f); }
                if (GUILayout.Button("All One")) { SetAll(1f); }
                void SetAll(float v)
                {
                    foreach (var ms in meshSimplifiers) { manageStates[ms].SObj.Update(); }
                    foreach (var ms in meshSimplifiers) { manageStates[ms].SQuality.floatValue = v; }
                    foreach (var ms in meshSimplifiers) { manageStates[ms].SObj.ApplyModifiedProperties(); }
                }
            }

            OptionsFoldout = EditorGUILayout.Foldout(OptionsFoldout, "Options View");
            using (new EditorGUI.IndentLevelScope())
                if (OptionsFoldout)
                {
                    serializedMeshSimplifiers.UpdateIfRequiredOrScript();
                    var iterator = serializedMeshSimplifiers.GetIterator();
                    iterator.NextVisible(true); // m_Script
                    iterator.NextVisible(true); // quality
                    while (iterator.NextVisible(false))
                    {
                        if (iterator.name == "options") iterator.NextVisible(true);
                        EditorGUILayout.PropertyField(iterator, L10n.G(iterator));
                    }
                    serializedMeshSimplifiers.ApplyModifiedProperties();
                }
            serializedObject.ApplyModifiedProperties();

        }

        private void FindMeshSimplifiers(NDMFMeshSimplifierOverallManager targetObj)
        {
            meshSimplifiers = targetObj.GetComponentsInChildren<NDMFMeshSimplifier>(true).Where(r => (r.GetComponent<SkinnedMeshRenderer>() != null && r.GetComponent<SkinnedMeshRenderer>().sharedMesh != null) || (r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null)).ToArray();
            manageStates = meshSimplifiers.ToDictionary(s => s, s => new ManageTarget(s));
            serializedMeshSimplifiers = new(meshSimplifiers);
        }

        private void AdjustQuality(NDMFMeshSimplifier nowEdit, bool reclusiveAdjust = false)
        {
            if (meshSimplifiers == null || manageStates == null) { return; }
            foreach (var ms in manageStates) { ms.Value.SObj.Update(); }
            var targetCount = sTargetPolygonCount.intValue;
            var nowAllEstimatedCount = manageStates.Sum(s => s.Value.EstimatedPolygonCountFromQuality);
            var AdjustmentPolygonCount = nowAllEstimatedCount - targetCount;

            var notEditablePolygonCount = manageStates[nowEdit].EstimatedPolygonCountFromQuality;
            var notEditableCount = 1;
            foreach (var ms in manageStates) { if (ms.Key != nowEdit && ms.Value.LockQuality) { notEditablePolygonCount += ms.Value.EstimatedPolygonCountFromQuality; notEditableCount += 1; } }
            var editableCount = manageStates.Count - notEditableCount;

            if (reclusiveAdjust) AdjustmentPolygonCount += editableCount;// 少し小さめに調整しないと溢れる
            var needReduceQuality = AdjustmentPolygonCount / (float)(nowAllEstimatedCount - notEditablePolygonCount);
            foreach (var ms in manageStates) { if (ms.Key != nowEdit && ms.Value.LockQuality is false) { ms.Value.SQuality.floatValue -= needReduceQuality; ms.Value.QualityClamp(); } }
            foreach (var ms in manageStates) { ms.Value.SObj.ApplyModifiedProperties(); }

            var result = manageStates.Sum(s => s.Value.EstimatedPolygonCountFromQuality);
            if (result > targetCount && reclusiveAdjust is false)//大きくなりすぎる場合があるから、調整するためにもう一度呼ぶ。
            {
                AdjustQuality(nowEdit, true);
            }
        }
        private void ForceReduceQuality()
        {
            if (meshSimplifiers == null || manageStates == null) { return; }
            var targetCount = sTargetPolygonCount.intValue;
            var nowEstimatedCount = manageStates.Sum(s => s.Value.EstimatedPolygonCountFromQuality);

            var needReducePolygonCount = nowEstimatedCount - targetCount;
            if (needReducePolygonCount <= 0) { return; }

            foreach (var ms in manageStates) { ms.Value.SObj.Update(); }
            var reduceQuality = needReducePolygonCount / (float)nowEstimatedCount;
            foreach (var ms in manageStates) { ms.Value.SQuality.floatValue -= reduceQuality; }
            foreach (var ms in manageStates) { ms.Value.SObj.ApplyModifiedProperties(); }


            // var eachReduceCount = needReducePolygonCount / meshSimplifiers.Length;
            // eachReduceCount += (needReducePolygonCount % meshSimplifiers.Length) > 0 ? 1 : 0;//余りがあった場合全体的に減らして誤魔化す。

            // foreach (var ms in meshSimplifiers) { manageStates[ms].SObj.Update(); }
            // foreach (var ms in meshSimplifiers) { manageStates[ms].EstimatedPolygonCountFromQuality -= eachReduceCount; }
            // foreach (var ms in meshSimplifiers) { manageStates[ms].SObj.ApplyModifiedProperties(); }
        }

        class ManageTarget
        {
            public NDMFMeshSimplifier Simplifier;
            public SerializedObject SObj;
            public SerializedProperty SQuality;
            public readonly int SourcePolygonCount;
            public bool LockQuality;
            public ManageTarget(NDMFMeshSimplifier simplifier)
            {
                Simplifier = simplifier;
                SObj = new(Simplifier);
                SQuality = SObj.FindProperty(nameof(NDMFMeshSimplifier.quality));


                var renderer = Simplifier.GetComponent<SkinnedMeshRenderer>();
                var filter = Simplifier.GetComponent<MeshFilter>();
                var mesh = renderer != null ? renderer.sharedMesh : filter.sharedMesh;

                SourcePolygonCount = 0;
                for (var si = 0; mesh.subMeshCount > si; si += 1)
                {
                    SourcePolygonCount += mesh.GetSubMesh(si).indexCount / 3;// Triangle 以外ないものとして考えます。
                }

                LockQuality = false;
            }

            public int EstimatedPolygonCountFromQuality
            {
                get { return Mathf.RoundToInt(SourcePolygonCount * SQuality.floatValue); }
                set { SQuality.floatValue = Mathf.Max(1, value) / (float)SourcePolygonCount; }
            }

            public void QualityClamp()
            {
                SQuality.floatValue = Mathf.Clamp01(Mathf.Max(1, EstimatedPolygonCountFromQuality) / (float)SourcePolygonCount);
            }
        }
    }
}
