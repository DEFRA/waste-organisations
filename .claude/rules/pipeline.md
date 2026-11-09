---
paths:
  - ".github/**"
  - "Dockerfile"
  - "compose.yml"
  - "compose/**"
---

Before changing workflows, the Dockerfile or the Compose stack, read and follow `doc/rules/continuous-integration.md` and `doc/rules/containers.md`. Actions stay pinned to a full commit SHA with the version as a comment; images stay pinned by digest; `check-pull-request.yml` and `publish.yml` stay in step.
