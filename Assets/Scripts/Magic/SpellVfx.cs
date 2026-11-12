using System.Collections;
using UnityEngine;
using Vhaldren.Gameplay.Combat;

namespace Vhaldren.Gameplay.Magic
{
    /// <summary>
    /// Firma visual de cada escuela: elemento de sus particulas, estallido de sus areas y
    /// el remate de los definitivos (meteoro que cae, tormenta que descarga, pilar de luz,
    /// sangre que vuelve al mago). Tecnicas generales del genero, con formas propias.
    /// Solo visual: los tiempos y el daño siguen siendo los del grimorio.
    /// </summary>
    public static class SpellVfx
    {
        /// <summary>Segundos antes del estallido en que empieza el remate de un area.</summary>
        public const float DescentSeconds = 0.55f;

        public static VfxElement ElementOf(SpellSchool school)
        {
            switch (school)
            {
                case SpellSchool.Pyromancy: return VfxElement.Fire;
                case SpellSchool.Cryomancy: return VfxElement.Frost;
                case SpellSchool.Fulmancy: return VfxElement.Storm;
                case SpellSchool.Umbramancy: return VfxElement.Shadow;
                case SpellSchool.Hemomancy: return VfxElement.Blood;
                case SpellSchool.Lumancy: return VfxElement.Light;
                default: return VfxElement.Neutral;
            }
        }

        public static VfxPalette PaletteOf(SpellSchool school) =>
            VfxLibrary.Palette(ElementOf(school), SpellCatalog.SchoolColor(school));

        /// <summary>Impacto de un hechizo sobre un objetivo concreto.</summary>
        public static void Hit(SpellSchool school, Vector3 point, bool critical)
        {
            SpellImpactFlash.Spawn(point, SpellCatalog.SchoolColor(school), critical ? 0.8f : 0.5f, 0.22f, 0f,
                ElementOf(school));
        }

        /// <summary>
        /// Estallido de un area (Nova o Zona) en <paramref name="ground"/>: destello, onda
        /// expansiva hasta el radio real, marca en el suelo y el gesto propio de la escuela.
        /// </summary>
        public static void AreaBurst(SpellDefinition spell, Vector3 ground, Transform caster)
        {
            if (spell == null)
                return;
            VfxElement element = ElementOf(spell.School);
            VfxPalette palette = PaletteOf(spell.School);
            Color tint = SpellCatalog.SchoolColor(spell.School);
            bool ultimate = spell.Windup >= 0.6f;
            float radius = spell.Radius;
            SpellImpactFlash.Spawn(ground + Vector3.up * 0.35f, tint, ultimate ? 1.6f : 1.2f, ultimate ? 0.55f : 0.45f,
                radius, element);

            VfxHost host = VfxHost.Get();
            if (host == null)
                return;
            switch (element)
            {
                case VfxElement.Fire:
                    // Llamas que barren el suelo hacia fuera; el meteoro deja columna de humo.
                    host.Ring(element, ground, Vector3.up, radius * 0.25f, palette.Spark, ultimate ? 70 : 50,
                        radius * 2.2f, 2.2f, 0.26f, 0.75f);
                    if (ultimate)
                    {
                        host.Burst(element, ground + Vector3.up * 0.3f, Vector3.up, palette.Spark, 45, 9f, 0.24f, 1f, 0.5f, 0.4f);
                        host.Smoke(ground + Vector3.up * 0.8f, radius * 0.25f, new Color(0.08f, 0.06f, 0.05f, 0.7f),
                            18, 1.6f, 1.8f, Vector3.up * 1.6f);
                    }
                    break;
                case VfxElement.Frost:
                    if (ultimate || spell.Delivery == SpellDelivery.Nova)
                    {
                        // Ventisca: esquirlas que salen disparadas y niebla helada que se extiende.
                        host.Ring(element, ground, Vector3.up, radius * 0.2f, palette.Spark, 60, radius * 2.6f, 1.5f, 0.24f, 0.6f);
                        host.Smoke(ground + Vector3.up * 0.4f, radius * 0.5f, new Color(0.82f, 0.94f, 1f, 0.4f), 16, 1.5f, 1.3f,
                            Vector3.up * 0.3f);
                    }
                    else
                    {
                        // Prision glacial: barrotes de hielo que brotan en el borde.
                        host.Ring(element, ground, Vector3.up, radius * 0.92f, palette.Spark, 42, 0.4f, 8f, 0.26f, 0.55f);
                        host.Burst(element, ground + Vector3.up * 0.2f, Vector3.up, palette.Spark, 20, 6f, 0.22f, 0.5f, 0.35f, radius * 0.4f);
                    }
                    break;
                case VfxElement.Storm:
                    // Descarga final en el centro (dos rayos entrelazados) y chispas por el area.
                    LightningArc.Strike(ground + new Vector3(0.6f, 13f, -0.4f), ground, palette.Glow, 0.3f, 0.4f, 0.8f);
                    LightningArc.Strike(ground + new Vector3(-0.5f, 13f, 0.3f), ground, palette.Core, 0.14f, 0.32f, 1.1f);
                    host.Ring(element, ground, Vector3.up, radius * 0.3f, palette.Spark, 50, radius * 2.4f, 2.5f, 0.16f, 0.35f);
                    break;
                case VfxElement.Shadow:
                    // Eclipse: anillo de humo negro que se expande y deja una mancha.
                    host.Ring(element, ground, Vector3.up, radius * 0.3f, palette.Spark, 48, radius * 1.6f, 0.4f, 0.75f, 1.1f);
                    host.Smoke(ground + Vector3.up * 0.6f, radius * 0.35f, palette.Spark, 16, 1.2f, 1.2f, Vector3.up * 0.25f);
                    break;
                case VfxElement.Blood:
                    host.Ring(element, ground, Vector3.up, radius * 0.25f, palette.Spark, 54, radius * 2f, 3.5f, 0.2f, 0.75f);
                    if (spell.LifeSteal > 0f && caster != null)
                        Drain(ground + Vector3.up * 0.6f, caster, radius * 0.5f);
                    break;
                case VfxElement.Light:
                    // Juicio celestial: pilar de luz desde el cielo y rayos hacia fuera.
                    LightningArc.Strike(ground + Vector3.up * 16f, ground, palette.Glow, radius * 0.45f, 0.5f, 0f);
                    LightningArc.Strike(ground + Vector3.up * 16f, ground, palette.Core, radius * 0.18f, 0.42f, 0f);
                    host.Burst(element, ground + Vector3.up * 0.4f, Vector3.up, palette.Spark, 40, 10f, 0.2f, 0.45f, 0.9f, 0.3f);
                    host.Glints(ground + Vector3.up * 1.2f, radius * 0.6f, palette.Spark, 24, 0.22f, 1.1f, Vector3.up * 1.2f);
                    break;
            }
        }

        /// <summary>Motas de sangre que vuelan del area al mago (robo de vida visible).</summary>
        public static void Drain(Vector3 from, Transform caster, float spread)
        {
            VfxHost host = VfxHost.Get();
            if (host == null || caster == null)
                return;
            Vector3 to = caster.position + Vector3.up * 1.1f;
            const float travel = 0.45f;
            host.Glints(from, spread, new Color(1f, 0.15f, 0.22f, 1f), 18, 0.18f, travel, (to - from) / travel);
        }

        /// <summary>
        /// Remate de un area durante el final de su preparacion: empieza
        /// <paramref name="delay"/> segundos despues y dura <paramref name="travel"/>, de modo
        /// que termina justo cuando el area estalla. Devuelve null si la escuela no tiene remate.
        /// </summary>
        public static Coroutine Descent(SpellDefinition spell, Vector3 ground, float delay, float travel)
        {
            if (spell == null || travel <= 0.05f)
                return null;
            VfxHost host = VfxHost.Get();
            if (host == null)
                return null;
            switch (ElementOf(spell.School))
            {
                case VfxElement.Fire:
                case VfxElement.Storm:
                case VfxElement.Light:
                case VfxElement.Blood:
                case VfxElement.Frost:
                    return host.StartCoroutine(DescentRoutine(spell, ground, Mathf.Max(0f, delay), travel));
                default:
                    return null;
            }
        }

        public static void StopDescent(Coroutine routine)
        {
            if (routine == null)
                return;
            VfxHost host = VfxHost.Get();
            if (host != null)
                host.StopCoroutine(routine);
        }

        private static IEnumerator DescentRoutine(SpellDefinition spell, Vector3 ground, float delay, float travel)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            VfxHost host = VfxHost.Get();
            if (host == null)
                yield break;
            VfxElement element = ElementOf(spell.School);
            VfxPalette palette = PaletteOf(spell.School);
            float radius = spell.Radius;

            if (element == VfxElement.Fire)
            {
                // Meteoro: cae en diagonal y llega en el instante del estallido. Sale bajo y
                // de lado para que la camara lejana lo vea caer y no solo estallar.
                VfxPool<VfxOrb> pool = host.Pool(VfxOrb.Factory);
                VfxOrb meteor = pool.Take();
                Vector3 start = ground + new Vector3(-3.5f, 6.5f, -1.5f);
                meteor.gameObject.SetActive(true);
                meteor.Configure(VfxElement.Fire, SpellCatalog.SchoolColor(spell.School), 1.5f, 0.5f);
                meteor.Teleport(start, (ground - start) / travel);
                float t = 0f;
                while (t < 1f && meteor != null)
                {
                    t = Mathf.Min(1f, t + Time.deltaTime / travel);
                    meteor.Step(Vector3.Lerp(start, ground, t), Time.deltaTime);
                    yield return null;
                }
                if (meteor == null)
                    yield break;
                meteor.Extinguish();
                yield return new WaitForSeconds(meteor.LingerSeconds);
                if (meteor != null)
                    pool.Release(meteor);
                yield break;
            }

            float elapsed = 0f;
            float nextPulse = 0f;
            while (elapsed < travel)
            {
                if (elapsed >= nextPulse)
                {
                    Vector2 inside = Random.insideUnitCircle * radius * 0.85f;
                    Vector3 point = ground + new Vector3(inside.x, 0f, inside.y);
                    switch (element)
                    {
                        case VfxElement.Storm:
                            // Rayos sueltos que caen dentro del circulo antes de la descarga.
                            nextPulse = elapsed + travel / 4f;
                            LightningArc.Strike(point + new Vector3(Random.Range(-1f, 1f), 12f, Random.Range(-1f, 1f)), point,
                                palette.Glow, 0.16f, 0.2f, 0.7f);
                            host.Burst(element, point + Vector3.up * 0.1f, Vector3.up, palette.Spark, 10, 5f, 0.12f, 0.3f, 0.8f, 0.1f);
                            break;
                        case VfxElement.Light:
                            // Motas de luz que bajan del cielo al circulo.
                            nextPulse = elapsed + 0.06f;
                            host.Glints(point + Vector3.up * 5f, 0.4f, palette.Spark, 3, 0.2f, 0.55f, Vector3.down * 8f);
                            break;
                        case VfxElement.Blood:
                            // La sangre del suelo empieza a hervir y subir.
                            nextPulse = elapsed + 0.08f;
                            host.Burst(element, point, Vector3.up, palette.Spark, 4, 3.5f, 0.14f, 0.5f, 0.25f, 0.1f);
                            break;
                        case VfxElement.Frost:
                            // Escarcha que se condensa en el borde.
                            nextPulse = elapsed + 0.09f;
                            Vector2 edge = Random.insideUnitCircle.normalized * radius * 0.9f;
                            host.Smoke(ground + new Vector3(edge.x, 0.15f, edge.y), 0.3f, new Color(0.85f, 0.95f, 1f, 0.35f),
                                2, 0.7f, 0.7f, Vector3.up * 0.2f);
                            break;
                        default:
                            nextPulse = travel;
                            break;
                    }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
    }
}
