# 020 — Auditoría de Google Drive y contrato Mixamo → Vhaldren

**Fecha:** 2026-11-12. **Objetivo:** preparar la integración futura de 180 FBX sin tocar los controladores KayKit ni perder el trabajo local no publicado.
**Drive (fuente privada):** https://drive.google.com/drive/folders/1zD1WZWYGAlSgF3j5zivgM4KxTg6M7Dn3
**Rama auditada antes de añadir este contrato:** feature/018-consolidacion-pre-modelado, b918ce7ba7.

## 1. Inventario real, inspección BINARIA, no solo nombres

| Fuente | FBX | Observación |
|---|---:|---|
| Great Sword Pack.zip | 52 | 1 X Bot con malla + 51 clips |
| Sword and Shield Pack.zip | 50 | 1 X Bot con malla + 49 clips |
| Pro Melee Axe Pack.zip | 48 | 1 X Bot con malla + 47 clips |
| Magic Spell Pack.zip | 14 | 1 X Bot con malla + 13 clips |
| FBX sueltos | 16 | 8 con malla y 8 solo animación |
| MP4 de referencia | 1 | 129,6 s, vertical 576×1024, 30 fps, combate cenital |

**Resultado de leer los 180 FBX:** 180/180 se pudieron analizar; versión FBX 7700; los 180 comparten **exactamente los mismos 65 nombres de huesos** \`mixamorig:\` y cuentan con 315 curvas de animación. **168 FBX no tienen malla y 12 sí la tienen**. Esto NO demuestra por sí solo que los retargetings sean perfectos, que todos los clips tengan root motion correcto o que sus tiempos sean adecuados: falta importación y prueba visual en Unity.

Los cuatro \`X Bot.fbx\` no son binariamente iguales (hashes diferentes) aunque los nombres de huesos sí coincidan. Importar **solo un X Bot de referencia**. Hay varios nombres repetidos entre ZIP y archivos sueltos (por ejemplo \`Sword And Shield Slash\`, \`Great Sword Slide Attack\` y \`Standing 1H Magic Attack 01\`), pero no hay dos FBX con bytes SHA-256 idénticos. Los archivos sueltos pueden ser alternativas **With Skin**; no ponerlos a la vez que sus correspondientes **Without Skin** sin medir el resultado.

El CSV completo \`Vhaldren_Auditoria_180_Animaciones.csv\` generado en la conversación contiene los 180 nombres, hashes SHA-256, tipo (malla o clip), fuente, bucle sugerido, categoría y carpeta futura. El CSV no se copia automáticamente a Drive ni a GitHub.

## 2. Contrato CON el código existente

- El personaje actual **KayKit** utiliza un \`Animator\` **Generic** y clips del \`RogueHooded.fbx\`. **No insertar clips Mixamo directamente en su controlador** ni cambiar \`KayKitCharacterSetup\`, \`KayKitCombatAnimationUpgrade\` o controladores existentes.
- El camino Mixamo necesita un **personaje Humanoid final** (Avatar válido, malla With Skin, pivote en pies, manos libres). El X Bot es una referencia de importación, **no** el personaje final.
- \`PlayerAnimationDriver\` lee \`PlayerMotor.ActualVelocity\` y dispara los parámetros \`MoveX\`, \`MoveY\`, \`Dodge\`, \`DodgeDir\`, \`Shoot\`, \`Harvest\`, \`HarvestStyle\`, \`SwordAction\`, \`SpellAction\`, \`FrostAction\`, \`ShieldAction\`, \`WardAction\` y \`ParryAction\`. Un futuro controlador Humanoid deberá mantener **tipos y nombres exactos**, o crear un adaptador validado sin duplicar inputs.
- Los enemigos KayKit usan \`Speed\`, \`Attack\`, \`Hit\` a través de \`EnemyAnimationDriver\`. El estado \`Death/Muerte\` añadido por otra sesión **puede estar solamente en disco local**, así que no regenerar \`EnemigoBase.controller\` ni sus 24 overrides hasta integrar sus commits.
- \`Animator.applyRootMotion = false\` en KayKit. Mantener **un único dueño del desplazamiento**, el \`PlayerMotor\`/IA; no permitir a la animación de dodge, giro o ataque mover el \`CharacterController\`. Si la pose contiene desplazamiento de cadera, revisar Root Transform > Bake Into Pose, \`In Place\`, orientación de Avatar y contacto con el suelo.
- El arma se adjunta por hueso de mano (\`Animator.GetBoneTransform(HumanBodyBones.RightHand/LeftHand)\`) a un socket sin collider jugable. Los clips no deben crear, destruir ni teletransportar armas; los colliders, hitboxes, cooldowns y daño siguen gobernados por gameplay.
- Evento de impacto: aceptar primero acción/coste/ventana de juego, después animación, VFX y sonido; utilizar evento de animación solo para feedback o sincronía verificada, nunca para saltarse \`CombatDamageReceiver\`.
- Prioridad sugerida: muerte > aturdimiento fuerte > esquiva válida > habilidad/ataque aceptado > bloqueo > locomoción. Las cancelaciones selectivas deben diseñarse y probarse explícitamente. No consumir input solo porque una transición Animator esté activa.

## 3. Staging aislado (se deja preparada la herramienta)

Ruta única:

\`\`\`text
Assets/ThirdParty/Personajes/Animaciones/Mixamo/
  Reference/X Bot.fbx
  SwordShield/*.fbx
  GreatSword/*.fbx
  Axe/*.fbx
  Magic/*.fbx
  Extras/*.fbx
\`\`\`

**Los FBX de Drive NO se han copiado a GitHub**. El staging está vacío hasta que se añadan los archivos en Unity; no añadir todavía 180 modelos a la vez.

Se añadió \`Assets/Editor/MixamoStagingAudit.cs\` con dos acciones manuales bajo el menú \`Vhaldren/Animaciones Mixamo\`:

1. **Auditar staging (solo lectura):** comprueba importadores Humanoid, X Bot, Avatar, huesos Mixamo, animación activada, nombres repetidos y existencia de malla.
2. **Preparar rigs del staging (confirmar):** previa autorización visible en Unity, importa el X Bot como Humanoid, extrae su Avatar y configura **solo los FBX bajo Mixamo/** como Humanoid usando \`CopyFromOther\` y \`sourceAvatar\`. Marca loops de reposo/caminar/correr/strafe. Jamás toca KayKit, escenas, controladores ni scripts de combate. Reimporta FBX del staging; primero respaldar import settings manuales.

La función NO crea un AnimatorController definitivo. Eso se hará después de comprobar el primer personaje, no antes, para evitar animaciones que compilan pero visualmente patinan.

## 4. Primer ensayo seguro: 1 personaje y 10 clips

Usar \`Sword and Shield Pack.zip\`:

- \`X Bot.fbx\` → \`Reference/\` (único X Bot).
- \`sword and shield idle.fbx\`, \`walk.fbx\`, \`run.fbx\`, \`strafe.fbx\` → \`SwordShield/\`.
- \`sword and shield slash.fbx\`, \`block.fbx\`, \`impact.fbx\`, \`death.fbx\` → \`SwordShield/\`.
- \`Standing 1H Magic Attack 01.fbx\` (versión **sin malla** del ZIP) → \`Magic/\`.
- \`Dodging Right.fbx\` (suelto sin malla) → \`Extras/\`.

**No incorporar el \`Sword And Shield Slash.fbx\` suelto con malla en ese mismo ensayo.** Elegir primero una versión. Comparar después por duración, giro y contacto visual, no por nombre.

Después importar un **personaje With Skin** como Humanoid, verificar Avatar válido y reproducir los diez clips sobre su rig. El X Bot sirve para preflight, no sustituye al personaje de raza final.

## 5. Matriz de regresión antes de conectar al juego

1. Importación: sin errores Console; Avatar válido, 65 huesos de origen, cabeza/manos/pies alineados, escala razonable (1 unidad ≈ 1 m). Verificar FBX skinned vs animation-only y que no reaparezca otro X Bot encima.
2. Reposo/marcha/carrera/strafe: cuatro direcciones, diagonales, apuntado mientras se mueve, frenado sin snap; probar 30/60/120 FPS. El \`CharacterController\` no debe desplazarse por root motion.
3. Dodge: frente/atrás/izquierda/derecha, sin dirección, durante recuperación y cerca de pared/escalera, sin pisar otros inputs; \`PlayerMotor\` mantiene distancias y colisiones.
4. Ataques: no cortar una animación antes del golpe salvo cancelación legítima; hitbox real coincide con golpe visual; cooldown/stamina/daño sólo servidor lógico de gameplay (actualmente local Unity).
5. Magia: preparación y release en mano correcta, barrera, interrupción, 18 hechizos, orden de VFX y registro de daño sin duplicación.
6. Escudo/arma: sockets se mueven con huesos de mano; no armas flotantes, colisiones físicas extra, dos armas superpuestas, ni objetos duplicados por transiciones.
7. Muerte: estado terminal prioritario, colisión desactivada cuando corresponda, cuerpo no reaparece caminando, respawn limpia triggers y velocidad. Probar enemigos KayKit sin cambiar su controlador.
8. Construcción, minería, misiones, cámara, HUD: ninguna acción aceptada bloquea input accidentalmente; se respetan pausas y ventanas de diálogo.
9. Ejecución: suite EditMode + PlayMode completa, log sin errores, build Windows, test real con teclado/mando antes de publicar cambios.

## 6. Límites honestos

- He validado estructura binaria y nomenclatura de 180 FBX de Drive, **no he hecho retargeting visual en Unity** ni ejecutado build/test en este entorno.
- No se ha modificado ni reimportado ningún FBX de Drive.
- No se han tocado escena artística, proyecto local, controladores con GUID ni los cambios pendientes de la sesión de VFX/muertes.
- El commit de esta herramienta en 018 es **aditivo**; si Claude trabaja en la misma carpeta, primero guardar/commit sus cambios locales y después integrar el remoto mediante merge/rebase seguro, **nunca hard reset**.
- Mixamo permite usar personajes y animaciones en videojuegos bajo sus condiciones, pero no publicar un catálogo bruto de clips como paquete independiente. Referencia: https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html.
