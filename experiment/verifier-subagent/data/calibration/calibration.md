# Calibration

One runner pass per cell, no agent involved. Tokens are bytes/4 estimates.
Cut zone assumes a 30,000-character cap keeping an equal head and tail (unverified; probe P1).

| scenario | verbosity | status | A: raw bytes (~tok) | B: runner stdout + summary | D: summary | raw / summary | failure lines (unique tests) | first failure at | failure lines outside cut zone |
|---|---|---|---|---|---|---|---|---|---|
| G | quiet | passed | 33,313 (~8,328) | 2,074 | 1,093 | 30.5x | 0 (0) | - | - |
| G | normal | passed | 144,138 (~36,034) | 2,074 | 1,093 | 131.9x | 0 (0) | - | - |
| G | detailed | passed | 144,503 (~36,125) | 2,074 | 1,093 | 132.2x | 0 (0) | - | - |
| R1 | quiet | failed | 29,666 (~7,416) | 1,809 | 957 | 31.0x | 2 (1) | 27,551 (93%) | 2 of 2 |
| R1 | normal | failed | 97,591 (~24,397) | 1,809 | 957 | 102.0x | 3 (2) | 67,236 (69%) | 0 of 3 |
| R1 | detailed | failed | 95,388 (~23,847) | 1,809 | 957 | 99.7x | 2 (1) | 47,836 (50%) | 0 of 2 |
| R3 | quiet | failed | 131,532 (~32,883) | 1,809 | 957 | 137.4x | 78 (39) | 27,652 (21%) | 11 of 78 |
| R3 | normal | failed | 286,437 (~71,609) | 1,809 | 957 | 299.3x | 78 (39) | 34,733 (12%) | 0 of 78 |
| R3 | detailed | failed | 286,688 (~71,672) | 1,809 | 957 | 299.6x | 77 (39) | 33,024 (12%) | 0 of 77 |
