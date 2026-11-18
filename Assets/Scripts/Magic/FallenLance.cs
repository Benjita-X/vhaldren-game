using System;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Lanza del Lucero: proyectil de luz dorada envuelto en fuego infernal que ATRAVIESA
    /// hasta <see cref="SpellDefinition.Pierce"/> enemigos (cada uno recibe algo menos) y se
    /// detiene contra paredes o el terreno. Avanza con SphereCast cada fotograma (no se salta
    /// objetivos a pocos FPS) y se reutiliza desde la reserva de efectos.
    /// </summary>
    public sealed class FallenLance : MonoBehaviour
    {
        public const float Speed = 30f;
        public const float HitRadiusFactor = 0.6f;
        /// <summary>Daño relativo de cada enemigo atravesado: 100 %, 80 %, 65 %, 50 %.</summary>
        public static float PierceScale(int index) => index <= 0 ? 1f : Mathf.Max(0.5f, 1f - 0.2f * index + 0.05f * (index - 1));

        private static readonly Func<Transform, FallenLance> Factory = Create;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly HashSet<CombatDamageReceiver> struck = new HashSet<CombatDamageReceiver>();
        private PlayerSpellbook caster;
        private SpellDefinition spell;
        private float power;
        private Vector3 direction;
        private float travelled;
        private int pierced;
        private bool done;
        private float recycleAt = float.PositiveInfinity;
        private float nextSpark;
        private VfxOrb orb;
        private VfxPool<FallenLance> pool;

        public bool IsDone => done;
        public int Pierced => pierced;
        public Vector3 LastImpactPoint { get; private set; }

        public static FallenLance Launch(PlayerSpellbook caster, SpellDefinition spell, float power,
            Vector3 origin, Vector3 direction)
        {
            VfxHost host = VfxHost.Get();
            VfxPool<FallenLance> reserve = host != null ? host.Pool(Factory) : null;
            FallenLance lance = reserve != null ? reserve.Take() : Create(null);
            lance.pool = reserve;
            lance.Begin(caster, spell, power, origin, direction);
            return lance;
        }

        private static FallenLance Create(Transform parent)
        {
            GameObject root = new GameObject("Lanza del Lucero (proyectil)");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            FallenLance lance = root.AddComponent<FallenLance>();
            lance.orb = VfxOrb.Create(root.transform);
            return lance;
        }

        private void Begin(PlayerSpellbook owner, SpellDefinition definition, float spellPower, Vector3 origin,
            Vector3 heading)
        {
            caster = owner;
            spell = definition;
            power = spellPower;
            heading.y = 0f;
            direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.forward;
            travelled = 0f;
            pierced = 0;
            done = false;
            struck.Clear();
            recycleAt = float.PositiveInfinity;
            transform.position = origin;
            LastImpactPoint = origin;
            gameObject.SetActive(true);
            // Núcleo de luz dorada con estela larga; las brasas del fuego se sueltan aparte.
            orb.Configure(VfxElement.Light, FallenLightKit.Gold, (definition != null ? definition.Radius : 0.4f) * 1.7f, 0.28f);
            orb.Teleport(origin, direction * Speed);
        }

        private void Update()
        {
            if (done)
            {
                if (Time.time >= recycleAt)
                    Recycle();
                return;
            }
            Step(Time.deltaTime);
        }

        /// <summary>Avanza la lanza. Separado de Update para poder probarlo.</summary>
        public void Step(float deltaTime)
        {
            if (done || spell == null)
                return;
            if (caster == null)
            {
                Finish(transform.position, false);
                return;
            }
            float step = Mathf.Min(Speed * Mathf.Max(0f, deltaTime), spell.Range - travelled);
            if (step <= 0f)
            {
                Finish(transform.position, false);
                return;
            }

            Vector3 start = transform.position;
            float radius = spell.Radius * HitRadiusFactor;
            int count = Physics.SphereCastNonAlloc(start, radius, direction, hits, step,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit[] list = hits;
            if (count >= hits.Length)
            {
                list = Physics.SphereCastAll(start, radius, direction, step,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = list.Length;
            }
            SortByDistance(list, count);

            for (int i = 0; i < count; i++)
            {
                Collider hit = list[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(caster.transform))
                    continue;
                Vector3 impact = start + direction * list[i].distance;
                CombatDamageReceiver receiver = hit.GetComponentInParent<CombatDamageReceiver>();
                if (receiver != null)
                {
                    if (!struck.Add(receiver))
                        continue;
                    CombatHealth targetHealth = receiver.GetComponent<CombatHealth>();
                    if (targetHealth == null || targetHealth.IsDefeated)
                        continue;
                    caster.ApplySpellHit(receiver, spell, power, start, PierceScale(pierced), 0.8f);
                    pierced++;
                    FallenLightKit.PierceFlash(receiver.transform.position + Vector3.up * 0.9f, direction);
                    if (pierced > Mathf.Max(0, spell.Pierce - 1))
                    {
                        Finish(impact, true);
                        return;
                    }
                    continue;
                }
                // Pared, roca o terreno: la lanza se clava y estalla.
                Finish(impact, true);
                return;
            }

            transform.position = start + direction * step;
            travelled += step;
            orb.Step(transform.position, deltaTime);
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + 0.03f;
                VfxHost host = VfxHost.Get();
                if (host != null)
                    host.Trail(VfxElement.Fire, transform.position, -direction * 2f, FallenLightKit.Hellfire, 2,
                        0.16f, 0.35f, 0.12f, 1.2f);
            }
            if (travelled >= spell.Range - 0.001f)
                Finish(transform.position, false);
        }

        private static void SortByDistance(RaycastHit[] list, int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit key = list[i];
                int j = i - 1;
                while (j >= 0 && list[j].distance > key.distance)
                {
                    list[j + 1] = list[j];
                    j--;
                }
                list[j + 1] = key;
            }
        }

        private void Finish(Vector3 point, bool impact)
        {
            if (done)
                return;
            done = true;
            LastImpactPoint = point;
            transform.position = point;
            orb.Step(point, Time.deltaTime);
            orb.Extinguish();
            SpellImpactFlash.Spawn(point, FallenLightKit.Gold, impact ? 0.75f : 0.45f, 0.24f, 0f, VfxElement.Fire);
            recycleAt = Time.time + orb.LingerSeconds;
            caster = null;
        }

        private void Recycle()
        {
            recycleAt = float.PositiveInfinity;
            spell = null;
            struck.Clear();
            if (pool != null)
                pool.Release(this);
            else
                Destroy(gameObject);
        }
    }
}
