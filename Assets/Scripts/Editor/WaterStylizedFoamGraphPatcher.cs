#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces DepthsFade foam mask with top-face / hex-edge / neighbor-aware mask.
/// Run once after pulling changes: Idle Forest → Shaders → Patch Water Stylized Foam
/// </summary>
public static class WaterStylizedFoamGraphPatcher
{
    private const string HlslPath = "Assets/Shaders/WaterFoam.hlsl";
    private const string SubGraphPath = "Assets/Shaders/WaterTileFoamMask.shadersubgraph";
    private const string MainGraphPath = "Assets/Shaders/WaterStylized.shadergraph";

    // Stable GUID for WaterFoam.hlsl (Custom Function file reference).
    private const string HlslGuid = "d76d588f069f81748825c71c589e8eca";
    // Stable GUID for WaterTileFoamMask.shadersubgraph.
    private const string SubGraphGuid = "c9e2f1a04b3d4c8e9f7a6b5c4d3e2f11";

    [MenuItem("Idle Forest/Shaders/Patch Water Stylized Foam")]
    public static void Patch()
    {
        EnsureHlslMeta();
        WriteSubGraphIfMissing();
        PatchMainGraph();
        AssetDatabase.Refresh();
        Debug.Log("[WaterFoam] Shader graph patched. Re-open WaterStylized if the graph was open.");
    }

    private static void EnsureHlslMeta()
    {
        if (!File.Exists(HlslPath))
        {
            Debug.LogError($"[WaterFoam] Missing {HlslPath}");
            return;
        }

        var metaPath = HlslPath + ".meta";
        if (!File.Exists(metaPath))
        {
            File.WriteAllText(metaPath,
                "fileFormatVersion: 2\n" +
                $"guid: {HlslGuid}\n" +
                "ShaderIncludeImporter:\n" +
                "  externalObjects: {}\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n");
        }
    }

    private static void WriteSubGraphIfMissing()
    {
        if (File.Exists(SubGraphPath))
            return;

        var metaPath = SubGraphPath + ".meta";
        if (!File.Exists(metaPath))
        {
            File.WriteAllText(metaPath,
                "fileFormatVersion: 2\n" +
                $"guid: {SubGraphGuid}\n" +
                "ScriptedImporter:\n" +
                "  internalIDToNameTable: []\n" +
                "  externalObjects: {}\n" +
                "  serializedVersion: 2\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n" +
                "  script: {fileID: 11500000, guid: 60072b568d64c40a485e0fc55012dc9f, type: 3}\n");
        }

        File.WriteAllText(SubGraphPath, BuildSubGraphJson());
    }

    private static void PatchMainGraph()
    {
        if (!File.Exists(MainGraphPath))
        {
            Debug.LogError($"[WaterFoam] Missing {MainGraphPath}");
            return;
        }

        var text = File.ReadAllText(MainGraphPath);
        const string foamNodeMarker = "\"m_ObjectId\": \"7c27fb12d9f84e018c6a67c0b0ad4143\"";
        if (!text.Contains(foamNodeMarker) || text.Contains(SubGraphGuid))
            return;

        var foamStart = text.IndexOf(foamNodeMarker, System.StringComparison.Ordinal);
        var foamEnd = text.IndexOf("\"m_DropdownSelectedEntries\": []", foamStart, System.StringComparison.Ordinal);
        if (foamStart < 0 || foamEnd < 0)
            return;

        foamEnd += "\"m_DropdownSelectedEntries\": []".Length;
        var segment = text.Substring(foamStart, foamEnd - foamStart);
        segment = segment.Replace(
            "\"guid\": \"d4126f2a1562a9e40aebceef3a374897\"",
            $"\"guid\": \"{SubGraphGuid}\"");
        segment = segment.Replace("\"m_Name\": \"DepthsFade\"", "\"m_Name\": \"WaterTileFoamMask\"");
        segment = segment.Replace(
            "    \"m_PropertyGuids\": [\n        \"d6add59c-ac30-41aa-85a6-7ee58e20bb18\"\n    ],\n    \"m_PropertyIds\": [\n        1791164327\n    ],",
            "    \"m_PropertyGuids\": [],\n    \"m_PropertyIds\": [],");

        text = text.Substring(0, foamStart) + segment + text.Substring(foamEnd);
        File.WriteAllText(MainGraphPath, text);
    }

    private static string BuildSubGraphJson()
    {
        var sb = new StringBuilder(32768);
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 3,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.GraphData\",");
        sb.AppendLine("    \"m_ObjectId\": \"a1000000000000000000000000000001\",");
        sb.AppendLine("    \"m_Properties\": [");
        sb.AppendLine("        { \"m_Id\": \"p01foamedge00000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"p02foamedgeb0000000000000000002\" },");
        sb.AppendLine("        { \"m_Id\": \"p03hexradius00000000000000000003\" },");
        sb.AppendLine("        { \"m_Id\": \"p04foamwidth00000000000000000004\" }");
        sb.AppendLine("    ],");
        sb.AppendLine("    \"m_Keywords\": [],");
        sb.AppendLine("    \"m_Dropdowns\": [],");
        sb.AppendLine("    \"m_CategoryData\": [{ \"m_Id\": \"cat0000000000000000000000000001\" }],");
        sb.AppendLine("    \"m_Nodes\": [");
        sb.AppendLine("        { \"m_Id\": \"out0000000000000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"cf0000000000000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"pos000000000000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"nrm000000000000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"prp000000000000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"prp000000000000000000000000002\" },");
        sb.AppendLine("        { \"m_Id\": \"prp000000000000000000000000003\" },");
        sb.AppendLine("        { \"m_Id\": \"prp000000000000000000000000004\" }");
        sb.AppendLine("    ],");
        sb.AppendLine("    \"m_GroupDatas\": [],");
        sb.AppendLine("    \"m_StickyNoteDatas\": [],");
        sb.AppendLine("    \"m_Edges\": [");
        AppendEdge(sb, "pos000000000000000000000000001", 0, "cf0000000000000000000000000001", 0, true);
        AppendEdge(sb, "nrm000000000000000000000000001", 0, "cf0000000000000000000000000001", 1, true);
        AppendEdge(sb, "prp000000000000000000000000001", 0, "cf0000000000000000000000000001", 2, true);
        AppendEdge(sb, "prp000000000000000000000000002", 0, "cf0000000000000000000000000001", 3, true);
        AppendEdge(sb, "prp000000000000000000000000003", 0, "cf0000000000000000000000000001", 4, true);
        AppendEdge(sb, "prp000000000000000000000000004", 0, "cf0000000000000000000000000001", 5, trailingComma: true);
        AppendEdge(sb, "cf0000000000000000000000000001", 6, "out0000000000000000000000000001", 1, trailingComma: false);
        sb.AppendLine("    ],");
        sb.AppendLine("    \"m_VertexContext\": { \"m_Position\": { \"x\": 0.0, \"y\": 0.0 }, \"m_Blocks\": [] },");
        sb.AppendLine("    \"m_FragmentContext\": { \"m_Position\": { \"x\": 0.0, \"y\": 0.0 }, \"m_Blocks\": [] },");
        sb.AppendLine("    \"m_PreviewData\": { \"serializedMesh\": { \"m_SerializedMesh\": \"{\\\"mesh\\\":{\\\"instanceID\\\":0}}\", \"m_Guid\": \"\" }, \"preventRotation\": false },");
        sb.AppendLine("    \"m_Path\": \"Sub Graphs\",");
        sb.AppendLine("    \"m_GraphPrecision\": 1,");
        sb.AppendLine("    \"m_PreviewMode\": 2,");
        sb.AppendLine("    \"m_OutputNode\": { \"m_Id\": \"out0000000000000000000000000001\" },");
        sb.AppendLine("    \"m_SubDatas\": [],");
        sb.AppendLine("    \"m_ActiveTargets\": []");
        sb.AppendLine("}");

        AppendSubGraphOutputNode(sb);
        AppendCustomFunctionNode(sb);
        AppendPositionNode(sb);
        AppendNormalNode(sb);
        AppendVector4Property(sb, "p01foamedge00000000000000000001", "a1010101010101010101010101010101",
            "FoamEdgeMask", "_FoamEdgeMask", true);
        AppendVector2Property(sb, "p02foamedgeb0000000000000000002", "a2020202020202020202020202020202",
            "FoamEdgeMaskB", "_FoamEdgeMaskB", true);
        AppendFloatProperty(sb, "p03hexradius00000000000000000003", "a3030303030303030303030303030303",
            "FoamHexRadius", "_FoamHexRadius", 5f, false);
        AppendFloatProperty(sb, "p04foamwidth00000000000000000004", "a4040404040404040404040404040404",
            "FoamWidth", "_FoamWidth", 2f, false);
        AppendPropertyNode(sb, "prp000000000000000000000000001", "p01foamedge00000000000000000001", "s01prop00000000000000000000001");
        AppendPropertyNode(sb, "prp000000000000000000000000002", "p02foamedgeb0000000000000000002", "s02prop00000000000000000000002");
        AppendPropertyNode(sb, "prp000000000000000000000000003", "p03hexradius00000000000000000003", "s03prop00000000000000000000003");
        AppendPropertyNode(sb, "prp000000000000000000000000004", "p04foamwidth00000000000000000004", "s04prop00000000000000000000004");
        AppendCategory(sb, "cat0000000000000000000000000001",
            "p01foamedge00000000000000000001", "p02foamedgeb0000000000000000002",
            "p03hexradius00000000000000000003", "p04foamwidth00000000000000000004");

        return sb.ToString();
    }

    private static void AppendEdge(StringBuilder sb, string outNode, int outSlot, string inNode, int inSlot, bool trailingComma)
    {
        sb.AppendLine("        {");
        sb.AppendLine("            \"m_OutputSlot\": { \"m_Node\": { \"m_Id\": \"" + outNode + "\" }, \"m_SlotId\": " + outSlot + " },");
        sb.AppendLine("            \"m_InputSlot\": { \"m_Node\": { \"m_Id\": \"" + inNode + "\" }, \"m_SlotId\": " + inSlot + " }");
        sb.AppendLine("        }" + (trailingComma ? "," : ""));
    }

    private static void AppendSubGraphOutputNode(StringBuilder sb)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.SubGraphOutputNode\",");
        sb.AppendLine("    \"m_ObjectId\": \"out0000000000000000000000000001\",");
        sb.AppendLine("    \"m_Name\": \"Output\",");
        sb.AppendLine("    \"m_DrawState\": { \"m_Expanded\": true, \"m_Position\": { \"serializedVersion\": \"2\", \"x\": 900.0, \"y\": 0.0, \"width\": 120.0, \"height\": 76.0 } },");
        sb.AppendLine("    \"m_Slots\": [{ \"m_Id\": \"slotout00000000000000000000001\" }],");
        sb.AppendLine("    \"m_Precision\": 0, \"m_PreviewExpanded\": true, \"m_DismissedVersion\": 0, \"m_PreviewMode\": 0,");
        sb.AppendLine("    \"m_CustomColors\": { \"m_SerializableColors\": [] },");
        sb.AppendLine("    \"IsFirstSlotValid\": true");
        sb.AppendLine("}");
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.Vector1MaterialSlot\",");
        sb.AppendLine("    \"m_ObjectId\": \"slotout00000000000000000000001\",");
        sb.AppendLine("    \"m_Id\": 1, \"m_DisplayName\": \"Mask\", \"m_SlotType\": 0, \"m_Hidden\": false,");
        sb.AppendLine("    \"m_ShaderOutputName\": \"Mask\", \"m_StageCapability\": 2, \"m_Value\": 0.0, \"m_DefaultValue\": 0.0, \"m_Labels\": []");
        sb.AppendLine("}");
    }

    private static void AppendCustomFunctionNode(StringBuilder sb)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 1,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.CustomFunctionNode\",");
        sb.AppendLine("    \"m_ObjectId\": \"cf0000000000000000000000000001\",");
        sb.AppendLine("    \"m_Name\": \"WaterTileFoamMask (Custom Function)\",");
        sb.AppendLine("    \"m_DrawState\": { \"m_Expanded\": true, \"m_Position\": { \"serializedVersion\": \"2\", \"x\": 300.0, \"y\": 0.0, \"width\": 280.0, \"height\": 200.0 } },");
        sb.AppendLine("    \"m_Slots\": [");
        sb.AppendLine("        { \"m_Id\": \"cfs01pos000000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"cfs02nrm000000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"cfs03mask00000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"cfs04maskb0000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"cfs05radius000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"cfs06width00000000000000000001\" },");
        sb.AppendLine("        { \"m_Id\": \"cfs07out000000000000000000001\" }");
        sb.AppendLine("    ],");
        sb.AppendLine("    \"m_Precision\": 0, \"m_PreviewExpanded\": true, \"m_DismissedVersion\": 0, \"m_PreviewMode\": 0,");
        sb.AppendLine("    \"m_CustomColors\": { \"m_SerializableColors\": [] },");
        sb.AppendLine("    \"m_SourceType\": 0,");
        sb.AppendLine("    \"m_FunctionName\": \"WaterTileFoamMask\",");
        sb.AppendLine($"    \"m_FunctionSource\": \"{HlslGuid}\",");
        sb.AppendLine("    \"m_FunctionBody\": \"\"");
        sb.AppendLine("}");
        AppendCfSlot(sb, "cfs01pos000000000000000000001", 0, "PositionOS", 0, "Vector3MaterialSlot");
        AppendCfSlot(sb, "cfs02nrm000000000000000000001", 1, "NormalWS", 0, "Vector3MaterialSlot");
        AppendCfSlot(sb, "cfs03mask00000000000000000001", 2, "FoamEdgeMask", 0, "Vector4MaterialSlot");
        AppendCfSlot(sb, "cfs04maskb0000000000000000001", 3, "FoamEdgeMaskB", 0, "Vector2MaterialSlot");
        AppendCfSlot(sb, "cfs05radius000000000000000001", 4, "HexRadius", 0, "Vector1MaterialSlot");
        AppendCfSlot(sb, "cfs06width00000000000000000001", 5, "FoamWidth", 0, "Vector1MaterialSlot");
        AppendCfSlot(sb, "cfs07out000000000000000000001", 6, "Mask", 1, "Vector1MaterialSlot");
    }

    private static void AppendCfSlot(StringBuilder sb, string id, int slotId, string name, int slotType, string typeName)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine($"    \"m_Type\": \"UnityEditor.ShaderGraph.{typeName}\",");
        sb.AppendLine($"    \"m_ObjectId\": \"{id}\",");
        sb.AppendLine($"    \"m_Id\": {slotId}, \"m_DisplayName\": \"{name}\", \"m_SlotType\": {slotType}, \"m_Hidden\": false,");
        sb.AppendLine($"    \"m_ShaderOutputName\": \"{name}\", \"m_StageCapability\": 3,");
        if (typeName == "Vector1MaterialSlot")
        {
            sb.AppendLine("    \"m_Value\": 0.0, \"m_DefaultValue\": 0.0, \"m_Labels\": []");
        }
        else if (typeName == "Vector2MaterialSlot")
        {
            sb.AppendLine("    \"m_Value\": { \"x\": 0.0, \"y\": 0.0 }, \"m_DefaultValue\": { \"x\": 0.0, \"y\": 0.0 }, \"m_Labels\": []");
        }
        else if (typeName == "Vector3MaterialSlot")
        {
            sb.AppendLine("    \"m_Value\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }, \"m_DefaultValue\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }, \"m_Labels\": []");
        }
        else
        {
            sb.AppendLine("    \"m_Value\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0, \"w\": 0.0 }, \"m_DefaultValue\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0, \"w\": 0.0 }, \"m_Labels\": []");
        }
        sb.AppendLine("}");
    }

    private static void AppendPositionNode(StringBuilder sb)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 1,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.PositionNode\",");
        sb.AppendLine("    \"m_ObjectId\": \"pos000000000000000000000000001\",");
        sb.AppendLine("    \"m_Name\": \"Position\",");
        sb.AppendLine("    \"m_DrawState\": { \"m_Expanded\": true, \"m_Position\": { \"serializedVersion\": \"2\", \"x\": -300.0, \"y\": -120.0, \"width\": 200.0, \"height\": 130.0 } },");
        sb.AppendLine("    \"m_Slots\": [{ \"m_Id\": \"slotpos00000000000000000000001\" }],");
        sb.AppendLine("    \"m_Precision\": 0, \"m_PreviewExpanded\": true, \"m_DismissedVersion\": 0, \"m_PreviewMode\": 0,");
        sb.AppendLine("    \"m_CustomColors\": { \"m_SerializableColors\": [] },");
        sb.AppendLine("    \"m_Space\": 2");
        sb.AppendLine("}");
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.Vector3MaterialSlot\",");
        sb.AppendLine("    \"m_ObjectId\": \"slotpos00000000000000000000001\",");
        sb.AppendLine("    \"m_Id\": 0, \"m_DisplayName\": \"Out\", \"m_SlotType\": 1, \"m_Hidden\": false,");
        sb.AppendLine("    \"m_ShaderOutputName\": \"Out\", \"m_StageCapability\": 3,");
        sb.AppendLine("    \"m_Value\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }, \"m_DefaultValue\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }, \"m_Labels\": []");
        sb.AppendLine("}");
    }

    private static void AppendNormalNode(StringBuilder sb)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.NormalVectorNode\",");
        sb.AppendLine("    \"m_ObjectId\": \"nrm000000000000000000000000001\",");
        sb.AppendLine("    \"m_Name\": \"Normal Vector\",");
        sb.AppendLine("    \"m_DrawState\": { \"m_Expanded\": true, \"m_Position\": { \"serializedVersion\": \"2\", \"x\": -300.0, \"y\": 40.0, \"width\": 200.0, \"height\": 130.0 } },");
        sb.AppendLine("    \"m_Slots\": [{ \"m_Id\": \"slotnrm00000000000000000000001\" }],");
        sb.AppendLine("    \"m_Precision\": 0, \"m_PreviewExpanded\": true, \"m_DismissedVersion\": 0, \"m_PreviewMode\": 0,");
        sb.AppendLine("    \"m_CustomColors\": { \"m_SerializableColors\": [] },");
        sb.AppendLine("    \"m_Space\": 2");
        sb.AppendLine("}");
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.Vector3MaterialSlot\",");
        sb.AppendLine("    \"m_ObjectId\": \"slotnrm00000000000000000000001\",");
        sb.AppendLine("    \"m_Id\": 0, \"m_DisplayName\": \"Out\", \"m_SlotType\": 1, \"m_Hidden\": false,");
        sb.AppendLine("    \"m_ShaderOutputName\": \"Out\", \"m_StageCapability\": 3,");
        sb.AppendLine("    \"m_Value\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }, \"m_DefaultValue\": { \"x\": 0.0, \"y\": 0.0, \"z\": 0.0 }, \"m_Labels\": []");
        sb.AppendLine("}");
    }

    private static void AppendVector4Property(StringBuilder sb, string propId, string guid, string name, string refName, bool perRenderer)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 1,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty\",");
        sb.AppendLine($"    \"m_ObjectId\": \"{propId}\",");
        sb.AppendLine($"    \"m_Guid\": {{ \"m_GuidSerialized\": \"{FormatGuid(guid)}\" }},");
        sb.AppendLine($"    \"m_Name\": \"{name}\",");
        sb.AppendLine("    \"m_DefaultRefNameVersion\": 1,");
        sb.AppendLine($"    \"m_RefNameGeneratedByDisplayName\": \"{name}\",");
        sb.AppendLine($"    \"m_DefaultReferenceName\": \"_{refName.TrimStart('_')}\",");
        sb.AppendLine("    \"m_OverrideReferenceName\": \"\",");
        sb.AppendLine("    \"m_GeneratePropertyBlock\": true,");
        sb.AppendLine("    \"m_UseCustomSlotLabel\": false, \"m_CustomSlotLabel\": \"\", \"m_DismissedVersion\": 0,");
        sb.AppendLine("    \"m_Precision\": 0, \"overrideHLSLDeclaration\": false, \"hlslDeclarationOverride\": 0,");
        sb.AppendLine($"    \"m_Hidden\": false, \"m_PerRendererData\": {(perRenderer ? "true" : "false")},");
        sb.AppendLine("    \"m_customAttributes\": [],");
        sb.AppendLine("    \"m_Value\": { \"x\": 1.0, \"y\": 1.0, \"z\": 1.0, \"w\": 1.0 }");
        sb.AppendLine("}");
    }

    private static void AppendVector2Property(StringBuilder sb, string propId, string guid, string name, string refName, bool perRenderer)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 1,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty\",");
        sb.AppendLine($"    \"m_ObjectId\": \"{propId}\",");
        sb.AppendLine($"    \"m_Guid\": {{ \"m_GuidSerialized\": \"{FormatGuid(guid)}\" }},");
        sb.AppendLine($"    \"m_Name\": \"{name}\",");
        sb.AppendLine("    \"m_DefaultRefNameVersion\": 1,");
        sb.AppendLine($"    \"m_RefNameGeneratedByDisplayName\": \"{name}\",");
        sb.AppendLine($"    \"m_DefaultReferenceName\": \"_{refName.TrimStart('_')}\",");
        sb.AppendLine("    \"m_OverrideReferenceName\": \"\",");
        sb.AppendLine("    \"m_GeneratePropertyBlock\": true,");
        sb.AppendLine("    \"m_UseCustomSlotLabel\": false, \"m_CustomSlotLabel\": \"\", \"m_DismissedVersion\": 0,");
        sb.AppendLine("    \"m_Precision\": 0, \"overrideHLSLDeclaration\": false, \"hlslDeclarationOverride\": 0,");
        sb.AppendLine($"    \"m_Hidden\": false, \"m_PerRendererData\": {(perRenderer ? "true" : "false")},");
        sb.AppendLine("    \"m_customAttributes\": [],");
        sb.AppendLine("    \"m_Value\": { \"x\": 1.0, \"y\": 1.0 }");
        sb.AppendLine("}");
    }

    private static void AppendFloatProperty(StringBuilder sb, string propId, string guid, string name, string refName, float value, bool perRenderer)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 1,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty\",");
        sb.AppendLine($"    \"m_ObjectId\": \"{propId}\",");
        sb.AppendLine($"    \"m_Guid\": {{ \"m_GuidSerialized\": \"{FormatGuid(guid)}\" }},");
        sb.AppendLine($"    \"m_Name\": \"{name}\",");
        sb.AppendLine("    \"m_DefaultRefNameVersion\": 1,");
        sb.AppendLine($"    \"m_RefNameGeneratedByDisplayName\": \"{name}\",");
        sb.AppendLine($"    \"m_DefaultReferenceName\": \"_{refName.TrimStart('_')}\",");
        sb.AppendLine("    \"m_OverrideReferenceName\": \"\",");
        sb.AppendLine("    \"m_GeneratePropertyBlock\": true,");
        sb.AppendLine("    \"m_UseCustomSlotLabel\": false, \"m_CustomSlotLabel\": \"\", \"m_DismissedVersion\": 0,");
        sb.AppendLine("    \"m_Precision\": 0, \"overrideHLSLDeclaration\": false, \"hlslDeclarationOverride\": 0,");
        sb.AppendLine($"    \"m_Hidden\": false, \"m_PerRendererData\": {(perRenderer ? "true" : "false")},");
        sb.AppendLine("    \"m_customAttributes\": [],");
        sb.AppendLine($"    \"m_Value\": {value}, \"m_FloatType\": 0,");
        sb.AppendLine("    \"m_RangeValues\": { \"x\": 0.0, \"y\": 10.0 }, \"m_SliderType\": 0, \"m_SliderPower\": 3.0,");
        sb.AppendLine("    \"m_EnumType\": 0, \"m_CSharpEnumString\": \"\", \"m_EnumNames\": [\"Default\"], \"m_EnumValues\": [0]");
        sb.AppendLine("}");
    }

    private static void AppendPropertyNode(StringBuilder sb, string nodeId, string propId, string slotId)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.PropertyNode\",");
        sb.AppendLine($"    \"m_ObjectId\": \"{nodeId}\",");
        sb.AppendLine("    \"m_Name\": \"Property\",");
        sb.AppendLine("    \"m_DrawState\": { \"m_Expanded\": true, \"m_Position\": { \"serializedVersion\": \"2\", \"x\": 0.0, \"y\": 0.0, \"width\": 130.0, \"height\": 34.0 } },");
        sb.AppendLine($"    \"m_Slots\": [{{ \"m_Id\": \"{slotId}\" }}],");
        sb.AppendLine("    \"m_Precision\": 0, \"m_PreviewExpanded\": true, \"m_DismissedVersion\": 0, \"m_PreviewMode\": 0,");
        sb.AppendLine("    \"m_CustomColors\": { \"m_SerializableColors\": [] },");
        sb.AppendLine($"    \"m_Property\": {{ \"m_Id\": \"{propId}\" }}");
        sb.AppendLine("}");
    }

    private static void AppendCategory(StringBuilder sb, string catId, params string[] childIds)
    {
        sb.AppendLine("{");
        sb.AppendLine("    \"m_SGVersion\": 0,");
        sb.AppendLine("    \"m_Type\": \"UnityEditor.ShaderGraph.CategoryData\",");
        sb.AppendLine($"    \"m_ObjectId\": \"{catId}\",");
        sb.AppendLine("    \"m_Name\": \"\",");
        sb.AppendLine("    \"m_ChildObjectList\": [");
        for (int i = 0; i < childIds.Length; i++)
        {
            sb.Append("        { \"m_Id\": \"").Append(childIds[i]).Append("\" }");
            sb.AppendLine(i < childIds.Length - 1 ? "," : "");
        }
        sb.AppendLine("    ]");
        sb.AppendLine("}");
    }

    private static string FormatGuid(string hex32)
    {
        if (hex32.Length != 32)
            throw new System.ArgumentException($"Expected 32 hex chars, got {hex32.Length}: {hex32}");

        return $"{hex32.Substring(0, 8)}-{hex32.Substring(8, 4)}-{hex32.Substring(12, 4)}-{hex32.Substring(16, 4)}-{hex32.Substring(20, 12)}";
    }
}
#endif
