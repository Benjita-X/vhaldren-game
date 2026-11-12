using UnityEngine;
using Vhaldren.Gameplay.Combat;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Vhaldren.Gameplay.Player
{
    /// <summary>
    /// Controles de defensa para el prototipo: Z escudo, C barrera runica,
    /// X parada precisa. No usa F/E/Q para evitar colisiones con interaccion,
    /// estallido de sangre y pociones de otros agentes.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatDefense))]
    public sealed class PlayerDefensiveInput : MonoBehaviour
    {
        private CombatDefense defense;
        private CombatHealth health;
        private PlayerActionModifiers actionModifiers;
        private PlayerAnimationDriver animations;
        private PlayerGuardVisual guardVisual;
        private CombatDefense.GuardMode lastGuard;
        private string status = "";
        private float nextStatus;

        private void Awake()
        {
            defense = GetComponent<CombatDefense>();
            health = GetComponent<CombatHealth>();
            actionModifiers = GetComponent<PlayerActionModifiers>();
            animations = GetComponent<PlayerAnimationDriver>();
            guardVisual = GetComponent<PlayerGuardVisual>();
            if (guardVisual == null && Application.isPlaying)
                guardVisual = gameObject.AddComponent<PlayerGuardVisual>();
        }

        private void Update()
        {
            if (defense == null)
                return;
            if (actionModifiers != null && !actionModifiers.AllowsAttack)
            {
                defense.SetGuard(CombatDefense.GuardMode.None);
                return;
            }
            if (health != null && health.IsDefeated)
            {
                defense.SetGuard(CombatDefense.GuardMode.None);
                return;
            }

            if (ParryPressed() && defense.TryParry())
            {
                if (animations == null)
                    animations = GetComponent<PlayerAnimationDriver>();
                animations?.PlayCombatAction(PlayerAnimationDriver.CombatAction.Parry);
                guardVisual?.FlashParry();
            }

            CombatDefense.GuardMode mode = ShieldHeld()
                ? CombatDefense.GuardMode.PhysicalShield
                : WardHeld()
                    ? CombatDefense.GuardMode.ArcaneWard
                    : CombatDefense.GuardMode.None;
            defense.SetGuard(mode);
            CombatDefense.GuardMode acceptedGuard = defense.Guard;
            if (acceptedGuard != lastGuard)
            {
                if (animations == null)
                    animations = GetComponent<PlayerAnimationDriver>();
                if (acceptedGuard == CombatDefense.GuardMode.PhysicalShield)
                    animations?.PlayCombatAction(PlayerAnimationDriver.CombatAction.Shield);
                else if (acceptedGuard == CombatDefense.GuardMode.ArcaneWard)
                    animations?.PlayCombatAction(PlayerAnimationDriver.CombatAction.Ward);
                lastGuard = acceptedGuard;
            }

            if (Time.time >= nextStatus)
            {
                nextStatus = Time.time + 0.15f;
                string modeName = defense.Guard ==
                    CombatDefense.GuardMode.PhysicalShield ? "ESCUDO FÍSICO" :
                    defense.Guard == CombatDefense.GuardMode.ArcaneWard
                        ? "BARRERA MÁGICA" : "SIN BLOQUEO";
                status = (defense.GuardBroken ? "GUARDIA ROTA | " : "") +
                    modeName + " | RESISTENCIA: " +
                    Mathf.CeilToInt(defense.Stamina) + "/" +
                    Mathf.CeilToInt(defense.MaximumStamina) +
                    " | PARADA: " + (defense.CanParry ? "LISTA" : "RECARGANDO");
            }
        }

        private void OnDisable()
        {
            if (defense != null)
                defense.SetGuard(CombatDefense.GuardMode.None);
            lastGuard = CombatDefense.GuardMode.None;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || defense == null)
                return;
            Rect rect = new Rect(12f, 132f, Mathf.Min(650f, Screen.width - 24f), 46f);
            GUI.Box(rect, "");
            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, 20f), status);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 23f, rect.width - 16f, 18f),
                "Mantener Z: escudo contra golpes | C: barrera mágica | X: parada precisa | Espacio: esquiva");
        }

        private static bool ShieldHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.zKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.Z);
#else
            return false;
#endif
        }
        private static bool WardHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.cKey.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(KeyCode.C);
#else
            return false;
#endif
        }
        private static bool ParryPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.xKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.X);
#else
            return false;
#endif
        }
    }
}
