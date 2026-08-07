# H2 — proves the custom-template mechanism: the sandbox runs OUR image, not the stock one.
# The real adoption image would pin the runtime CLIs the way the worker image pins them
# (DEC-012); for the spike a verifiable marker plus one added tool is the proof.
FROM docker/sandbox-templates:claude-code

USER root
RUN echo "spike-sbx-sandbox template v1 (2026-08-07)" > /etc/spike-marker \
    && apt-get update && apt-get install -y --no-install-recommends jq \
    && rm -rf /var/lib/apt/lists/*
