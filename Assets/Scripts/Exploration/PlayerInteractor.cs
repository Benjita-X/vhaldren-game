using UnityEngine;
using Vhaldren.Gameplay.Combat;
using Vhaldren.Gameplay.Settings;
using Vhaldren.Gameplay.Survival;
using Vhaldren.Gameplay.UI;

namespace Vhaldren.Gameplay.Exploration
{
    /// <summary>
    /// Interaccion con la tecla de recolectar (E): elige el <see cref="ExplorationInteractable"/>
    /// mas cercano y muestra su aviso encima de la barra de accion. Si hay un recurso para
    /// recolectar mas cerca, gana la recoleccion (y las acciones de mantener ceden siempre
    /// ante un recurso en alcance). Tambien dibuja el panel de las piedras de saber.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private ExplorationInteractable target;
        private float holdProgress;
        private CombatHealth health;
        private PlayerBuilder builder;

        public ExplorationInteractable Target => target;
        public float HoldProgress => holdProgress;

        private void Update()
        {
            if (health == null) health = GetComponent<CombatHealth>();
            if (builder == null) builder = GetComponent<PlayerBuilder>();

            if (LoreReader.IsOpen)
            {
                LoreStone source = LoreReader.Source;
                bool far = source == null || (source.transform.position - transform.position).sqrMagnitude >
                           Mathf.Pow(source.InteractRadius + 2.5f, 2f);
                if (far || GameInput.Down(GameAction.Gather) || GameInput.Down(GameAction.Map) ||
                    GameInput.Down(GameAction.Pause) || (health != null && health.IsDefeated))
                    LoreReader.Close();
                SetTarget(null);
                return;
            }

            bool blocked = Time.timeScale <= 0f || ExplorationUi.MapOpen ||
                           NPC.NpcDialogue.IsOpen || NPC.NpcDialogue.ClaimsInteractKey ||
                           (health != null && health.IsDefeated) || (builder != null && builder.IsBuilding) ||
                           // un aldeano cerca o un diálogo abierto se quedan con la tecla E
                           Vhaldren.Gameplay.NPC.NpcDialogue.IsOpen || Vhaldren.Gameplay.NPC.NpcDialogue.ClaimsInteractKey;
            float distance = float.PositiveInfinity;
            ExplorationInteractable next = blocked ? null : ExplorationInteractable.Nearest(transform.position, out distance);
            if (next != null)
            {
                HarvestNode node = HarvestNode.Nearest(transform.position, PlayerGatherer.Range);
                if (node != null)
                {
                    Vector3 offset = node.transform.position - transform.position;
                    offset.y = 0f;
                    if (next.HoldSeconds > 0f || offset.magnitude < distance)
                        next = null;
                }
            }
            SetTarget(next);
            if (target == null)
                return;

            if (target.HoldSeconds <= 0f)
            {
                if (GameInput.Down(GameAction.Gather))
                    target.Interact(gameObject);
                return;
            }
            if (GameInput.Held(GameAction.Gather))
            {
                holdProgress = Mathf.Min(1f, holdProgress + Time.deltaTime / target.HoldSeconds);
                target.OnHoldProgress(holdProgress);
                if (holdProgress >= 1f)
                {
                    ExplorationInteractable done = target;
                    holdProgress = 0f;
                    done.OnHoldProgress(0f);
                    done.Interact(gameObject);
                }
            }
            else if (holdProgress > 0f)
            {
                holdProgress = 0f;
                target.OnHoldProgress(0f);
            }
        }

        private void SetTarget(ExplorationInteractable next)
        {
            if (next == target)
                return;
            if (target != null && holdProgress > 0f)
                target.OnHoldProgress(0f);
            holdProgress = 0f;
            target = next;
        }

        private void OnDisable()
        {
            SetTarget(null);
            if (LoreReader.IsOpen)
                LoreReader.Close();
        }

        private void OnGUI()
        {
            GUI.depth = -11;
            if (Event.current.type != EventType.Repaint || HudMode.Hidden)
                return;
            if (!LoreReader.IsOpen && (target == null || !target.CanInteract))
                return;
            MenuStyle.Begin();
            float w = MenuStyle.Width, h = MenuStyle.Height;
            if (LoreReader.IsOpen)
                LoreReader.Draw(w, h);
            else
                DrawPrompt(w, h);
            MenuStyle.End();
        }

        /// <summary>Aviso justo encima del de la barra de accion (recolectar, tablon...).</summary>
        private void DrawPrompt(float w, float h)
        {
            string key = GameInput.Label(GameAction.Gather);
            string text = (target.HoldSeconds > 0f ? "Mantén " + key : key) + ": " + target.PromptVerb;
            string detail = target.PromptDetail;
            float scale = ActionBarHud.BarScale(w);
            float y = h - 232f * scale;
            float tw = HudSkin.TextWidth(text, 16) + 60f;
            Rect band = new Rect(w * 0.5f - tw * 0.5f, y, tw, 26f);
            HudSkin.SoftPanel(new Rect(band.x - 10f, band.y - 4f, band.width + 20f, band.height + (string.IsNullOrEmpty(detail) ? 8f : 22f)), 0.6f);
            HudSkin.Label(band, text, 16, target.PromptColor, TextAnchor.MiddleCenter, true);
            if (!string.IsNullOrEmpty(detail))
                HudSkin.Label(new Rect(band.x, band.yMax - 2f, band.width, 16f), detail, 12, HudSkin.TextDim);
            if (holdProgress > 0f)
            {
                Rect bar = new Rect(w * 0.5f - 110f, band.yMax + (string.IsNullOrEmpty(detail) ? 4f : 16f), 220f, 5f);
                HudSkin.Fill(bar, new Color(0f, 0f, 0f, 0.65f));
                HudSkin.Fill(new Rect(bar.x, bar.y, bar.width * holdProgress, bar.height), target.PromptColor);
            }
        }
    }
}
