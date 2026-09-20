#!/usr/bin/env bash
set -Eeuo pipefail

SOURCE_DIR="/opt/resellmanager/source"
DATA_DIR="/opt/resellmanager/data"
BACKUP_DIR="/opt/resellmanager/backups"
HEALTH_URL="https://app.resellmanager.tech/health"
SERVICE="resellmanager"
BACKUP_OWNER="reselladmin"

if (( EUID != 0 )); then
  echo "Ejecuta este script con sudo: sudo /opt/resellmanager/backup.sh" >&2
  exit 1
fi

timestamp="$(date +%Y%m%d-%H%M%S)"
backup="$BACKUP_DIR/resellmanager-$timestamp.tar.gz"
checksum="$backup.sha256"
stopped=0

log() {
  printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"
}

exec 9>/run/lock/resellmanager-backup.lock
if ! flock -n 9; then
  log "Ya hay un backup de ResellManager en ejecución; no se iniciará otro."
  exit 0
fi

start_app() {
  if (( stopped == 1 )); then
    log "Levantando ResellManager..."
    (cd "$SOURCE_DIR" && docker compose start "$SERVICE")
    stopped=0
  fi
}

on_exit() {
  status=$?
  if (( stopped == 1 )); then
    log "El script terminó antes de tiempo; intentando levantar ResellManager..."
    (cd "$SOURCE_DIR" && docker compose start "$SERVICE") || true
  fi
  exit "$status"
}
trap on_exit EXIT INT TERM

rotate_backups() {
  BACKUP_DIR="$BACKUP_DIR" python3 <<'PY'
from datetime import datetime
from pathlib import Path
import os

root = Path(os.environ["BACKUP_DIR"])
items = []

for path in root.glob("resellmanager-????????-??????.tar.gz"):
    stamp = path.name.removeprefix("resellmanager-").removesuffix(".tar.gz")
    try:
        when = datetime.strptime(stamp, "%Y%m%d-%H%M%S")
    except ValueError:
        continue
    items.append((when, path))

items.sort(key=lambda item: item[0], reverse=True)
keep = set()

# 7 copias recientes (diarias si el timer corre una vez por día).
for _, path in items[:7]:
    keep.add(path)

# Una copia por semana ISO para las 4 semanas más recientes disponibles.
weeks = set()
for when, path in items:
    key = (when.isocalendar().year, when.isocalendar().week)
    if key not in weeks and len(weeks) < 4:
        weeks.add(key)
        keep.add(path)

# Una copia por mes para los 3 meses más recientes disponibles.
months = set()
for when, path in items:
    key = (when.year, when.month)
    if key not in months and len(months) < 3:
        months.add(key)
        keep.add(path)

removed = 0
for _, path in items:
    if path in keep:
        continue
    path.unlink(missing_ok=True)
    Path(str(path) + ".sha256").unlink(missing_ok=True)
    removed += 1

print(f"Retención aplicada: {len(keep)} backups conservados, {removed} eliminados.")
PY
}

mkdir -p "$BACKUP_DIR"

if ! id "$BACKUP_OWNER" >/dev/null 2>&1; then
  log "ERROR: no existe el usuario $BACKUP_OWNER."
  exit 1
fi

log "Deteniendo ResellManager para obtener un snapshot consistente de SQLite..."
(cd "$SOURCE_DIR" && docker compose stop "$SERVICE")
stopped=1

log "Creando backup: $backup"
tar -C "$DATA_DIR"   -czf "$backup"   database comprobantes dataprotection

log "Generando SHA-256..."
(
  cd "$BACKUP_DIR"
  sha256sum "$(basename "$backup")" > "$(basename "$checksum")"
)

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

chown "$BACKUP_OWNER:$BACKUP_OWNER" "$backup" "$checksum"
chmod 600 "$backup" "$checksum"

log "Aplicando retención: 7 recientes + 4 semanales + 3 mensuales..."
rotate_backups

log "Backup completado correctamente."
log "Archivo: $backup"
log "Checksum: $checksum"
log "Tamaño: $(du -h "$backup" | awk '{print $1}')"
