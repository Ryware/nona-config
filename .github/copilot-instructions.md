# Copilot Instructions

## Project Guidelines
- For Nona API memory work, treat high RSS after large full-environment reads as GC heap commitment from excessive response materialization rather than a managed-object leak; prioritize streaming/pagination/bounds for environment reads before GC tuning.