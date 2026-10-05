# State

- Phase: 0 Foundations
- Verified this session: SIM-01, SIM-02, SIM-03, QA-01, QA-02
- Last verify: pass, 2026-10-05T02:03:55Z, `build/reports/20261005T020355Z-phase0.md`
- Gates in that run: G1, G2, G3, G10, G11, G12, zero failures
- Not verified: a fresh Omarchy account has not run `scripts/bootstrap.sh`. The script is written, and the same install steps were run on this machine.
- Blockers: none for the next sim requirement
- Next:
  1. SIM-04 terrain line of sight, with a failing scenario first.
  2. Keep the 20 Hz tick and fixed-point state. Do not put combat results in the view.
  3. Style board stays unstarted until the slice needs it. That needs Donovan's sign-off.

The exported shell runs and prints a foundation hash. It is not a playable battle.
