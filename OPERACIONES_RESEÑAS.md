# Sistema de operaciones y reseñas de VentaMap

## Objetivo

Crear un historial verificable para ventas, alquileres, temporarios, permutas y otras modalidades. Las personas que participan pueden dejar una reseña de entre 1 y 5 estrellas con un comentario, reduciendo opiniones falsas y respuestas por represalia.

## Vocabulario

- `Operacion`: acuerdo concretado a partir de un anuncio.
- `Anunciante`: usuario que publico el anuncio.
- `Otra persona`: expresion visible para quien concreto la operacion con el anunciante.
- `Contraparte`: nombre tecnico interno de la otra persona.
- `Reseña`: estrellas y comentario sobre la experiencia.
- `Operacion verificada`: operacion reconocida por la contraparte mediante el enlace seguro.

## Flujo de cierre

1. El anunciante elige `Operacion concretada` al dar de baja el anuncio.
2. VentaMap pregunta `¿Con quien concretaste la operacion?` y solicita:
   - usuario registrado;
   - persona no registrada;
   - email de la otra persona.
3. Si el email pertenece a una cuenta existente, la operacion queda vinculada a esa cuenta. Nadie puede aparecer como contraparte de su propio anuncio.
4. La otra persona recibe un email con un enlace seguro para responder.
5. Puede indicar:
   - `Si, concrete esta operacion`;
   - `La operacion existio, pero hubo un problema`;
   - `No participe en esta operacion`.
6. Confirmar solo significa que la operacion existio. No implica estar conforme.
7. Una persona no registrada debe crear una cuenta con el mismo email para dejar una reseña. La operacion se vincula entonces a su cuenta.

## Reseñas y publicacion diferida

- Cada participante responde sin ver la reseña de la otra persona.
- Puede elegir entre 1 y 5 estrellas, escribir un comentario o seleccionar `No deseo dejar una reseña`.
- Cuando ambos responden comienza una espera de 7 dias.
- Al finalizar se publican simultaneamente las reseñas existentes.
- Declinar la reseña cuenta como respuesta final y no bloquea a la otra persona.
- Si alguien no responde, se aplica un plazo configurable de 14 dias y luego comienza la espera de publicacion.

## Controles contra abuso

- Una respuesta por usuario y operacion.
- Nadie puede reseñarse a si mismo.
- Se conservan anuncio, anunciante, contraparte, email y fechas.
- Tras la confirmacion no se puede sustituir a la contraparte.
- Solo las reseñas de operaciones confirmadas se muestran como verificadas.
- Los comentarios se renderizan como texto, pueden denunciarse y moderarse.
- Los datos se conservan aunque la funcionalidad se desactive.

## Presentacion

En el detalle del anuncio:

`★ 4,7 · 23 reseñas verificadas`

Estado sin historial:

`☆ Aun no tiene reseñas`

Al pasar el mouse, enfocar con teclado o tocar el indicador se abre un popover con los ultimos 10 comentarios publicados del anunciante. Cada entrada muestra estrellas, comentario, fecha y `Operacion verificada`. El panel tiene altura maxima, scroll y se cierra con `Escape` o al tocar fuera.

La pantalla personal se llama `Mis operaciones y reseñas`. Los roles se explican con `Publicaste este anuncio` y `Respondiste a este anuncio`.

## Aviso al confirmar

> Confirmar una operacion crea un registro permanente entre las personas involucradas. Este historial permite construir confianza y proteger a futuros usuarios. Confirma unicamente operaciones que realmente hayan ocurrido. Confirmar no significa que estes conforme: luego podras informar un problema y dejar una reseña sobre la experiencia.

## Parametros

La tabla `VentaMapParameters` administra la funcionalidad:

- `Reviews.Enabled`
- `Reviews.Advertiser.Enabled`
- `Reviews.Counterparty.Enabled`
- `Reviews.EmailNotifications.Enabled`
- `Reviews.DisplayExisting.Enabled`
- `Reviews.PublicationDelayDays` (valor inicial: `7`)
- `Reviews.ResponseDeadlineDays` (valor inicial: `14`)

Los parámetros se validan en el servidor. Desactivar una opción impide nuevas acciones de ese tipo, pero no elimina operaciones ni reseñas existentes.
