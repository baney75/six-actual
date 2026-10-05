# Six Actual

Real-time tactics project. The contract is `docs/BRIEF.md`. Current status is `docs/STATE.md`.

```bash
scripts/bootstrap.sh
scripts/verify.sh --quick
scripts/verify.sh --phase 0
```

`bootstrap.sh` prints the system package list and does not install those packages. It does install the pinned .NET SDK, Godot editor, and export templates into this repo and the user-local Godot templates folder.
