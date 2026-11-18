using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Player;
using Vhaldren.Gameplay.Races;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Kit de Lucifer (escuela Luz caída): luz dorada y fuego infernal.
    /// 1 Lanza del Lucero (atraviesa y prende), 2 Salto de alas (vuela por encima de
    /// obstáculos; al caer, cadenas de azufre atan y queman), 3 Caída del Lucero (una
    /// estrella cae en un gran círculo avisado tras una lluvia de fuego). El poder R,
    /// Estallido de alas, usa <see cref="WingBurstVfx"/>. Todo el daño pasa por el grimorio
    /// (crítico, afinidad, esencia, registro) y por <see cref="CombatDamageReceiver"/>.
    /// </summary>
    public static class FallenLightKit
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
        public static readonly Color Hellfire = new Color(1f, 0.36f, 0.07f);
        public static readonly Color Crimson = new Color(0.9f, 0.16f, 0.08f);

        public const float LeapRootSeconds = 1.5f;
        public const float LeapMinDistance = 2.5f;
        public const float LeapForce = 0.5f;
        public const float StarEdgeDamage = 0.75f;
        public const float StarForce = 2.2f;
        public const float StarSeconds = 0.6f;
        public const float WingBurstRadius = 5f;
        public const float WingBurstDamage = 30f;
        public const float WingBurstForce = 3f;
        public const float WingBurstStagger = 0.6f;

        private static readonly List<CombatDamageReceiver> targets = new List<CombatDamageReceiver>(32);
        private static readonly RaycastHit[] groundHits = new RaycastHit[12];
        private static readonly Collider[] overlap = new Collider[16];

        private sealed class DescentState
        {
            public bool Cancelled;
        }

        /// <summary>Ejecuta un hechizo de la Luz caída (lo llama el grimorio).</summary>
        public static IEnumerator Execute(PlayerSpellbook book, SpellDefinition spell, float power, Vector3 aim)
        {
            switch (spell.Delivery)
            {
                case SpellDelivery.Leap: return Leap(book, spell, power, aim);
                case SpellDelivery.Zone: return StarFall(book, spell, power, aim);
                default: return Lance(book, spell, power, aim);
            }
        }

        // ── Cálculo puro (probado en EditMode) ─────────────────────────

        /// <summary>Punto del arco del salto: recta entre inicio y fin más una parábola de altura <paramref name="apex"/>.</summary>
        public static Vector3 LeapArc(Vector3 start, Vector3 end, float t, float apex)
        {
            float k = Mathf.Clamp01(t);
            return Vector3.Lerp(start, end, k) + Vector3.up * (4f * apex * k * (1f - k));
        }

        public static float LeapApex(float distance) => 1.4f + 0.16f * Mathf.Max(0f, distance);

        public static float LeapDuration(float distance) => 0.4f + 0.025f * Mathf.Max(0f, distance);

        /// <summary>Distancia del salto hacia el cursor (mínimo 2,5 m; cursor encima = alcance completo).</summary>
        public static float LeapDistance(Vector3 from, Vector3 aim, float range)
        {
            Vector3 flat = aim - from;
            flat.y = 0f;
            float length = flat.magnitude;
            if (length < 0.5f)
                return range;
            return Mathf.Clamp(length, Mathf.Min(LeapMinDistance, range), range);
        }

        /// <summary>Daño relativo de la estrella según la distancia al centro (100 % → 75 % en el borde).</summary>
        public static float StarFalloff(float distance, float radius) =>
            Mathf.Lerp(1f, StarEdgeDamage, Mathf.Clamp01(distance / Mathf.Max(0.01f, radius)));

        // ── 1. Lanza del Lucero ─────────────────────────────────────────

        private static IEnumerator Lance(PlayerSpellbook book, SpellDefinition spell, float power, Vector3 aim)
        {
            Vector3 direction = FlatDirection(book, aim);
            LuciferAvatar avatar = LuciferAvatar.For(book);
            if (avatar != null)
            {
                avatar.Flap(0.35f, 0.22f);
                avatar.Flare(0.6f);
            }
            SpellCastCharge hand = book.CastCharge();
            if (hand != null)
                hand.Begin(VfxElement.Fire, Gold, spell.Windup, direction);
            if (spell.Windup > 0f)
                yield return new WaitForSeconds(spell.Windup);
            if (!book.Alive())
            {
                if (hand != null)
                    hand.Cancel();
                yield break;
            }
            if (hand != null)
                hand.Release(direction);
            FallenLance.Launch(book, spell, power, book.CastOrigin() + direction * 0.6f, direction);
            GameFeel.Shake(0.04f, 0.08f);
        }

        /// <summary>Destello al atravesar a un enemigo.</summary>
        public static void PierceFlash(Vector3 point, Vector3 direction)
        {
            SpellImpactFlash.Spawn(point, Gold, 0.55f, 0.2f, 0f, VfxElement.Fire);
            VfxHost host = VfxHost.Get();
            if (host != null)
                host.Burst(VfxElement.Fire, point, direction, Hellfire, 12, 6.5f, 0.14f, 0.32f, 0.45f, 0.05f);
        }

        // ── 2. Salto de alas ───────────────────────────────────────────

        private static IEnumerator Leap(PlayerSpellbook book, SpellDefinition spell, float power, Vector3 aim)
        {
            Transform self = book.transform;
            CharacterController body = book.GetComponent<CharacterController>();
            PlayerMotor motor = book.GetComponent<PlayerMotor>();
            LuciferAvatar avatar = LuciferAvatar.For(book);
            LuciferFlight flight = LuciferFlight.For(book);

            Vector3 start = self.position;
            Vector3 direction = FlatDirection(book, aim);
            float distance = LeapDistance(start, aim, spell.Range);
            float feetOffset = body != null && body.enabled ? start.y - body.bounds.min.y : 1f;
            Vector3 landing = FindLanding(self, body, start, direction, distance, feetOffset);
            Vector3 flat = landing - start;
            flat.y = 0f;
            float flightDistance = flat.magnitude;
            float duration = LeapDuration(flightDistance);

            if (motor != null)
                motor.SetFacing(direction);
            flight.Begin();
            if (avatar != null)
            {
                avatar.Spread(spell.Windup + duration + 0.5f, 1f);
                avatar.Flap(1f, spell.Windup + duration + 0.15f);
                avatar.Flare(1.5f);
            }
            VfxHost host = VfxHost.Get();
            Vector3 feet = start + Vector3.down * feetOffset;
            if (host != null)
            {
                host.Smoke(feet + Vector3.up * 0.15f, 0.5f, new Color(0.5f, 0.46f, 0.4f, 0.55f), 14, 0.5f, 0.7f, Vector3.up * 0.6f);
                host.Ring(VfxElement.Fire, feet, Vector3.up, 0.4f, Hellfire, 24, 3.5f, 1.5f, 0.18f, 0.45f);
            }
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.Whoosh, start, 0.9f, 0.05f);

            if (spell.Windup > 0f)
                yield return new WaitForSeconds(spell.Windup);
            if (book == null)
                yield break;
            if (!book.Alive())
            {
                flight.Land();
                yield break;
            }

            flight.TakeOff(body);
            float apex = LeapApex(flightDistance);
            float elapsed = 0f;
            Vector3 previous = start;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                Vector3 point = LeapArc(start, landing, elapsed / duration, apex);
                if (self == null)
                    yield break;
                self.position = point;
                if (host != null)
                {
                    Vector3 velocity = (point - previous) / Mathf.Max(0.0001f, Time.deltaTime);
                    host.Trail(VfxElement.Fire, point + Vector3.up * 0.3f, -velocity * 0.15f, Hellfire, 3,
                        0.16f, 0.45f, 0.25f, 0.6f);
                    host.Glints(point + Vector3.up * 0.6f, 0.3f, new Color(1f, 0.88f, 0.55f, 1f), 1, 0.14f, 0.5f,
                        Vector3.up * 0.3f);
                }
                previous = point;
                yield return null;
                if (book == null)
                    yield break;
            }
            self.position = landing;
            flight.Land();
            Physics.SyncTransforms();

            Vector3 ground = VfxLibrary.FindGround(landing, out Vector3 found, out _) ? found : landing + Vector3.down * feetOffset;
            LandingImpact(book, spell, power, ground);
        }

        private static void LandingImpact(PlayerSpellbook book, SpellDefinition spell, float power, Vector3 ground)
        {
            float radius = spell.Radius;
            SpellImpactFlash.Spawn(ground + Vector3.up * 0.3f, Gold, 1.5f, 0.45f, radius, VfxElement.Fire);
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                host.Ring(VfxElement.Fire, ground, Vector3.up, 0.4f, Hellfire, 50, radius * 2.2f, 1.8f, 0.22f, 0.6f);
                host.Smoke(ground + Vector3.up * 0.3f, 0.6f, new Color(0.45f, 0.4f, 0.36f, 0.55f), 14, 0.8f, 0.9f,
                    Vector3.up * 0.5f);
                host.Glints(ground + Vector3.up * 0.8f, radius * 0.5f, new Color(1f, 0.9f, 0.55f, 1f), 16, 0.18f, 0.7f,
                    Vector3.up * 1.2f);
            }
            GameFeel.Shake(0.2f, 0.25f);
            if (GameFeel.Enabled)
                GameAudio.Play(GameSound.SpellImpact, ground, 0.9f, 0.05f);

            int count = book.CollectTargets(ground, radius, targets);
            for (int i = 0; i < count; i++)
            {
                CombatDamageReceiver receiver = targets[i];
                if (receiver == null)
                    continue;
                if (book.ApplySpellHit(receiver, spell, power, ground, 1f, LeapForce) <= 0f)
                    continue;
                StatusEffects status = StatusEffects.For(receiver);
                if (status != null)
                    status.Bind(LeapRootSeconds);
                Chains(receiver, ground);
            }
            targets.Clear();
        }

        /// <summary>Cadenas de azufre: dos eslabones de fuego del suelo al pecho del atado.</summary>
        private static void Chains(CombatDamageReceiver receiver, Vector3 ground)
        {
            Vector3 chest = receiver.transform.position + Vector3.up * 0.7f;
            Vector3 feet = new Vector3(chest.x, ground.y + 0.05f, chest.z);
            for (int k = 0; k < 2; k++)
            {
                float angle = (k * 180f + Random.Range(-40f, 40f)) * Mathf.Deg2Rad;
                Vector3 anchor = feet + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.1f;
                LightningArc.Strike(anchor, chest, Hellfire, 0.07f, LeapRootSeconds, 0.05f);
                LightningArc.Strike(anchor, chest, Gold, 0.025f, LeapRootSeconds * 0.9f, 0.03f);
            }
        }

        private static Vector3 FindLanding(Transform self, CharacterController body, Vector3 start, Vector3 direction,
            float distance, float feetOffset)
        {
            float radius = body != null ? body.radius : 0.4f;
            float height = body != null ? body.height : 2f;
            float startFeet = start.y - feetOffset;
            for (float d = distance; d >= 0.99f; d -= 0.5f)
            {
                Vector3 point = start + direction * d;
                if (!GroundBelow(self, point, out Vector3 ground))
                    continue;
                if (ground.y - startFeet > 6f)
                    continue;   // un acantilado demasiado alto
                if (!SpaceFree(self, ground, radius, height))
                    continue;
                return ground + Vector3.up * (feetOffset + 0.05f);
            }
            return start;
        }

        private static bool GroundBelow(Transform self, Vector3 point, out Vector3 ground)
        {
            ground = point;
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 8f, Vector3.down, groundHits, 20f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider hit = groundHits[i].collider;
                if (hit == null || hit.transform.IsChildOf(self) || hit.GetComponentInParent<CombatHealth>() != null)
                    continue;
                if (groundHits[i].distance < best)
                {
                    best = groundHits[i].distance;
                    ground = groundHits[i].point;
                }
            }
            return best < float.PositiveInfinity;
        }

        private static bool SpaceFree(Transform self, Vector3 ground, float radius, float height)
        {
            Vector3 bottom = ground + Vector3.up * (radius + 0.12f);
            Vector3 top = ground + Vector3.up * Mathf.Max(radius + 0.13f, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.9f, overlap,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = overlap[i];
                if (hit == null || hit.transform.IsChildOf(self))
                    continue;
                // Caer junto a enemigos está permitido (es la idea); muros y rocas no.
                if (hit.GetComponentInParent<CombatHealth>() != null)
                    continue;
                return false;
            }
            return true;
        }

        // ── 3. Caída del Lucero ────────────────────────────────────────

        private static IEnumerator StarFall(PlayerSpellbook book, SpellDefinition spell, float power, Vector3 aim)
        {
            Vector3 origin = book.transform.position;
            Vector3 toAim = aim - origin;
            toAim.y = 0f;
            Vector3 point = origin + Vector3.ClampMagnitude(toAim, spell.Range);
            point.y = origin.y;
            Vector3 ground = book.ZoneGround(point);
            float radius = spell.Radius;

            GroundTelegraph warning = book.Telegraph(null, ground, radius, Color.Lerp(Gold, Crimson, 0.35f), spell.Windup);
            LuciferAvatar avatar = LuciferAvatar.For(book);
            if (avatar != null)
            {
                avatar.Spread(spell.Windup + 0.6f, 1f);
                avatar.Flap(0.45f, spell.Windup);
                avatar.Flare(2f);
            }
            SpellCastCharge hand = book.CastCharge();
            if (hand != null)
                hand.Begin(VfxElement.Fire, Gold, spell.Windup, Vector3.up);
            DescentState state = new DescentState();
            VfxHost host = VfxHost.Get();
            if (host != null)
                host.StartCoroutine(StarDescent(ground, radius, spell.Windup, state));

            if (spell.Windup > 0f)
                yield return new WaitForSeconds(spell.Windup);
            if (!book.Alive())
            {
                state.Cancelled = true;
                if (warning != null)
                    warning.Cancel();
                if (hand != null)
                    hand.Cancel();
                yield break;
            }
            if (hand != null)
                hand.Release(Vector3.up);

            StarImpact(ground, radius);
            GameFeel.HitStop(0.12f);
            GameFeel.Shake(0.4f, 0.45f);
            int count = book.CollectTargets(ground, radius, targets);
            for (int i = 0; i < count; i++)
            {
                CombatDamageReceiver receiver = targets[i];
                if (receiver == null)
                    continue;
                Vector3 delta = receiver.transform.position - ground;
                delta.y = 0f;
                book.ApplySpellHit(receiver, spell, power, ground, StarFalloff(delta.magnitude, radius), StarForce);
            }
            targets.Clear();
            if (host != null)
                host.StartCoroutine(BurningGround(ground, radius, 2.6f));
        }

        /// <summary>Lluvia de fuego infernal en el círculo y, al final, la estrella que cae.</summary>
        private static IEnumerator StarDescent(Vector3 ground, float radius, float windup, DescentState state)
        {
            VfxHost host = VfxHost.Get();
            if (host == null)
                yield break;
            float starStart = Mathf.Max(0f, windup - StarSeconds);
            Vector3 sky = ground + new Vector3(-2.5f, 24f, -1.5f);
            VfxPool<VfxOrb> pool = host.Pool(VfxOrb.Factory);
            VfxOrb star = null;
            float elapsed = 0f, nextEmber = 0f, nextBeam = 0f;
            while (elapsed < windup && !state.Cancelled)
            {
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, windup));
                if (elapsed >= nextEmber)
                {
                    nextEmber = elapsed + Mathf.Lerp(0.07f, 0.03f, progress);
                    Vector2 inside = Random.insideUnitCircle * radius;
                    Vector3 from = ground + new Vector3(inside.x, 7f + Random.value * 3f, inside.y);
                    host.Trail(VfxElement.Fire, from, Vector3.down * 16f + new Vector3(1.5f, 0f, 0.8f), Hellfire, 2,
                        0.24f, 0.55f, 0.1f, 0.4f);
                }
                if (elapsed >= nextBeam)
                {
                    nextBeam = elapsed + 0.3f;
                    LightningArc.Strike(ground + Vector3.up * 18f, ground, new Color(Gold.r, Gold.g, Gold.b, 0.35f + 0.5f * progress),
                        0.06f + 0.22f * progress, 0.25f, 0f);
                }
                if (star == null && elapsed >= starStart)
                {
                    star = pool.Take();
                    star.gameObject.SetActive(true);
                    star.Configure(VfxElement.Light, Gold, 2.2f, 0.6f);
                    star.Teleport(sky, (ground - sky) / Mathf.Max(0.05f, windup - starStart));
                }
                if (star != null)
                {
                    float k = Mathf.Clamp01((elapsed - starStart) / Mathf.Max(0.05f, windup - starStart));
                    Vector3 at = Vector3.Lerp(sky, ground + Vector3.up * 0.5f, k * k);
                    star.Step(at, Time.deltaTime);
                    host.Trail(VfxElement.Fire, at, (sky - ground).normalized * 3f, Hellfire, 4, 0.35f, 0.5f, 0.3f, 0.8f);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (star != null)
            {
                star.Extinguish();
                yield return new WaitForSeconds(star.LingerSeconds);
                if (star != null)
                    pool.Release(star);
            }
        }

        private static void StarImpact(Vector3 ground, float radius)
        {
            SpellImpactFlash.Spawn(ground + Vector3.up * 0.4f, Gold, 2.4f, 0.6f, radius, VfxElement.Fire);
            SpellImpactFlash.Spawn(ground + Vector3.up * 1.2f, new Color(1f, 0.97f, 0.85f), 1.4f, 0.3f, 0f, VfxElement.Light);
            LightningArc.Strike(ground + Vector3.up * 20f, ground, Hellfire, radius * 0.4f, 0.5f, 0f);
            LightningArc.Strike(ground + Vector3.up * 20f, ground, Gold, radius * 0.16f, 0.45f, 0f);
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                host.Ring(VfxElement.Fire, ground, Vector3.up, radius * 0.25f, Hellfire, 90, radius * 2.4f, 2.4f, 0.3f, 0.8f);
                host.Burst(VfxElement.Fire, ground + Vector3.up * 0.3f, Vector3.up, Gold, 60, 11f, 0.26f, 1f, 0.5f, 0.4f);
                host.Glints(ground + Vector3.up * 1.2f, radius * 0.6f, new Color(1f, 0.9f, 0.55f, 1f), 30, 0.22f, 1.1f,
                    Vector3.up * 1.4f);
                host.Smoke(ground + Vector3.up * 0.8f, radius * 0.3f, new Color(0.08f, 0.05f, 0.04f, 0.7f), 20, 1.8f, 2f,
                    Vector3.up * 1.8f);
            }
            if (GameFeel.Enabled)
            {
                GameAudio.Play(GameSound.SpellImpact, ground, 1f, 0.02f);
                GameAudio.Play(GameSound.EnemyDown, ground, 0.7f, 0.02f);
            }
        }

        /// <summary>El suelo sigue ardiendo unos segundos tras la estrella (solo visual).</summary>
        private static IEnumerator BurningGround(Vector3 ground, float radius, float seconds)
        {
            VfxHost host = VfxHost.Get();
            float elapsed = 0f;
            while (host != null && elapsed < seconds)
            {
                Vector2 inside = Random.insideUnitCircle * radius * 0.85f;
                host.Trail(VfxElement.Fire, ground + new Vector3(inside.x, 0.1f, inside.y), Vector3.up * 1.2f, Hellfire, 2,
                    0.2f, 0.7f, 0.2f, 0.6f);
                elapsed += 0.12f;
                yield return new WaitForSeconds(0.12f);
            }
        }

        // ── R. Estallido de alas (efecto; el daño lo aplica PlayerRace) ─

        /// <summary>Las alas se despliegan de golpe: onda dorada, anillo de fuego y plumas.</summary>
        public static void WingBurstVfx(Transform caster)
        {
            if (caster == null)
                return;
            LuciferAvatar avatar = LuciferAvatar.For(caster);
            if (avatar != null)
            {
                avatar.Spread(0.9f, 1f);
                avatar.Flap(1f, 0.5f);
                avatar.Flare(2.5f);
            }
            CharacterController body = caster.GetComponent<CharacterController>();
            Vector3 feet = body != null && body.enabled
                ? new Vector3(caster.position.x, body.bounds.min.y + 0.05f, caster.position.z)
                : caster.position;
            Vector3 back = avatar != null ? avatar.BackPoint : caster.position + Vector3.up;
            SpellImpactFlash.Spawn(feet + Vector3.up * 0.3f, Gold, 1.8f, 0.5f, WingBurstRadius, VfxElement.Fire);
            VfxHost host = VfxHost.Get();
            if (host != null)
            {
                host.Ring(VfxElement.Fire, feet, Vector3.up, 0.5f, Hellfire, 80, WingBurstRadius * 2.6f, 1.2f, 0.26f, 0.6f);
                host.Glints(back, 1.2f, new Color(1f, 0.88f, 0.5f, 1f), 30, 0.18f, 0.8f, Vector3.up * 0.4f);
                host.Smoke(back, 0.8f, new Color(0.07f, 0.05f, 0.06f, 0.6f), 16, 0.3f, 0.9f, Vector3.up * 0.3f);
            }
            GameFeel.Shake(0.22f, 0.3f);
        }

        // ── Utilidades ─────────────────────────────────────────────────

        private static Vector3 FlatDirection(PlayerSpellbook book, Vector3 aim)
        {
            Vector3 toAim = aim - book.transform.position;
            toAim.y = 0f;
            return toAim.sqrMagnitude > 0.01f ? toAim.normalized : book.Facing();
        }
    }
}
