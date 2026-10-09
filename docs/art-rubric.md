# Art rubric (model review, contract §M5)

Used by `art-reviewer` in a fresh context. Score each criterion 1–5. Every score cites a sheet file and region.

| Criterion | Weight | Sheets | 5 | 3 | 1 |
|---|---|---|---|---|---|
| Silhouette and proportion | 25% | CS-1, CS-2 | Every ortho IoU above threshold (side 0.88, top 0.85, front 0.82); nothing off in CS-2 | IoU passes but one view has a visible proportion error | Wrong scale or a missing major part |
| Signature features | 20% | CS-2, CS-3 | All five present and correctly placed | Four present, or one misplaced | Two or more missing; reads as a generic vehicle |
| Detail density and consistency | 10% | CS-2, CS-11 | Matches CS-11 neighbors; no noisy or empty zones | One zone noticeably sparser or busier | Visibly a different art standard |
| Topology and budget | 10% | CS-4, CS-5 | Clean; within budget with ≥10% headroom | Within budget; minor waste | Over budget, n-gons, holes |
| UV, bake and materials | 15% | CS-6, CS-7 | No artifacts; camo and wear match the art bible | Minor seam or texel variance | Bake errors, stretching, wrong palette |
| LOD and RTS readability | 15% | CS-5, CS-10 | Class and faction identifiable at 200 m; no LOD pop | Class identifiable at 200 m, faction not | Unreadable at 120 m or visible pop |
| Articulation and damage | 5% | CS-8, CS-9 | Correct pivots, no clipping; damage reads | One clipping or pivot issue | Broken pivots; no destroyed state |

**Pass:** weighted score ≥ 4.0 and no criterion below 3. A blockout review scores only silhouette, proportion and signature features (weights renormalized to 25:20). A failed blind recognition test forces signature features to 1.

`review.md` format: a table of scores with evidence, the weighted total, the 3 most important fixes (sheet file plus region), and `APPROVED` or `REJECTED → <stage>`.
