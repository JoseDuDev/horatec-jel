# Deploy de teste — docker-compose + sslip.io + Caddy (HTTPS automático)

Sobe a stack completa (Postgres, Redis, RabbitMQ, API .NET, frontend Next.js)
num único host, com **hostnames via [sslip.io](https://sslip.io)** e **TLS automático**
pelo Caddy/Let's Encrypt — sem registrar domínio.

> Ambiente de **teste**: usa o gateway de pagamento *fake* (`PAYMENT_GATEWAY=fake`),
> auto-migra o banco na subida e não exige SMTP/WhatsApp.

## Pré-requisitos no host
- IP público e **portas 80 e 443 abertas** (Let's Encrypt usa a 80 no desafio HTTP-01).
- **Docker** e **Docker Compose v2** instalados.
- Este repositório no host.

## Como o sslip.io resolve
`<IP>.sslip.io` (e `qualquer.label.<IP>.sslip.io`) resolve para `<IP>`. Use o IP com
**traços** para evitar ambiguidade. Ex.: para o IP `203.0.113.5`:
- Frontend: `203-0-113-5.sslip.io`
- API: `api.203-0-113-5.sslip.io`

## Passos

1. **Configurar o ambiente**
   ```bash
   cp .env.deploy.example .env
   # edite .env: troque <IP> pelos hostnames sslip.io, defina senhas, JWT e ACME_EMAIL
   ```
   Variáveis-chave:
   - `APP_HOST` / `API_HOST` — hostnames sslip.io (sem `https://`).
   - `APP_PUBLIC_URL` / `API_PUBLIC_URL` — com `https://` (a `API_PUBLIC_URL` é **assada
     no build do frontend**).
   - `POSTGRES_PASSWORD`, `REDIS_PASSWORD`, `RABBITMQ_PASSWORD`, `JWT_SECRET` (≥ 32 chars),
     `ACME_EMAIL`.

2. **Subir a stack** (build + up; o frontend é assado com a URL pública da API)
   ```bash
   docker compose -f docker-compose.deploy.yml up -d --build
   ```

3. **Acompanhar a subida** (a API auto-migra; o Caddy emite o certificado)
   ```bash
   docker compose -f docker-compose.deploy.yml ps
   docker compose -f docker-compose.deploy.yml logs -f caddy   # veja a emissão do TLS
   ```

4. **Acessar**
   - Frontend: `https://<IP>.sslip.io`
   - API (health): `https://api.<IP>.sslip.io/health`

5. **Criar um tenant para começar** (o schema do tenant é criado automaticamente)
   ```bash
   curl -X POST https://api.<IP>.sslip.io/api/v1/platform/tenants \
     -H "Content-Type: application/json" \
     -d '{"name":"Demo","slug":"demo","vertical":"Barbershop",
          "ownerName":"Owner","ownerEmail":"owner@demo.test","ownerPassword":"Demo1234"}'
   ```
   Portal do tenant: `https://<IP>.sslip.io/demo` · Admin: `https://<IP>.sslip.io/admin`.

## Operação
- **Logs de um serviço:** `docker compose -f docker-compose.deploy.yml logs -f api`
- **Atualizar após mudanças:** `docker compose -f docker-compose.deploy.yml up -d --build`
- **Derrubar (mantém os dados):** `docker compose -f docker-compose.deploy.yml down`
- **Derrubar e apagar dados:** `docker compose -f docker-compose.deploy.yml down -v`
- **Dados persistem** nos volumes `pgdata`, `redisdata`, `rabbitmqdata`, `caddydata`.

## Observações e limitações (é um ambiente de teste)
- **Pagamentos** usam o gateway *fake* — nenhuma cobrança real. Aprove pagamentos
  via webhook fake conforme os specs E2E (`{ type: payment, data: { id: "fake-<bookingId>" } }`).
- **Notificações** (WhatsApp/e-mail) ficam inativas sem `Evolution`/`SMTP` configurados;
  o app funciona normalmente sem elas.
- **Let's Encrypt** tem limite de emissão por semana — evite recriar o `caddydata`
  repetidamente (ele guarda os certificados).
- **sslip.io** é ótimo para teste/demo; para produção, use um domínio próprio (basta
  trocar `APP_HOST`/`API_HOST`/URLs no `.env`).
- Se trocar o IP do host, **atualize o `.env`** e rebuilde o frontend
  (`NEXT_PUBLIC_API_URL`/`API_PUBLIC_URL` são assados no build).
