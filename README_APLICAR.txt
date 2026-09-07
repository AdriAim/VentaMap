PARCHE LOCAL — BÚSQUEDA AJAX + SCROLL + ALTURA DEL MAPA

Copiar el contenido de este ZIP sobre la raíz del proyecto VentaMap.

Archivos incluidos:
- Pages/Browse.cshtml
- Views/Content/Browse.cshtml
- wwwroot/js/browse-ajax.js
- wwwroot/css/browse.css

Qué cambia:
1. Buscar actualiza resultados vía fetch/AJAX, sin refrescar toda la página.
2. La paginación y los botones 50/100/200 también funcionan vía AJAX.
3. La URL se mantiene actualizada mediante history.pushState.
4. Atrás y Adelante del navegador vuelven a cargar los resultados sin recargar.
5. Scroll automático:
   - Mapa: lleva el mapa arriba.
   - Texto: lleva el primer clasificado arriba.
   - Galería: lleva la primera foto/feed arriba.
6. En escritorio, la altura del mapa se sincroniza con el panel de la publicación seleccionada.
7. Se fuerza el resize del mapa después del cambio de altura.

IMPORTANTE:
- Hacé una copia de seguridad de los archivos antes de reemplazarlos.
- Este parche no toca Git ni crea commits.
- Si tu Views/Content/Browse.cshtml tiene cambios posteriores, comparalo antes de reemplazarlo.
