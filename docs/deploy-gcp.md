# Deploy de teste no Google Cloud (Compute Engine VM)

Sobe a stack numa única VM do Compute Engine, usando o
[`docker-compose.deploy.yml`](../docker-compose.deploy.yml) + Caddy + sslip.io
(ver [deploy-sslip.md](./deploy-sslip.md) para detalhes da stack).

> Rode os comandos `gcloud` no **Cloud Shell** (console GCP → ícone do terminal),
> que já vem autenticado no seu projeto.

## 1. Variáveis (ajuste e cole no Cloud Shell)
```bash
export PROJECT_ID="SEU_PROJECT_ID"
export REGION="southamerica-east1"      # São Paulo
export ZONE="southamerica-east1-a"
export VM="horafy-test"

gcloud config set project "$PROJECT_ID"
gcloud services enable compute.googleapis.com
```

## 2. IP estático (mantém o hostname sslip.io estável)
```bash
gcloud compute addresses create horafy-ip --region "$REGION"
export IP=$(gcloud compute addresses describe horafy-ip --region "$REGION" --format='value(address)')
echo "IP estático: $IP"
```

## 3. Firewall (libera 80/443)
```bash
gcloud compute firewall-rules create horafy-web \
  --allow=tcp:80,tcp:443 --target-tags=horafy --direction=INGRESS
```

## 4. Criar a VM (Ubuntu 22.04, 2 vCPU / 8 GB — folga p/ o build)
```bash
gcloud compute instances create "$VM" \
  --zone="$ZONE" \
  --machine-type=e2-standard-2 \
  --image-family=ubuntu-2204-lts --image-project=ubuntu-os-cloud \
  --boot-disk-size=30GB \
  --address=horafy-ip \
  --tags=horafy
```
> `e2-standard-2` dá conta do build do Next.js + .NET. Para economizar, pode usar
> `e2-medium` (4 GB) **com swap** (passo 5.1). A VM pode ser **parada** quando não
> estiver testando (`gcloud compute instances stop "$VM" --zone "$ZONE"`).

## 5. Acessar a VM e instalar o Docker
```bash
gcloud compute ssh "$VM" --zone "$ZONE"
```
Dentro da VM:
```bash
# (5.1 — opcional, só se usar e2-medium) swap de 2 GB para o build não estourar
# sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile

curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker "$USER"
newgrp docker             # aplica o grupo docker na sessão atual
docker compose version
```

## 6. Obter o código (repo privado → use um PAT do GitHub)
Crie um **fine-grained PAT** (GitHub → Settings → Developer settings → Tokens),
somente leitura no repositório `horatec-jel`. Na VM:
```bash
git clone https://USUARIO:SEU_PAT@github.com/JoseDuDev/horatec-jel.git
cd horatec-jel
```

## 7. Configurar o ambiente
O IP estático vira o hostname sslip.io (com **traços**). Ex.: `34.95.1.2` → `34-95-1-2`.
```bash
cp .env.deploy.example .env
nano .env
```
Preencha (troque `<IP-COM-TRACOS>` pelo IP do passo 2 com traços):
```
APP_HOST=<IP-COM-TRACOS>.sslip.io
API_HOST=api.<IP-COM-TRACOS>.sslip.io
APP_PUBLIC_URL=https://<IP-COM-TRACOS>.sslip.io
API_PUBLIC_URL=https://api.<IP-COM-TRACOS>.sslip.io
ACME_EMAIL=voce@exemplo.com
POSTGRES_PASSWORD=...        # senhas fortes
REDIS_PASSWORD=...
RABBITMQ_PASSWORD=...
JWT_SECRET=...               # >= 32 caracteres
```

## 8. Subir
```bash
docker compose -f docker-compose.deploy.yml up -d --build
docker compose -f docker-compose.deploy.yml logs -f caddy   # acompanhe a emissão do TLS
```
Acesse: `https://<IP-COM-TRACOS>.sslip.io` (frontend) e
`https://api.<IP-COM-TRACOS>.sslip.io/health` (API). Crie o primeiro tenant
conforme [deploy-sslip.md](./deploy-sslip.md#passos).

## 9. Custo e limpeza
- **Parar** a VM quando não usar: `gcloud compute instances stop "$VM" --zone "$ZONE"`.
- **Remover tudo** ao terminar o teste:
  ```bash
  gcloud compute instances delete "$VM" --zone "$ZONE" -q
  gcloud compute addresses delete horafy-ip --region "$REGION" -q
  gcloud compute firewall-rules delete horafy-web -q
  ```
> IP estático **reservado e não usado** é cobrado — libere-o ao deletar a VM.
