# ST_01 Cimiento erosionado: informe (v005)

**Veredicto:** aprobada en Blender. La importación en Unity está pendiente: no se ha probado.

## Qué se modeló
- Bloque de 2 × 1 × 0,5 m, con chaflán plano de 6 cm en las aristas superiores y verticales. La base es recta.
- 22 desconchones recortados con operaciones booleanas, de tres tamaños, en las aristas y las esquinas.
- Dos grietas recortadas en la geometría: una en la cara superior y otra en la frontal.
- Versión de alta resolución (228.000 triángulos): remallado con voxels de 8 mm y grano por desplazamiento. Sus detalles se pasaron a texturas sobre el modelo de juego.
- Material: textura de piedra procedural con grano fino, musgo en la cara superior, mapa de relieve (normal) y de oclusión (AO), ambos de 1024 px.

## Revisión
| Prueba | Resultado |
|---|---|
| Medidas | 2,0 × 1,0 × 0,5 m, punto de origen en la base (z = 0) |
| Triángulos | 618 (modelo) / 308 (versión simplificada, LOD1) |
| Aristas abiertas / caras deformes | 0 / 0 (corregidas 2 caras deformes en v005) |
| Materiales / UV | 1 / 1 |
| Colisión | caja UCX_ST_01_Cimiento |
| Repetición | 3 módulos en fila, sin huecos en las juntas |

## Pendiente
- Importar el FBX en Unity y comprobar la escala, la colisión y el material. No se ha hecho.
- Sustituir el color procedural por una textura pasada a imagen (albedo), para usarla en Unity.

## Historial
- **v001 a v003:** deformación de vértices; quedaba con aspecto de tela o almohada.
- **v004:** cambio a cortes booleanos y paso del detalle de alta resolución a texturas.
- **v005:** grietas visibles, desconchones variados, grano fino.

Script de generación: `st05.py`, en la carpeta temporal de la sesión.
