# Phase template reference

`PhaseTemplateBody.scl` contains the unmodified body of the user's supplied
`phase_template.scl` reference, reviewed with five screenshots on 2026-10-23.
Original reference SHA256:
`A3D715DD355E7C0D11520E6DF78CB35789B47112C8C1A4DF23D021A0EE91E401`.

The test compares it independently with the repository's tokenized EmptyPhase
template and the actual runtime renderer in both selected languages/staging modes.
Whitespace and keyword case are ignored in the source-text comparison. The
runtime XML comparison is exact for all source tokens, formatting and comments.
Do not regenerate this reference from the implementation to make a test pass.

Phases remain SCL in both modes by the user's explicit instruction. The original
control flow, comments and inactive editing placeholders are part of the contract.
