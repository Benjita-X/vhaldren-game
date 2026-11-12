using UnityEngine;

namespace Vhaldren.Gameplay.World
{
    /// <summary>
    /// Estado local del Cruce. Los eventos alteran seguridad, provisiones
    /// y tension arcana; completar encargos provoca cambios persistentes
    /// durante la sesion, en vez de mostrar solo nombres narrativos.
    /// Los NPC/edificios/fisica seguiran siendo tareas independientes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettlementState : MonoBehaviour
    {
        [SerializeField, Range(0, 100)] private int security = 45;
        [SerializeField, Range(0, 100)] private int supplies = 50;
        [SerializeField, Range(0, 100)] private int morale = 55;
        [SerializeField, Range(0, 100)] private int arcaneTension = 12;

        private VillageQuestJournal journal;
        private VillageEvent lastEvent;
        private int lastCompletedChapters;
        private bool bound;

        public int Security => security;
        public int Supplies => supplies;
        public int Morale => morale;
        public int ArcaneTension => arcaneTension;
        public bool RouteSafe => security >= 60 && supplies >= 35;
        public bool HasArcaneCrisis => arcaneTension >= 70;

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            if (journal != null)
                journal.Changed -= Refresh;
            bound = false;
        }

        private void Update()
        {
            if (!bound)
                Bind();
        }

        public void Bind(VillageQuestJournal source = null)
        {
            if (bound)
                return;
            journal = source;
            if (journal == null)
            {
                GameObject player = GameObject.Find("PlayerRoot");
                if (player != null)
                    journal = player.GetComponent<VillageQuestJournal>();
            }
            if (journal == null)
                return;

            lastEvent = journal.CurrentEvent;
            lastCompletedChapters = 0;
            journal.Changed += Refresh;
            bound = true;
            Refresh();
        }

        private void Refresh()
        {
            if (journal == null)
                return;

            // Reprocesar cada capítulo solo una vez, aunque Changed se emita
            // por inventario, misiones o HUD varias veces por fotograma.
            int complete = journal.CompletedChapters;
            while (lastCompletedChapters < complete)
            {
                ApplyQuestOutcome(lastCompletedChapters);
                lastCompletedChapters++;
            }

            if (journal.CurrentEvent != lastEvent)
            {
                lastEvent = journal.CurrentEvent;
                ApplyEvent(lastEvent);
            }
        }

        public void ApplyQuestOutcome(int chapterIndex)
        {
            switch (chapterIndex)
            {
                case 0: Change(0, +12, +9, 0); break;    // refugio
                case 1: Change(+13, 0, +3, 0); break;    // puente
                case 2: Change(+19, 0, +7, 0); break;    // guardianes
                case 3: Change(0, 0, +5, -17); break;    // academia
                case 4: Change(0, +8, +10, 0); break;   // fogata
                case 5: Change(+25, 0, +18, -7); break;  // jefe
            }
        }

        public void ApplyEvent(VillageEvent type)
        {
            switch (type)
            {
                case VillageEvent.RefugeeCaravan:
                    Change(0, -6, +5, 0); break;
                case VillageEvent.ArcaneDisturbance:
                    Change(-3, 0, -3, +12); break;
                case VillageEvent.MoonlitHunt:
                    Change(-8, -4, -3, 0); break;
                case VillageEvent.UndeadOmen:
                    Change(-8, 0, -5, +4); break;
                case VillageEvent.CelestialEnvoys:
                    Change(0, +3, +6, -7); break;
                case VillageEvent.InfernalDelegation:
                    Change(-4, +4, -7, +9); break;
            }
        }

        private void Change(int securityDelta, int suppliesDelta,
            int moraleDelta, int arcaneDelta)
        {
            security = Mathf.Clamp(security + securityDelta, 0, 100);
            supplies = Mathf.Clamp(supplies + suppliesDelta, 0, 100);
            morale = Mathf.Clamp(morale + moraleDelta, 0, 100);
            arcaneTension = Mathf.Clamp(arcaneTension + arcaneDelta, 0, 100);
        }

        public string CurrentSituation()
        {
            if (HasArcaneCrisis)
                return "CRISIS ARCANA: el consejo necesita a los magos";
            if (security < 35)
                return "RUTAS EN PELIGRO: las patrullas piden ayuda";
            if (supplies < 25)
                return "ESCASEZ: los viajeros necesitan provisiones";
            if (RouteSafe)
                return "CAMINOS ABIERTOS: rutas seguras para comerciantes";
            return "FRONTERA INESTABLE: el pueblo vigila los caminos";
        }
    }
}
