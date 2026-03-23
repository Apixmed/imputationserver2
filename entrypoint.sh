#!/bin/bash
set -euxo pipefail

trap 'echo "[entrypoint] ERROR at line $LINENO: command exited with code $?"' ERR

echo "[entrypoint] Starting imputation job: $JOB_ID"

# ── 1. Download input VCFs from Blob SAS URLs ────────────────────────────────
mkdir -p /input

IFS=',' read -ra URLS <<< "$INPUT_FILES_URLS"
for url in "${URLS[@]}"; do
    # Strip SAS query string to get the clean filename
    filename=$(basename "${url%%\?*}")
    echo "[entrypoint] Downloading $filename"
    wget -q -O "/input/$filename" "$url"
done

echo "[entrypoint] Downloaded ${#URLS[@]} file(s) to /input"

# ── 2. Download reference panel from Blob SAS URL ────────────────────────────
echo "[entrypoint] Downloading reference panel"
mkdir -p /refpanels
azcopy copy "$REF_PANEL_SAS_URL" /refpanels/ --recursive
echo "[entrypoint] Reference panel downloaded to /refpanels"

# ── 3. Run Nextflow pipeline ──────────────────────────────────────────────────
# All tools (eagle, minimac4, bcftools, etc.) are installed locally in this
# image, so Docker is disabled — no Docker-in-Docker needed.
cat > /tmp/override.config <<'EOF'
docker.enabled = false
singularity.enabled = false
EOF

echo "[entrypoint] Output files before pipeline:"
ls -la /output/ 2>/dev/null || echo "(none)"

nextflow run /app/main.nf \
    --project "$JOB_ID" \
    --files "/input/*.vcf.gz" \
    --refpanel_yaml "/refpanels/refpanel.yaml" \
    --output /output \
    -c "/app/$CONFIG_PATH" \
    -c /tmp/override.config

echo "[entrypoint] Nextflow pipeline completed"

echo "[entrypoint] Output files after pipeline:"
ls -la /output/ 2>/dev/null || echo "(none)"

# ── 4. Upload results to Azure Blob via SAS URL ───────────────────────────────
echo "[entrypoint] Uploading results to blob storage"
azcopy copy "/output/*" "$OUTPUT_SAS_URL" --recursive

echo "[entrypoint] Upload complete"

# ── 5. Notify that the job is done ───────────────────────────────────────
echo "[entrypoint] Sending callback to $CALLBACK_URL"
curl -s -X POST "$CALLBACK_URL" \
    -H "Content-Type: application/json" \
    -d "{\"jobId\":\"$JOB_ID\",\"status\":\"success\"}"

echo "[entrypoint] Done"
