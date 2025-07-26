using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRA
{
    // 定义了一个数据结构，用于从XML文件中加载我们的配置。
    public class SRAWalllDef : Def
    {
        // 以下所有 public 字段都会由RimWorld的加载系统自动从XML同名节点中填充。
        public ThingDef targetDef;
        public ShaderTypeDef shaderType;

        public string texPath_Corner_NW;
        public string texPath_Corner_NE;
        public string texPath_Corner_SW;
        public string texPath_Corner_SE;
        public string texPath_Diagonal_NW;
        public string texPath_Diagonal_NE;
        public string texPath_Diagonal_SW;
        public string texPath_Diagonal_SE;

        // 提供一个全局静态访问点，方便在代码中随时获取配置，而无需传来传去。
        public static SRAWalllDef Current => DefDatabase<SRAWalllDef>.GetNamed("SRAWalllConfig");
    }

    // 这是核心的渲染类，继承自SectionLayer，负责在地图上绘制额外的视觉效果。
    public class SectionLayer_SRAWalll : SectionLayer
    {
        public enum CornerType { None, Corner_NW, Corner_NE, Corner_SW, Corner_SE, Diagonal_NW, Diagonal_NE, Diagonal_SW, Diagonal_SE }

        private static readonly Vector2[] UVs = { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        [TweakValue("SRAWalllCorners", 0f, 2f)]
        private static float HullCornerScale = 2f;

        private static CachedMaterial mat_Corner_NW, mat_Corner_NE, mat_Corner_SW, mat_Corner_SE, mat_Diagonal_NW, mat_Diagonal_NE, mat_Diagonal_SW, mat_Diagonal_SE;
        private static readonly float cornerAltitude = AltitudeLayer.BuildingOnTop.AltitudeFor();
        private static bool initalized;

        private static readonly IntVec3[] Directions = { IntVec3.North, IntVec3.East, IntVec3.South, IntVec3.West, IntVec3.North + IntVec3.West, IntVec3.North + IntVec3.East, IntVec3.South + IntVec3.East, IntVec3.South + IntVec3.West };
        private static bool[] tmpChecks = new bool[Directions.Length];

        // 获取要使用的着色器。优先从XML配置中读取，如果未配置则使用一个安全合理的默认值。
        private static Shader WallShader => SRAWalllDef.Current.shaderType?.Shader ?? ShaderDatabase.CutoutComplex;

        public override bool Visible => true;

        public SectionLayer_SRAWalll(Section section) : base(section)
        {
            // 告诉游戏，当建筑、地形等发生变化时，需要重绘我们的图层。
            relevantChangeTypes = (ulong)MapMeshFlagDefOf.Buildings | (ulong)MapMeshFlagDefOf.Terrain | (ulong)MapMeshFlagDefOf.Things | (ulong)MapMeshFlagDefOf.Roofs;
        }

        // 这是一个性能优化技巧，确保贴图和材质只在第一次使用时被加载和创建，而不是每次重绘都加载。
        private static void EnsureInitialized()
        {
            if (!initalized)
            {
                initalized = true;
                SRAWalllDef config = SRAWalllDef.Current;

                mat_Corner_NW = new CachedMaterial(config.texPath_Corner_NW, WallShader);
                mat_Corner_NE = new CachedMaterial(config.texPath_Corner_NE, WallShader);
                mat_Corner_SW = new CachedMaterial(config.texPath_Corner_SW, WallShader);
                mat_Corner_SE = new CachedMaterial(config.texPath_Corner_SE, WallShader);
                mat_Diagonal_NW = new CachedMaterial(config.texPath_Diagonal_NW, WallShader);
                mat_Diagonal_NE = new CachedMaterial(config.texPath_Diagonal_NE, WallShader);
                mat_Diagonal_SW = new CachedMaterial(config.texPath_Diagonal_SW, WallShader);
                mat_Diagonal_SE = new CachedMaterial(config.texPath_Diagonal_SE, WallShader);
            }
        }

        // 游戏引擎会定期调用这个方法来更新和重绘我们的图层。
        public override void Regenerate()
        {
            if (!SRAMod.AngledSRAWall)
            {return;}
            ClearSubMeshes(MeshParts.All);
            Map map = base.Map;
            TerrainGrid terrGrid = map.terrainGrid;
            foreach (IntVec3 item in section.CellRect)
            {
                if (ShouldDrawCornerPiece(item, map, terrGrid, out var cornerType, out var color))
                {
                    CachedMaterial material = GetMaterial(cornerType);
                    IntVec3 offset = GetOffset(cornerType);
                    bool addGravshipMask = false;
                    bool addIndoorMask = IsCornerIndoorMasked(item, cornerType, map);
                    AddQuad(material.Material, item + offset, HullCornerScale, cornerAltitude, color, addGravshipMask, addIndoorMask);
                }
            }
            FinalizeMesh(MeshParts.All);
        }

        // 这是整个框架的核心判断逻辑。
        // 它检查给定的坐标，分析其周围8个方向的建筑，来决定是否需要绘制斜角，以及绘制哪种类型的斜角。
        public static bool ShouldDrawCornerPiece(IntVec3 pos, Map map, TerrainGrid terrGrid, out CornerType cornerType, out Color color)
        {
            cornerType = CornerType.None;
            color = Color.white;
            if (pos.GetEdifice(map) != null) return false;
            TerrainDef terrainDef = terrGrid.FoundationAt(pos);
            if (terrainDef != null && terrainDef.IsSubstructure) return false;
            ThingDef targetDef = SRAWalllDef.Current.targetDef;
            if (targetDef == null) { Log.ErrorOnce("SRAWalllConfig has a null targetDef. Please check SRAWalllDefs.xml.", 168168168); return false; }
            for (int i = 0; i < Directions.Length; i++) { tmpChecks[i] = (pos + Directions[i]).GetEdificeSafe(map)?.def == targetDef; }
            if (tmpChecks[0] && tmpChecks[3] && !tmpChecks[2] && !tmpChecks[1]) cornerType = (tmpChecks[4] ? CornerType.Corner_NW : CornerType.Diagonal_NW);
            else if (tmpChecks[0] && tmpChecks[1] && !tmpChecks[2] && !tmpChecks[3]) cornerType = (tmpChecks[5] ? CornerType.Corner_NE : CornerType.Diagonal_NE);
            else if (tmpChecks[2] && tmpChecks[1] && !tmpChecks[0] && !tmpChecks[3]) cornerType = (tmpChecks[6] ? CornerType.Corner_SE : CornerType.Diagonal_SE);
            else if (tmpChecks[2] && tmpChecks[3] && !tmpChecks[0] && !tmpChecks[1]) cornerType = (tmpChecks[7] ? CornerType.Corner_SW : CornerType.Diagonal_SW);
            if (cornerType == CornerType.None) return false;
            for (int j = 0; j < 4; j++) { if (tmpChecks[j]) { color = (pos + Directions[j]).GetEdificeSafe(map).DrawColor; break; } }
            return true;
        }

        private static CachedMaterial GetMaterial(CornerType edgeType)
        {
            EnsureInitialized();
            switch (edgeType)
            {
                case CornerType.Corner_NW: return mat_Corner_NW;
                case CornerType.Corner_NE: return mat_Corner_NE;
                case CornerType.Corner_SW: return mat_Corner_SW;
                case CornerType.Corner_SE: return mat_Corner_SE;
                case CornerType.Diagonal_NW: return mat_Diagonal_NW;
                case CornerType.Diagonal_NE: return mat_Diagonal_NE;
                case CornerType.Diagonal_SW: return mat_Diagonal_SW;
                case CornerType.Diagonal_SE: return mat_Diagonal_SE;
                default: throw new System.ArgumentOutOfRangeException("edgeType", edgeType, null);
            }
        }

        private static IntVec3 GetOffset(CornerType cornerType)
        {
            switch (cornerType)
            {
                case CornerType.Corner_NE: case CornerType.Diagonal_NE: return new IntVec3(0, 0, 0);
                case CornerType.Corner_NW: case CornerType.Diagonal_NW: return new IntVec3(-1, 0, 0);
                case CornerType.Corner_SE: case CornerType.Diagonal_SE: return new IntVec3(0, 0, -1);
                case CornerType.Corner_SW: case CornerType.Diagonal_SW: return new IntVec3(-1, 0, -1);
                default: return IntVec3.Zero;
            }
        }

        private void AddQuad(Material mat, IntVec3 c, float scale, float altitude, Color color, bool addGravshipMask, bool addIndoorMask)
        {
            LayerSubMesh subMesh = GetSubMesh(mat);
            AddQuad(subMesh, c.ToVector3(), scale, altitude, color);
            if (addGravshipMask)
            {
                Texture2D srcTex = subMesh.material.mainTexture as Texture2D;
                Color color2 = subMesh.material.color;
                Material material = MaterialPool.MatFrom(srcTex, ShaderDatabase.GravshipMaskMasked, color2);
                AddQuad(GetSubMesh(material), c.ToVector3(), scale, altitude, color);
            }
            if (addIndoorMask)
            {
                Texture2D srcTex2 = subMesh.material.mainTexture as Texture2D;
                Color color3 = subMesh.material.color;
                Material material2 = MaterialPool.MatFrom(srcTex2, ShaderDatabase.IndoorMaskMasked, color3);
                AddQuad(GetSubMesh(material2), c.ToVector3(), scale, altitude, color);
            }
        }

        private static void AddQuad(LayerSubMesh sm, Vector3 c, float scale, float altitude, Color color)
        {
            int count = sm.verts.Count;
            for (int i = 0; i < 4; i++)
            {
                sm.verts.Add(new Vector3(c.x + UVs[i].x * scale, altitude, c.z + UVs[i].y * scale));
                sm.uvs.Add(UVs[i % 4]);
                sm.colors.Add(color);
            }
            sm.tris.Add(count);
            sm.tris.Add(count + 1);
            sm.tris.Add(count + 2);
            sm.tris.Add(count);
            sm.tris.Add(count + 2);
            sm.tris.Add(count + 3);
        }

        private static bool IsCornerIndoorMasked(IntVec3 c, CornerType cornerType, Map map)
        {
            switch (cornerType)
            {
                case CornerType.Corner_NE: case CornerType.Diagonal_NE: if (!c.Roofed(map)) return (c + IntVec3.East).Roofed(map); return true;
                case CornerType.Corner_NW: case CornerType.Diagonal_NW: if (!c.Roofed(map)) return (c + IntVec3.West).Roofed(map); return true;
                case CornerType.Corner_SE: case CornerType.Diagonal_SE: if (!c.Roofed(map)) return (c + IntVec3.East).Roofed(map); return true;
                case CornerType.Corner_SW: case CornerType.Diagonal_SW: if (!c.Roofed(map)) return (c + IntVec3.West).Roofed(map); return true;
                default: return false;
            }
        }
    }
}