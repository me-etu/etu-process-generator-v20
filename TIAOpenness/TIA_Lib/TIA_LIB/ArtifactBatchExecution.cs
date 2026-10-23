using System;
using System.Collections.Generic;

namespace TIA_LIB.Generation
{
    public static class ArtifactBatchExecution
    {
        public static void Execute(IEnumerable<ArtifactDependency> artifacts, Action preflight, Action<string> import)
        {
            if (preflight == null || import == null) throw new ArgumentNullException("Batch execution requires preflight and import actions.");
            var ordered = ArtifactSchedule.Order(artifacts);
            preflight();
            // Exceptions deliberately escape. A failed prerequisite never falls through
            // to any later caller (or another unrelated import) in this batch.
            foreach (var artifact in ordered) import(artifact.Identity);
        }
    }
}
