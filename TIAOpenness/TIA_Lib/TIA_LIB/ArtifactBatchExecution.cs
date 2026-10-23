using System;
using System.Collections.Generic;

namespace TIA_LIB.Generation
{
    public static class ArtifactBatchExecution
    {
        public static void PublishWithInstances(Action checkDefinition, IEnumerable<Action> prepareInstances, Action compileAndPublish)
        {
            if (checkDefinition == null || prepareInstances == null || compileAndPublish == null)
                throw new ArgumentNullException("Publishing requires definition, companion instances and final validation actions.");
            checkDefinition();
            foreach (var prepare in prepareInstances) prepare();
            // Global instance DBs belong to their FB definition. Complete this pair
            // before requiring export consistency or exposing it to caller imports.
            compileAndPublish();
        }
        public static void CompileUntilConsistent(Action compile, Func<bool> isConsistent, string identity, Action compileDependencies = null)
        {
            if (compile == null || isConsistent == null) throw new ArgumentNullException("Compilation requires a compiler and consistency check.");
            compile();
            if (isConsistent()) return;
            // Updating FBD call interfaces can require a subsequent compile pass.
            // Never export/mark ownership or continue callers while inconsistent.
            compile();
            if (isConsistent()) return;
            // A block-only incremental compile can report up-to-date while its
            // dependency consistency still prevents native XML export. Reconcile
            // the PLC software once, then require the actual flag again.
            if (compileDependencies != null)
            {
                compileDependencies();
                if (isConsistent()) return;
            }
            throw new InvalidOperationException("PLC block '" + identity + "' remains inconsistent after two block compile passes" +
                (compileDependencies == null ? "" : " and PLC software compilation") + ". Review retained compiler diagnostics before importing callers.");
        }
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
