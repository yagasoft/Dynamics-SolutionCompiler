# DBM SDK Registration

This optional DBM-specific example captures a source-first SDK-message registration pattern from code.

Use it for:
- studying how a project can resolve `sdkmessage` and `sdkmessagefilter` context before creating or updating `sdkmessageprocessingstep` rows
- seeing how step images are registered from code rather than inferred from solution XML alone
- pairing the DBM plugin assembly baseline with the code-level registration layer that drives runtime extensibility

What it includes:
- a generated `inventory.md`
- a generated `inventory.json`

Important limits:
- this is a project-specific example, not a neutral seed
- it is intentionally source-first; it does not claim that the tracked DBM baseline export contains every step or image row
- use it to understand registration shape and reasoning boundaries, not to import DBM assumptions into global guidance
