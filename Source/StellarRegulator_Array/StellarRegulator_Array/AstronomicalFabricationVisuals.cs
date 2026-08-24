using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace SRA
{
    [StaticConstructorOnStartup]
    public static class AstronomicalFabricationVisuals
    {
        private static readonly Color HologramCyan = new Color(0.18f, 0.92f, 1f, 1f);
        private static readonly Color HologramMint = new Color(0.5f, 1f, 0.82f, 1f);
        private static readonly Texture2D StarTexture = CreateRadialTexture(64, new Color(1f, 0.83f, 0.36f), false);
        private static readonly Texture2D NodeTexture = CreateRadialTexture(24, HologramCyan, false);
        private static readonly Texture2D RingTexture = CreateRadialTexture(64, HologramCyan, true);
        private static readonly Material ChargeRingMaterial = CreateGlowMaterial(RingTexture, HologramCyan);
        private static readonly Material ChargeCoreMaterial = CreateGlowMaterial(NodeTexture, new Color(0.7f, 1f, 1f));
        private const int MaximumVisibleDysonNodes = 512;

        public static void DrawTransfer(Vector3 drawPos, float progress, float beamPhaseStart)
        {
            float time = Time.realtimeSinceStartup;
            float chargeProgress = Mathf.Clamp01(progress / Mathf.Max(0.05f, beamPhaseStart));
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 10f);
            float ringSize = Mathf.Lerp(1.8f, 6.8f, chargeProgress) + pulse * 0.35f;
            Vector3 groundPos = drawPos;
            groundPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();

            Matrix4x4 outerRing = Matrix4x4.TRS(groundPos, Quaternion.identity, new Vector3(ringSize, 1f, ringSize));
            Graphics.DrawMesh(MeshPool.plane10, outerRing, FadedMaterialPool.FadedVersionOf(ChargeRingMaterial, 0.45f + chargeProgress * 0.45f), 0);

            float innerSize = Mathf.Lerp(0.8f, 2.2f, chargeProgress) + pulse * 0.3f;
            Matrix4x4 innerGlow = Matrix4x4.TRS(groundPos + new Vector3(0f, 0.01f, 0f), Quaternion.identity, new Vector3(innerSize, 1f, innerSize));
            Graphics.DrawMesh(MeshPool.plane10, innerGlow, FadedMaterialPool.FadedVersionOf(ChargeCoreMaterial, 0.45f + chargeProgress * 0.5f), 0);
        }

        public static void DrawHolographicSystem(Rect rect, int industrialUnits, long orbitalMassEnergy, long orbitalMassEnergyCapacity)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.015f, 0.045f, 0.065f, 0.98f));
            Widgets.DrawBox(rect, 1);

            Rect inner = rect.ContractedBy(12f);
            DrawGrid(inner);
            Vector2 center = new Vector2(inner.center.x - inner.width * 0.08f, inner.center.y);
            float minAxis = Mathf.Min(inner.width, inner.height);
            float time = Time.realtimeSinceStartup;

            float planetOrbitX = minAxis * 0.25f;
            float planetOrbitY = minAxis * 0.12f;
            float dysonOrbitX = minAxis * 0.44f;
            float dysonOrbitY = minAxis * 0.21f;
            DrawEllipse(center, planetOrbitX, planetOrbitY, new Color(0.18f, 0.72f, 0.82f, 0.48f), 1f, 0f);
            DrawEllipse(center, dysonOrbitX, dysonOrbitY, new Color(0.35f, 1f, 0.86f, 0.72f), 1.4f, 5f);

            float starSize = minAxis * (0.095f + Mathf.Sin(time * 2f) * 0.006f);
            GUI.DrawTexture(new Rect(center.x - starSize / 2f, center.y - starSize / 2f, starSize, starSize), StarTexture);
            Widgets.DrawLine(new Vector2(center.x - starSize * 0.75f, center.y), new Vector2(center.x + starSize * 0.75f, center.y), new Color(1f, 0.84f, 0.42f, 0.65f), 1f);

            DrawPlanet(center, planetOrbitX, planetOrbitY, time * 13f, minAxis * 0.028f, new Color(0.36f, 0.84f, 1f));
            DrawDysonCloud(center, dysonOrbitX, dysonOrbitY, industrialUnits, time);

            Rect tag = new Rect(inner.x + 8f, inner.yMax - 48f, inner.width - 16f, 38f);
            Widgets.DrawBoxSolid(tag, new Color(0.02f, 0.14f, 0.17f, 0.72f));
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = HologramMint;
            Widgets.Label(tag.ContractedBy(10f, 0f), "SRA_OrbitalHologramReadout".Translate(
                industrialUnits,
                OrbitalFabricationUtility.FormatMassEnergy(orbitalMassEnergy),
                OrbitalFabricationUtility.FormatMassEnergy(orbitalMassEnergyCapacity)));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawGrid(Rect rect)
        {
            Color grid = new Color(0.1f, 0.48f, 0.56f, 0.14f);
            const float spacing = 32f;
            for (float x = rect.x; x <= rect.xMax; x += spacing)
            {
                Widgets.DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax), grid, 1f);
            }

            for (float y = rect.y; y <= rect.yMax; y += spacing)
            {
                Widgets.DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y), grid, 1f);
            }
        }

        private static void DrawEllipse(Vector2 center, float radiusX, float radiusY, Color color, float width, float rotationDegrees)
        {
            const int segments = 72;
            Vector2 previous = EllipsePoint(center, radiusX, radiusY, rotationDegrees, 0f);
            for (int i = 1; i <= segments; i++)
            {
                Vector2 current = EllipsePoint(center, radiusX, radiusY, rotationDegrees, i * 360f / segments);
                Widgets.DrawLine(previous, current, color, width);
                previous = current;
            }
        }

        private static void DrawPlanet(Vector2 center, float radiusX, float radiusY, float angle, float size, Color color)
        {
            Vector2 point = EllipsePoint(center, radiusX, radiusY, 0f, angle);
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - size / 2f, point.y - size / 2f, size, size), NodeTexture);
            GUI.color = previous;
        }

        private static void DrawDysonCloud(Vector2 center, float radiusX, float radiusY, int units, float time)
        {
            if (units <= 0)
            {
                return;
            }

            int visibleNodes = Math.Min(units, MaximumVisibleDysonNodes);
            float density = Mathf.Clamp01(units / (float)MaximumVisibleDysonNodes);
            for (int i = 0; i < visibleNodes; i++)
            {
                // Each visible marker represents exactly one industrial unit. For very large clouds the readout retains the exact count.
                float representedIndex = units <= MaximumVisibleDysonNodes
                    ? i
                    : i * (units / (float)MaximumVisibleDysonNodes);
                float angle = representedIndex * 137.50776f + time * 3.2f;
                Vector2 point = EllipsePoint(center, radiusX, radiusY, 5f, angle);

                // Each node keeps a stable orbital lane, then drifts within it slowly instead of jumping to a new random position every frame.
                float heightSeed = StableHash01(i, 37);
                float motionSeed = StableHash01(i, 83);
                float phaseSeed = StableHash01(i, 149);
                float verticalOffset = (heightSeed - 0.5f) * radiusY * 0.26f;
                float bobAmplitude = radiusY * Mathf.Lerp(0.025f, 0.06f, motionSeed);
                float bob = Mathf.Sin(time * Mathf.Lerp(0.45f, 0.8f, motionSeed) + phaseSeed * Mathf.PI * 2f) * bobAmplitude;
                point.y += verticalOffset + bob;

                float size = units <= 96 ? 5f : 3.8f;
                Color previous = GUI.color;
                GUI.color = i % 7 == 0 ? HologramMint : new Color(HologramCyan.r, HologramCyan.g, HologramCyan.b, 0.55f + density * 0.4f);
                GUI.DrawTexture(new Rect(point.x - size / 2f, point.y - size / 2f, size, size), NodeTexture);
                GUI.color = previous;
            }
        }

        private static Vector2 EllipsePoint(Vector2 center, float radiusX, float radiusY, float rotationDegrees, float angleDegrees)
        {
            float angle = angleDegrees * Mathf.Deg2Rad;
            float rotation = rotationDegrees * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * radiusX;
            float y = Mathf.Sin(angle) * radiusY;
            return new Vector2(
                center.x + x * Mathf.Cos(rotation) - y * Mathf.Sin(rotation),
                center.y + x * Mathf.Sin(rotation) + y * Mathf.Cos(rotation));
        }

        private static float StableHash01(int index, int salt)
        {
            unchecked
            {
                uint value = (uint)(index * 73856093) ^ (uint)(salt * 19349663);
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return (value & 0x00FFFFFF) / 16777215f;
            }
        }

        private static Texture2D CreateRadialTexture(int size, Color color, bool ring)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = ring ? "SRA_OrbitalRing" : "SRA_OrbitalGlow";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = ring
                        ? Mathf.Clamp01(1f - Mathf.Abs(distance - 0.72f) * 9f) * Mathf.Clamp01(1f - distance)
                        : Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Material CreateGlowMaterial(Texture2D texture, Color color)
        {
            Material material = new Material(ShaderDatabase.MoteGlow)
            {
                mainTexture = texture,
                color = color
            };
            return material;
        }
    }
}
