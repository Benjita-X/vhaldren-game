using UnityEngine;

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Ballesta tridimensional sencilla montada en ShotOrigin del modelo.
    /// No crea colision, no cambia apuntado ni disparo: solo equipo visible.
    /// Se instancia una vez al activar las flechas fisicas en el mundo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrossbowVisual : MonoBehaviour
    {
        private static Material wood;
        private static Material iron;
        private static Material cord;
        private Transform weapon;

        public void Attach(Transform shotOrigin)
        {
            if (shotOrigin == null)
                return;
            if (weapon == null)
            {
                Transform existing = shotOrigin.Find("Ballesta visible");
                weapon = existing != null ? existing : new GameObject("Ballesta visible").transform;
                weapon.SetParent(shotOrigin, false);
                weapon.localPosition = Vector3.zero;
                weapon.localRotation = Quaternion.identity;
            }
            weapon.gameObject.SetActive(true);
            if (weapon.childCount > 0)
                return;
            EnsureMaterials();

            Piece("Culata de madera", PrimitiveType.Cube,
                new Vector3(0f, -0.13f, -0.22f),
                new Vector3(0.16f, 0.13f, 0.62f), wood);
            Piece("Rail de flecha", PrimitiveType.Cube,
                new Vector3(0f, -0.04f, 0.16f),
                new Vector3(0.13f, 0.09f, 0.66f), iron);
            Piece("Estribo", PrimitiveType.Cube,
                new Vector3(0f, -0.06f, 0.54f),
                new Vector3(0.29f, 0.08f, 0.13f), iron);
            Piece("Palanca", PrimitiveType.Cube,
                new Vector3(0f, -0.26f, -0.13f),
                new Vector3(0.10f, 0.27f, 0.09f), wood);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject arm = Piece("Brazo flexible", PrimitiveType.Cube,
                    new Vector3(0.29f * side, -0.01f, 0.31f),
                    new Vector3(0.58f, 0.08f, 0.12f), wood);
                arm.transform.localRotation = Quaternion.Euler(0f, -side * 18f, 0f);
                Piece("Punta metalica", PrimitiveType.Cube,
                    new Vector3(0.60f * side, -0.01f, 0.40f),
                    new Vector3(0.11f, 0.10f, 0.13f), iron);
            }

            GameObject stringObject = new GameObject("Cuerda tensada");
            stringObject.transform.SetParent(weapon, false);
            LineRenderer line = stringObject.AddComponent<LineRenderer>();
            line.sharedMaterial = cord;
            line.useWorldSpace = false;
            line.positionCount = 3;
            line.widthMultiplier = 0.018f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.SetPosition(0, new Vector3(-0.60f, 0f, 0.40f));
            line.SetPosition(1, new Vector3(0f, 0f, -0.12f));
            line.SetPosition(2, new Vector3(0.60f, 0f, 0.40f));
        }

        public void Hide()
        {
            if (weapon != null)
                weapon.gameObject.SetActive(false);
        }

        private GameObject Piece(string title, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = title;
            part.transform.SetParent(weapon, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider body = part.GetComponent<Collider>();
            if (body != null)
            {
                body.enabled = false;
                Destroy(body);
            }
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static void EnsureMaterials()
        {
            if (wood != null && iron != null && cord != null)
                return;
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            wood = Make(shader, new Color(0.34f, 0.21f, 0.12f));
            iron = Make(shader, new Color(0.65f, 0.70f, 0.75f));
            cord = Make(shader, new Color(0.91f, 0.86f, 0.74f));
        }

        private static Material Make(Shader shader, Color color)
        {
            Material material = new Material(shader);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }
    }
}
