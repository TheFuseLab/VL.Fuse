# Follow-up: generic weighted sampling

Deferred by user request. Current delivery is the dedicated AssetAliasTable node; do not refactor the production triangle builder as part of that change.

Later consider a reusable BuildAliasTable (weights -> Prob/Alias/Count/Valid) and Fuse SampleAliasTable (buffers + Offset/Count + independent random values -> selected local index). Domain helpers would calculate triangle areas or instance surface-area * uniform-scale-squared weights. Preserve existing nodes and their zero-weight behavior through compatibility wrappers.

Resolve empty/all-zero inputs, negative/nonfinite rejection, numeric precision, packed-table offsets, CPU/GPU parity and caching. For projector deployment preserve stable instance identity and particle seeds; per-view culling must not redistribute the shared scene's particles. Separately consider instance-based weights when repeated placements have different scales. No scheduled automation or separate task has been created.
