#!/bin/bash
set -euo pipefail

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

# ── 2. Run Nextflow pipeline ──────────────────────────────────────────────────
# All tools (eagle, minimac4, bcftools, etc.) are installed locally in this
# image, so Docker is disabled — no Docker-in-Docker needed.
cat > /tmp/override.config <<'EOF'
docker.enabled = false
singularity.enabled = false
EOF

nextflow run /app/main.nf \
    --project "$JOB_ID" \
    --files "/input/*.vcf.gz" \
    --refpanel_yaml "$CONFIG_PATH" \
    --output /output \
    -c /tmp/override.config

echo "[entrypoint] Nextflow pipeline completed"

# ── 3. Upload results to Azure Blob via SAS URL ───────────────────────────────
echo "[entrypoint] Uploading results to blob storage"
azcopy copy "/output/*" "$OUTPUT_SAS_URL" --recursive

echo "[entrypoint] Upload complete"

# ── 4. Notify Tyr that the job is done ───────────────────────────────────────
echo "[entrypoint] Sending callback to $CALLBACK_URL"
curl -s -X POST "$CALLBACK_URL" \
    -H "Content-Type: application/json" \
    -d "{\"jobId\":\"$JOB_ID\",\"status\":\"success\"}"

echo "[entrypoint] Done"
