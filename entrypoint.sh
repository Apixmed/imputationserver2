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

# ── 2. Download and extract reference panel .7z archive ──────────────────────
apt-get install -y -q p7zip-full
echo "[entrypoint] Downloading reference panel archive"
mkdir -p /refpanels
refpanel_filename=$(basename "${REF_PANEL_SAS_URL%%\?*}")
wget --no-verbose -O "/tmp/$refpanel_filename" "$REF_PANEL_SAS_URL"
echo "[entrypoint] Extracting reference panel archive"
7z x "/tmp/$refpanel_filename" -o/refpanels -y
rm "/tmp/$refpanel_filename"
refpanel_yaml=$(find /refpanels -name "refpanel.yaml" -type f | head -1)
echo "[entrypoint] Reference panel extracted, refpanel.yaml: $refpanel_yaml"

# ── 3. Run Nextflow pipeline ──────────────────────────────────────────────────
# All tools (eagle, minimac4, bcftools, etc.) are installed locally in this
# image, so Docker is disabled — no Docker-in-Docker needed.
cat > /tmp/override.config <<'EOF'
docker.enabled = false
singularity.enabled = false
EOF

echo "[entrypoint] Output files before pipeline:"
ls -la /output/ 2>/dev/null || echo "(none)"

echo "[entrypoint] Running Nextflow pipeline"
set +e
nextflow run /app/main.nf \
    --project "$JOB_ID" \
    --files "/input/*.vcf.gz" \
    --refpanel_yaml "$refpanel_yaml" \
    --output /output \
    -c "/app/$CONFIG_PATH" \
    -c /tmp/override.config
nextflow_exit=$?
set -e
echo "[entrypoint] Nextflow exited with code: $nextflow_exit"

echo "[entrypoint] Output files after pipeline:"
ls -la /output/ 2>/dev/null || echo "(none)"

if [ $nextflow_exit -ne 0 ]; then
    echo "[entrypoint] Nextflow failed — uploading .nextflow.log to blob storage"
    output_base="${OUTPUT_SAS_URL%%\?*}"
    output_sas="${OUTPUT_SAS_URL#*\?}"
    azcopy copy ".nextflow.log" "${output_base}/${OUTPUT_BLOB_PREFIX}/.nextflow.log?${output_sas}" --log-level INFO || \
        echo "[entrypoint] Failed to upload .nextflow.log"
    exit $nextflow_exit
fi

# ── 4. Upload results to Azure Blob via SAS URL ───────────────────────────────
echo "[entrypoint] Uploading results to blob storage"
azcopy copy "/output/*" "$OUTPUT_SAS_URL" --recursive --log-level INFO
echo "[entrypoint] azcopy exited with code: $?"

echo "[entrypoint] Upload complete"

# ── 5. Notify that the job is done ───────────────────────────────────────
echo "[entrypoint] Sending callback to $CALLBACK_URL"
curl -v -X POST "$CALLBACK_URL" \
    -H "Content-Type: application/json" \
    -d "{\"jobId\":\"$JOB_ID\",\"status\":\"success\"}"

echo "[entrypoint] Done"
