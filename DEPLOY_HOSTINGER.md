# Deploy en Hostinger VPS

Regla operativa vigente al 19 de agosto de 2026:

- Hostinger se actualiza siempre desde el estado local actual.
- No depender de `git pull` ni del remoto para publicar cambios.
- El stack activo en el VPS vive en `/root/ventagram-local`.

## Flujo real de deploy

1. Empaquetar la copia local actual en `E:\Proyectos\ventagram`.
2. Subir el `.tar.gz` al VPS.
3. Respaldar `/root/ventagram-local/.env`.
4. Descomprimir el paquete encima de `/root/ventagram-local`.
5. Ejecutar `docker compose` o `./update-hostinger.sh` dentro del VPS.

## Archivos relevantes

- `Ventagram.Web`
- `Ventagram.ChatService`
- `docker-compose.hostinger.yml`
- `.env.hostinger.example`
- `update-hostinger.sh`
- `package-hostinger-local.ps1`

## Empaquetar desde Windows

Desde `E:\Proyectos\ventagram`:

```powershell
powershell -ExecutionPolicy Bypass -File .\package-hostinger-local.ps1
```

Eso genera un archivo tipo:

```text
ventagram-local-deploy-20260819-013500.tar.gz
```

## Subir al VPS

Ejemplo:

```powershell
scp .\ventagram-local-deploy-20260819-013500.tar.gz root@TU_IP:/root/
```

## Desplegar en el VPS

Conectate:

```bash
ssh root@TU_IP
```

Respalda `.env` y descomprime:

```bash
cp /root/ventagram-local/.env /root/ventagram-local/.env.backup
tar -xzf /root/ventagram-local-deploy-20260819-013500.tar.gz -C /root/ventagram-local
```

Deploy normal:

```bash
cd /root/ventagram-local
chmod +x update-hostinger.sh
./update-hostinger.sh
```

Deploy con migraciones y seeds:

```bash
cd /root/ventagram-local
./update-hostinger.sh --with-db
```

## Qué hace `update-hostinger.sh`

- usa el contenido local ya copiado en `/root/ventagram-local`
- activa migraciones y `SeedData` solo si pasas `--with-db`
- ejecuta `docker compose -f docker-compose.hostinger.yml up -d --build`
- deja los flags otra vez en `false` al salir

## Variables importantes de `.env`

- `MYSQL_ROOT_PASSWORD`
- `VENTAGRAM_WEB_BASE_URL`
- `VENTAGRAM_WEB_BASE_URL_ALT`
- `VENTAGRAM_CHAT_BASE_URL`
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
cd /root/ventagram-local
docker compose -f docker-compose.hostinger.yml ps
docker compose -f docker-compose.hostinger.yml logs --tail 100 ventagram-web
docker compose -f docker-compose.hostinger.yml logs --tail 100 ventagram-chat
curl -I http://127.0.0.1:8080
curl -I http://127.0.0.1:8081
```

## Notas

- MySQL queda publicado solo en `127.0.0.1:3306`.
- `DataProtection` queda compartido entre web y chat.
- Si la web falla por historial EF desalineado, revisar primero logs y estado de `__EFMigrationsHistory` antes de insistir con rebuild.
