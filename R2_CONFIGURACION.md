# Cloudflare R2 en VentaMap

VentaMap sube las imágenes procesadas al bucket de Cloudflare R2 por el endpoint S3-compatible y luego guarda en la publicación la URL pública final.

## Configuración en Cloudflare

1. Creá un bucket en R2.
2. Generá un `Access Key ID` y `Secret Access Key` para acceso S3.
3. Definí una URL pública para lectura:
   - recomendable: un dominio propio apuntado al bucket;
   - alternativa: el dominio público de R2.
4. Verificá que la URL pública final permita leer objetos directamente.

## Configuración en la app

Completar `Cloudflare:R2` en `appsettings.json` o en variables de entorno:

```json
"Cloudflare": {
  "R2": {
    "AccountId": "tu-account-id",
    "AccessKeyId": "tu-access-key",
    "SecretAccessKey": "tu-secret-key",
    "Bucket": "ventamap-publicaciones",
    "PublicBaseUrl": "https://img.ventamap.com.ar",
    "Prefix": "publications",
    "Region": "auto",
    "MaxImageSide": 1600,
    "WebpQuality": 82,
    "WatermarkScale": 0.14,
    "WatermarkOpacity": 0.45
  }
}
```

`ServiceUrl` es opcional si completas `AccountId`. En ese caso la app usa automaticamente:

```text
https://TU_ACCOUNT_ID.r2.cloudflarestorage.com
```

## Qué hace la subida

- Convierte la imagen a `WebP`.
- Redimensiona respetando proporción hasta `MaxImageSide`.
- Aplica marca de agua con `wwwroot/images/logo4.png` en la esquina inferior derecha.
- Sube el archivo ya optimizado a R2.

## Variables de entorno

- `Cloudflare__R2__AccountId`
- `Cloudflare__R2__ServiceUrl`
- `Cloudflare__R2__AccessKeyId`
- `Cloudflare__R2__SecretAccessKey`
- `Cloudflare__R2__Bucket`
- `Cloudflare__R2__PublicBaseUrl`
- `Cloudflare__R2__Prefix`
- `Cloudflare__R2__Region`
- `Cloudflare__R2__MaxImageSide`
- `Cloudflare__R2__WebpQuality`
- `Cloudflare__R2__WatermarkScale`
- `Cloudflare__R2__WatermarkOpacity`
