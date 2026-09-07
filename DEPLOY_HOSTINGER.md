# Deploy en Hostinger VPS

Regla operativa vigente al 19 de agosto de 2026:

- Hostinger se actualiza siempre desde el estado local actual.
- No depender de `git pull` ni del remoto para publicar cambios.
- El stack activo en el VPS vive en `/root/ventamap-local`.
- Si `VentaMap.ChatService` no tiene cambios, no incluirlo en el paquete ni reconstruir o desplegar el servicio de chat. Publicar únicamente `VentaMap.Web` y los archivos de infraestructura que hayan cambiado.

## Flujo real de deploy

1. Empaquetar la copia local actual en `E:\Proyectos\ventamap`.
2. Subir el `.tar.gz` al VPS.
3. Respaldar `/root/ventamap-local/.env`.
4. Descomprimir el paquete encima de `/root/ventamap-local`.
5. Ejecutar `docker compose` o `./update-hostinger.sh` dentro del VPS.

## Primera actualización después del cambio de nombre

Los servicios pasaron a llamarse `ventamap-mysql`, `ventamap-web` y `ventamap-chat`.
Los scripts de despliegue detectan la carpeta anterior `/root/ventagram-local`, la mueven a `/root/ventamap-local` y conservan `COMPOSE_PROJECT_NAME=ventagram-local` para reutilizar los volúmenes actuales de MySQL y Data Protection. El despliegue usa `--remove-orphans`, por lo que sustituye los contenedores anteriores sin borrar volúmenes.

La base existente puede conservar los nombres internos históricos. No crear una base vacía ni cambiar sus tablas durante esta transición; cualquier renombrado físico de la base debe hacerse mediante una migración respaldada y verificada por separado.

## Archivos relevantes

- `VentaMap.Web`
- `VentaMap.ChatService`
- `docker-compose.hostinger.yml`
- `.env.hostinger.example`
- `update-hostinger.sh`
- `package-hostinger-local.ps1`

## Empaquetar desde Windows

Desde `E:\Proyectos\ventamap`:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-hostinger-local.ps1
```

Eso genera un archivo tipo:

```text
ventamap-local-deploy-20260819-013500.tar.gz
```

## Subir al VPS

Ejemplo:

```powershell
scp .\ventamap-local-deploy-20260819-013500.tar.gz root@TU_IP:/root/
```

## Desplegar en el VPS

Conectate:

```bash
ssh root@TU_IP
```

Respalda `.env` y descomprime:

```bash
cp /root/ventamap-local/.env /root/ventamap-local/.env.backup
tar -xzf /root/ventamap-local-deploy-20260819-013500.tar.gz -C /root/ventamap-local
```

Deploy normal:

```bash
cd /root/ventamap-local
chmod +x update-hostinger.sh
./update-hostinger.sh
```

Deploy con migraciones y seeds:

```bash
cd /root/ventamap-local
./update-hostinger.sh --with-db
```

## Qué hace `update-hostinger.sh`

- usa el contenido local ya copiado en `/root/ventamap-local`
- activa migraciones y `SeedData` solo si pasas `--with-db`
- ejecuta `docker compose -f docker-compose.hostinger.yml up -d --build`
- deja los flags otra vez en `false` al salir

## Variables importantes de `.env`

- `MYSQL_ROOT_PASSWORD`
- `COMPOSE_PROJECT_NAME=ventagram-local` durante la transición de volúmenes
- `VENTAMAP_WEB_BASE_URL`
- `VENTAMAP_WEB_BASE_URL_ALT`
- `VENTAMAP_CHAT_BASE_URL`
- `SMTP_HOST`
- `SMTP_USER`
- `SMTP_PASSWORD`
- `SMTP_FROM_EMAIL`
- `MAP_STYLE_URL` o `MAP_TILES_URL_TEMPLATE`
- `R2_ACCOUNT_ID` o `R2_SERVICE_URL`
- `R2_ACCESS_KEY_ID`
- `R2_SECRET_ACCESS_KEY`
- `R2_BUCKET`
- `R2_PUBLIC_BASE_URL`

Opcionales:

- `DATABASE_APPLY_MIGRATIONS_ON_STARTUP`
- `DATABASE_RUN_SEED_DATA_ON_STARTUP`
- `MAP_GEOCODING_SEARCH_URL_TEMPLATE`
- `MAP_REVERSE_GEOCODING_URL_TEMPLATE`
- `SMTP_CONTACT_RECIPIENT`

## Verificación

```bash
cd /root/ventamap-local
docker compose -f docker-compose.hostinger.yml ps
docker compose -f docker-compose.hostinger.yml logs --tail 100 ventamap-web
docker compose -f docker-compose.hostinger.yml logs --tail 100 ventamap-chat
curl -I http://127.0.0.1:8080
curl -I http://127.0.0.1:8081
```

## Notas

- MySQL queda publicado solo en `127.0.0.1:3306`.
- `DataProtection` queda compartido entre web y chat.
- Si la web falla por historial EF desalineado, revisar primero logs y estado de `__EFMigrationsHistory` antes de insistir con rebuild.
