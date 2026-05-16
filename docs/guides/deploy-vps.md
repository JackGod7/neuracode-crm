# Deploy VPS — Neuracode CRM

Guía para desplegar una instancia del CRM para un cliente nuevo.
Stack: docker-compose (Next.js + .NET API + SQLite en volumen compartido).

---

## Prerequisitos

- VPS Ubuntu 22.04+ (mínimo 1GB RAM — DigitalOcean $6/mes, región São Paulo)
- Dominio o subdominio apuntando al VPS (ej: `crm-joyeria.neuracode.pe`)
- Número WhatsApp del cliente registrado en Meta API (chip físico, no virtual)
- Credenciales Meta del cliente (ver sección Credenciales)

---

## 1 — Preparar el VPS

```bash
# Instalar Docker
curl -fsSL https://get.docker.com | sh
apt-get install -y docker-compose-plugin git nginx certbot python3-certbot-nginx

# Clonar repo
git clone https://github.com/TU_ORG/neuracode-crm /opt/crm-CLIENTE
cd /opt/crm-CLIENTE
mkdir -p data
```

---

## 2 — Crear `.env`

```bash
cp .env.example .env
nano .env
```

Llenar obligatoriamente:

```env
META_PHONE_NUMBER_ID=    # ID del número en Meta (no el número en sí)
META_WABA_ID=            # WhatsApp Business Account ID
META_ACCESS_TOKEN=       # Token de sistema permanente
META_APP_SECRET=         # Para validar firma X-Hub-Signature-256
META_VERIFY_TOKEN=       # Token propio, ej: crm-joyeria-2026
```

---

## 3 — Levantar servicios

```bash
./deploy.sh crm-CLIENTE
# O manualmente:
docker compose up -d --build
```

Verificar:
```bash
curl http://localhost:5000/healthz     # → {"status":"ok"}
curl -I http://localhost:3000          # → 200 OK
```

---

## 4 — nginx + SSL

```nginx
# /etc/nginx/sites-available/crm-joyeria
server {
    server_name crm-joyeria.neuracode.pe;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
ln -s /etc/nginx/sites-available/crm-joyeria /etc/nginx/sites-enabled/
nginx -t && systemctl reload nginx
certbot --nginx -d crm-joyeria.neuracode.pe
```

---

## 5 — Registrar webhook en Meta

1. Meta for Developers → tu app → WhatsApp → Configuration
2. **Callback URL**: `https://crm-joyeria.neuracode.pe/api/webhooks/whatsapp`
3. **Verify Token**: el valor de `META_VERIFY_TOKEN` en `.env`
4. Suscribir a: `messages`, `message_template_status_update`

---

## 6 — Crear templates bajo WABA del cliente

```bash
# Usar la API de Meta con el token del cliente
# Ver: docs/specs/whatsapp-meta/spec.md §Templates
# Plantillas: crm_bienvenida, crm_seguimiento, crm_propuesta_lista, crm_recordatorio_cita
# Aprobación: 24-48h
```

---

## Credenciales Meta — cómo obtenerlas

El cliente necesita:
1. Cuenta Meta Business → `business.facebook.com`
2. App tipo Business → `developers.facebook.com`
3. Número en WhatsApp Platform (chip físico, no la app)
4. Token de sistema permanente → Meta Business Manager → System Users

Resultado: `PHONE_NUMBER_ID`, `WABA_ID`, `APP_SECRET`, `ACCESS_TOKEN`

---

## Actualizaciones futuras

```bash
cd /opt/crm-CLIENTE
git pull
docker compose up -d --build
```

---

## Costo estimado por cliente

| Ítem | Costo |
|------|-------|
| VPS DigitalOcean São Paulo | $6/mes |
| Meta API (200 msgs/día, mayoría en ventana 24h) | $0-5/mes |
| Dominio (subdominio de Neuracode) | $0 |
| **Total infra** | **~$10/mes** |
