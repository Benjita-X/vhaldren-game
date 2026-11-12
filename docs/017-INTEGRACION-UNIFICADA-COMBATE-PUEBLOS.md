# 017 — Una línea de desarrollo: combate legible, curación precisa y primer pueblo jugable

Fecha: 12 de noviembre de 2026 · Rama: `feature/017-integracion-jugable-pueblos-combate` · Base: `feature/016-progresion-raza-recoleccion`.

## Estado verificado en GitHub

- La línea **015→016→017** contiene la escena de mundo `WorldPrototype001.unity`, supervivencia, recolección, herramientas, diez piezas de construcción, defensa, armas, barra de PV, magias y documentación previa.
- La rama `wip/codex-009-010` diverge de 016 (un commit WIP frente a más de cien posteriores). Incluye **otro inventario y otro constructor** bajo `Assets/Scripts/Player`, y magias que competían con E=recolección. No se fusiona la rama entera: duplicaría sistemas y podría romper prefabs/escenas.
- **Rescatado desde WIP:** `PrototypeVfx.cs` + `SpellImpactFlash.cs` (conservando sus .meta/GUID) y conectado a impactos reales de espada, pulso y escarcha. No se transfirió `PlayerSpellCaster`, `SurvivalBuilder`, `SurvivalInventory` de WIP ni el proyecto/escena WIP.
- **Fuente de verdad propuesta:** `feature/017-integracion-jugable-pueblos-combate`; una carpeta Unity abierta en esa rama. **GitHub no puede inspeccionar ni borrar las dos copias locales del computador**: no eliminar ninguna hasta respaldar cambios locales y compilar/validar 017.

## Fallos concretos corregidos

### Curación: «Q +35» pero recibes +25
`CombatHealth.Heal(35f)` aplica solo la vida faltante. Si el jugador tiene 125/150, debe curar **25** y no 35. El error era anunciar siempre +35. `PrototypeGameSession` muestra los PV que se recuperarían realmente, `LastPotionHealed`, mensaje del valor aplicado y explicación del cooldown/vida llena. No consume una poción si no recupera vida.

`CampfireComfort` curaba cada Update y generaba demasiados eventos `Healed` y números verdes diminutos. Ahora emite un pulso de 4 PV cada segundo. Esto hace el HUD legible, aunque resta testear la sensación en Play.

### Habilidades que parecen no funcionar
`PlayerCombatAbilities` ahora avisa cuando:
- la habilidad está recargando y cuántos segundos faltan;
- falta resistencia;
- el personaje está derrotado;
- el modo construcción o un diálogo bloquean combate.

V (espada), T (pulso rúnico) y F (escarcha) activan triggers de animación **si el Animator incluye el estado correspondiente** y crean impacto visible con efectos recuperados del trabajo WIP. Los tiempos y el daño siguen calculándose por los sistemas anteriores, no por la UI.

### Animaciones de defensa
Z (escudo) muestra arco físico ambar y una animación de preparación; C (barrera) un anillo azulado y gesto mágico; X (parry) destello y animación corta. Hay mensajes de guardia rota, resistencia y disponibilidad de parry.
`PlayerGuardVisual` es solo presentación y no cambia hitboxes ni locomoción.
`KayKitCombatAnimationUpgrade` añade al `RogueHooded.controller` los estados y parámetros nuevos **que faltan**, sin borrar los estados existentes. Se ejecuta al cargar el proyecto en el Editor, y también existe menú `Vhaldren > Personaje > Revisar y completar animaciones`. Si el FBX no trae algún clip, lo avisa en Console.

### Construcción e interfaz
`PlayerBuilder` actualiza referencias a los componentes de combate en el momento de activar B. Algunos componentes se instalan desde `PrototypeGameSession.OnEnable`, después de `PlayerBuilder.Awake`; capturarlos una sola vez dejaba habilidades activas al construir. Ahora se bloquean y se restauran correctamente.
Los paneles de vida, resistencia y habilidades se separaron verticalmente. Abrir el tablero de misiones bloquea ataques para evitar disparar al interactuar.

## Primer pueblo funcional: Puesto del Cruce

Se crea junto al camino inicial **un tablón físico 3D** en `(-4,0,-6)` al iniciar `WorldPrototype001`, si la escena tiene inventario. La sesión es dueña del tablón y lo destruye al cerrar para no contaminar pruebas o escenas aditivas.

- **N:** abrir/cerrar el tablón, solo cerca.
- **Y:** aceptar un encargo o reclamar recompensa cuando se cumplen condiciones.
- **O:** elegir oferta de mercado.
- **P:** comprar si hay recursos, stock y reputación suficientes.

### Cadena inicial de 6 encargos
1. **Refugio para los desplazados:** entrega 6 madera. Recompensa +3 fibra, reputación mixta +2.
2. **Piedra para el puente:** requiere la cadena de picos del 016 para obtener y entregar 5 piedra. Recompensa +2 esencia, reputación humana +2.
3. **Guardianes del camino:** dos guardianes derrotados tras aceptar. Recompensa +3 esencia, reputación mixta +3.
4. **Prueba de la academia:** dos hechizos aceptados tras tomar encargo. Recompensa +2 cristal, reputación arcana +3.
5. **Fuego para el pueblo:** construir una fogata. Recompensa +5 comida, reputación mixta +3.
6. **Juramento del altar:** vencer al jefe. Recompensa +6 esencia, reputación mixta +6.

No cuenta clicks falsos, intentos sin materiales, ataques rechazados ni eventos previos a aceptar el objetivo. Reclamaciones repetidas se rechazan.

### Mercado y eventos
Ofertas iniciales: comida por fibra, fibras por piel y cristales por esencia. Mercader con stock limitado por evento; precios modificados por reputación y situación de la caravana. Las transacciones pasan por `SurvivalInventory.TryExchange`: cuesta y entrega en una sola operación con comprobación previa de capacidad, sin gasto parcial.

Eventos narrativos actuales: caravana de refugiados, anomalía arcana, cacería lunar, presagios no muertos, emisarios celestiales y delegación infernal. Cambian el mercado y se anuncian en el tablón. **Todavía no generan automáticamente aldeanos con IA ni invasiones 3D**; esos comportamientos requieren escenas/NPC y navegación.

## Diseño de pueblos, facciones y narrativa para próximas escenas

La Tierra será un espacio de conflicto político entre clanes, no un mapa dividido mecánicamente en «pueblo bueno/pueblo malo». Las otras razas no serán automáticamente hostiles según su apariencia.

| Asentamiento | Motivación narrativa | Eventos reales a implementar | Reglas de juego |
|---|---|---|---|
| Cruce mixto | Refugiados entre rutas de tres reinos | Caravana, asalto de bandidos, consejo dividido | Comercio común, encargos de protección, reputación de varias facciones |
| Academia arcana | Magos disputan una fuente de energía | Examen, fuga arcana, duelo | Mentor enseña hechizo tras pruebas, resistencias mágicas especiales |
| Villa licántropa | Clan lucha por proteger su territorio | Luna, rastreo, desafío del alfa | Sigilo/olfato, transformación, reputación basada en pactos |
| Ciudad vampírica | Casas nobles compiten por sangre y poder | Cónclave nocturno, caza de reliquias | Intrigas, sigilo, diurno/nocturno; no solo combate |
| Necrópolis | No muertos buscan memoria e identidad | Despertar, peste de almas, ritual | Tratos con espíritus, hechizos de muerte y consecuencias persistentes |
| Reino celestial | Órdenes de guardianes y jerarquías de luz | Juicios, enviados, ruptura de voto | Pruebas de movilidad vertical, escudos, decisiones difíciles |
| Corte infernal | Familias y principados demoníacos rivales | Sucesión, pacto, guerra de rutas | Reputación, cláusulas de pacto, recursos de riesgo alto |

**La magia tendrá escuelas** (runas, sangre, escarcha, luz, sombras, evocación) con aprendizajes por mentores, grimorios, práctica y consecuencias. No copiar hechizos reconocibles de otra franquicia. Los celestiales pueden incorporar jerarquías y formas artísticas inspiradas en tradiciones históricas (serafines, querubines, guardianes), con diseños originales y modelos/animaciones propios. Las razas, transformaciones, ciudades, cielo e infierno completos **todavía no son escenas terminadas**.

### Que el juego sea difícil sin frustrar
- Telegraph de jefe, defensas correctas por tipo de daño, ventanas de contraataque, stamina y cooldown comunicados.
- El enemigo debe elegir entre perseguir, proteger, flanquear, pedir apoyo o retroceder según su rol; no recibir velocidad infinita por dificultad.
- Un evento de incursión necesita aviso, preparación y recompensa, no destruir bases de forma arbitraria.
- Cada decisión de pueblo afecta acceso a mentores, comercio, tareas y alianzas, pero nunca elimina irreversiblemente el progreso básico por un clic.
- La dificultad debe crecer mediante nuevas combinaciones y patrones, no solo multiplicar los PV de todos los enemigos.

## Matriz de input / control
WASD mover; Espacio esquiva; clic izquierdo ballesta; V espada; T pulso rúnico; F escarcha; Z escudo físico; C barrera mágica; X parada; Q poción; B construcción; 1–0 pieza; R girar; G desmontar; E recolectar; J/K fabricar picos; N abrir tablón; Y misión; O oferta; P comprar.

Cuando hay diálogo o construcción no debe salir una flecha por el mismo clic. Las entradas rechazadas tienen motivo visible. El buffer de 2 disparos heredado de 016 conserva peticiones independientes según cooldown.

## Validación pendiente en Unity (obligatoria antes de fusionar)

1. Unity 6000.2.12f1: compilar rama 017, revisar Console, verificar si Animator fue actualizado sin borrar clips.
2. Ejecutar **todas** las suites EditMode y PlayMode. Se añadieron 5 pruebas nuevas de integración 017, pero NO se han ejecutado.
3. Q al faltarle 25/35/50 PV, Q en cooldown y estando al lado de fogata. Valores del HUD y números verdes deben coincidir con PV aplicados.
4. V/T/F: con stamina suficiente, sin stamina, en cooldown, al construir (B), mientras se lee tablón (N), cuando mueres. Ver animación/FX y daño real sin atravesar paredes.
5. Z contra golpe físico, C contra magia y X parry. Comprobar el efecto 3D, la reducción real y la salida correcta de estado al soltar tecla.
6. N/Y cerca/lejos: aceptar, entregar materiales, reclamar una sola vez, morir/reaparecer. O/P: reputación, capacidad, stock, descuento por caravana.
7. Abrir/cerrar escena varias veces y Test Runner para verificar que no quedan tablones huérfanos, materiales duplicados ni listeners retenidos.
8. 30/60/120 FPS, spam de magia, 2–3 enemigos, impactos múltiples y efectos encadenados. Medir FPS, memoria, GC y luces de los impactos.

**Limitación verificada:** desde GitHub no puedo inspeccionar ni unir dos directorios de proyecto abiertos en el computador, ni ejecutar Unity. La rama 017 está subida pero NO está certificada como ejecutable hasta hacer las pruebas anteriores. No fusionar a main todavía.
