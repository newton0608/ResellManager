#!/usr/bin/env bash
set -Eeuo pipefail

SOURCE_DIR="/opt/resellmanager/source"
DATA_DIR="/opt/resellmanager/data"
BACKUP_DIR="/opt/resellmanager/backups"
HEALTH_URL="https://app.resellmanager.tech/health"
SERVICE="resellmanager"

timestamp="$(date +%Y%m%d-%H%M%S)"
backup="$BACKUP_DIR/resellmanager-$timestamp.tar.gz"
checksum="$backup.sha256"
stopped=0

log() {
  printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"
}

start_app() {
  if (( stopped == 1 )); then
    log "Levantando ResellManager..."
    (cd "$SOURCE_DIR" && sudo docker compose start "$SERVICE")
    stopped=0
  fi
}

on_exit() {
  status=$?
  if (( stopped == 1 )); then
    log "El script terminó antes de tiempo; intentando levantar ResellManager..."
    (cd "$SOURCE_DIR" && sudo docker compose start "$SERVICE") || true
  fi
  exit "$status"
}
trap on_exit EXIT INT TERM

mkdir -p "$BACKUP_DIR"

log "Deteniendo ResellManager para obtener un snapshot consistente de SQLite..."
(cd "$SOURCE_DIR" && sudo docker compose stop "$SERVICE")
stopped=1

log "Creando backup: $backup"
sudo tar -C "$DATA_DIR"   -czf "$backup"   database comprobantes dataprotection

log "Generando SHA-256..."
sudo sha256sum "$backup" | sudo tee "$checksum" >/dev/null

start_app

log "Esperando a que /health vuelva a responder..."
healthy=0
for _ in $(seq 1 20); do
  if curl --fail --silent --show-error --max-time 5 "$HEALTH_URL" | grep -qx 'OK'; then
    healthy=1
    break
  fi
  sleep 2
done

if (( healthy != 1 )); then
  log "ERROR: el backup se creó, pero /health no volvió a responder correctamente."
  log "Revisa: cd $SOURCE_DIR && sudo docker compose ps && sudo docker compose logs --tail=100 $SERVICE"
  exit 1
fi

sudo chmod 600 "$backup" "$checksum"

log "Backup completado correctamente."
log "Archivo: $backup"
log "Checksum: $checksum"
log "Tamaño: $(sudo du -h "$backup" | awk '{print $1}')"
