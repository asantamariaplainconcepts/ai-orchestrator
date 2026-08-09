# The image a Run would execute in on this substrate (H1).
#
# Deliberately the same contents the sbx path needs, so the comparison is between substrates and
# not between images: a Node runtime, git, and the agent CLI — pinned to the same version this
# product pins everywhere else, because a Run and a conversation that answered from different CLI
# versions would disagree about the same repository.
#
# curl and ca-certificates are not decoration: the egress proxy is the interesting part of this
# spike, and a sandbox with no way to make an outbound request cannot demonstrate being denied one.
FROM node:22-bookworm-slim

RUN apt-get update \
 && apt-get install -y --no-install-recommends git ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*

# Same pin as AgentRuntimeRemedies.InstallOpenCode.
RUN npm install -g opencode-ai@1.18.6

# A place for the workspace to land, whichever of H2's three mechanisms puts it there.
WORKDIR /workspace

# No ENTRYPOINT on purpose: the platform's exec is the entry point, and an image that starts a
# process of its own would be answering a question this spike is trying to ask.
CMD ["sleep", "infinity"]
