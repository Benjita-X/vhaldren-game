using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.World;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Referencias compartidas de la exploracion: el jugador local y la sesion. Las fija
    /// <see cref="ExplorationBootstrap.Install"/>; si faltan se buscan como mucho cada 2 s
    /// (nunca una busqueda global por fotograma).
    /// </summary>
    public static class ExplorationContext
    {
        private static Transform player;
        private static PrototypeGameSession session;
        private static float nextSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            player = null;
            session = null;
            nextSearch = 0f;
        }

        public static Transform Player
        {
            get
            {
                if (player == null && Time.unscaledTime >= nextSearch)
                {
                    nextSearch = Time.unscaledTime + 2f;
                    Progression.PlayerEquipment gear = Progression.PlayerEquipment.Local;
                    if (gear != null)
                        player = gear.transform;
                    else
                    {
                        GameObject found = GameObject.Find("PlayerRoot");
                        if (found != null)
                            player = found.transform;
                    }
                }
                return player;
            }
            set => player = value;
        }

        public static PrototypeGameSession Session
        {
            get => session;
            set => session = value;
        }
    }

    /// <summary>
    /// Altura del suelo bajo un punto: primero los Terrain activos (lista cacheada), luego un
    /// rayo hacia abajo. Para el vuelo solo cuentan el terreno y los colisionadores muy altos
    /// (montañas o acantilados de malla): arboles y casas se sobrevuelan.
    /// </summary>
    public static class GroundProbe
    {
        public const float TallObstacleHeight = 20f;

        private static readonly List<Terrain> terrains = new List<Terrain>(8);
        private static readonly RaycastHit[] hits = new RaycastHit[16];
        private static float nextRefresh = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            terrains.Clear();
            nextRefresh = -1f;
        }

        private static void Refresh()
        {
            if (Time.unscaledTime < nextRefresh && terrains.Count > 0)
                return;
            nextRefresh = Time.unscaledTime + 3f;
            terrains.Clear();
            Terrain.GetActiveTerrains(terrains);
        }

        /// <summary>Altura del Terrain que contiene el punto en XZ (la mayor si se solapan).</summary>
        public static bool TryTerrainHeight(Vector3 at, out float height)
        {
            Refresh();
            height = float.NegativeInfinity;
            bool found = false;
            for (int i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                    continue;
                Vector3 origin = terrain.GetPosition();
                Vector3 size = terrain.terrainData.size;
                if (at.x < origin.x || at.z < origin.z || at.x > origin.x + size.x || at.z > origin.z + size.z)
                    continue;
                float h = terrain.SampleHeight(at) + origin.y;
                if (h > height)
                    height = h;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// Suelo para volar: terreno y obstaculos altos (montañas de malla). Ignora la
        /// jerarquia de <paramref name="ignore"/> (el jugador y su montura).
        /// </summary>
        public static bool TryFlightFloor(Vector3 at, Transform ignore, out float height)
        {
            bool found = TryTerrainHeight(at, out height);
            float top = (found ? height : at.y) + 400f;
            int count = Physics.RaycastNonAlloc(new Vector3(at.x, top, at.z), Vector3.down, hits, 900f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || (ignore != null && c.transform.IsChildOf(ignore)))
                    continue;
                // Terreno, montañas de malla (altas) o suelos de malla muy extensos.
                Vector3 size = c.bounds.size;
                bool solid = c is TerrainCollider || size.y >= TallObstacleHeight || size.x >= 40f || size.z >= 40f;
                if (!solid || hits[i].point.y <= height)
                    continue;
                height = hits[i].point.y;
                found = true;
            }
            return found;
        }

        /// <summary>Primer suelo bajo el punto (cualquier colisionador), empezando un poco por encima.</summary>
        public static bool TryGroundBelow(Vector3 at, Transform ignore, float startAbove, out float height)
        {
            height = float.NegativeInfinity;
            int count = Physics.RaycastNonAlloc(at + Vector3.up * Mathf.Max(0f, startAbove), Vector3.down, hits,
                startAbove + 600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || (ignore != null && c.transform.IsChildOf(ignore)))
                    continue;
                if (hits[i].distance < nearest)
                {
                    nearest = hits[i].distance;
                    height = hits[i].point.y;
                }
            }
            if (!float.IsPositiveInfinity(nearest))
                return true;
            return TryTerrainHeight(at, out height);
        }
    }

    /// <summary>
    /// Aguante para viajar (correr a pie y galopar), aparte de la resistencia de combate.
    /// Logica pura: se gasta con <see cref="TryConsume"/> y se recupera tras una pausa;
    /// al agotarse hay que recuperar <see cref="RecoverFraction"/> antes de volver a usarlo.
    /// </summary>
    public sealed class StaminaPool
    {
        public float Max = 100f;
        public float RegenPerSecond = 24f;
        public float RegenDelay = 0.8f;
        public float RecoverFraction = 0.3f;

        private float sinceUse = 999f;

        public float Current { get; private set; } = 100f;
        public bool Exhausted { get; private set; }
        public float Fraction => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;
        public bool CanUse => !Exhausted && Current > 0f;

        /// <summary>Gasta <paramref name="amount"/>. Devuelve false si esta agotado (no gasta).</summary>
        public bool TryConsume(float amount)
        {
            if (!CanUse || float.IsNaN(amount) || amount < 0f)
                return false;
            Current = Mathf.Max(0f, Current - amount);
            sinceUse = 0f;
            if (Current <= 0f)
                Exhausted = true;
            return true;
        }

        /// <summary>Recupera aguante si no se ha gastado en los ultimos <see cref="RegenDelay"/> s.</summary>
        public void Regenerate(float deltaTime)
        {
            if (float.IsNaN(deltaTime) || deltaTime <= 0f)
                return;
            sinceUse += deltaTime;
            if (sinceUse >= RegenDelay)
                Current = Mathf.Min(Max, Current + RegenPerSecond * deltaTime);
            if (Exhausted && Current >= Max * RecoverFraction)
                Exhausted = false;
        }

        public void Refill()
        {
            Current = Max;
            Exhausted = false;
        }
    }
}
