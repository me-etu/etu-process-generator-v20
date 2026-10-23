using System.Collections.Generic;
using System.Linq;

namespace TIA_LIB.Generation
{
    // New interlocks share G-014's cyclic FALSE starter semantics in both backends.
    // Existing manually edited interlocks must pass ownership/migration policy first.
    public sealed class GeneratedInterlockModel : GeneratedStructuredBody
    {
        public GeneratedInterlockModel(OperationInterlockDescriptor descriptor) : base(CreateRegions(descriptor)) { }
        static IEnumerable<GeneratedRegion> CreateRegions(OperationInterlockDescriptor descriptor)
        {
            if (descriptor.Phases.Count == 0) yield break;
            yield return new GeneratedRegion("Function", new GeneratedStatement[0]);
            yield return Group("General Interlocks", descriptor.BoolMembers.Take(50));
            for (int index = 0; index < descriptor.Phases.Count; index++)
                yield return Group(descriptor.Phases[index] + " Interlocks", descriptor.BoolMembers.Skip(50 + index * 50).Take(50));
        }
        static GeneratedRegion Group(string title, IEnumerable<string> members)
        {
            return new GeneratedRegion(title, members.Select(name => new GeneratedStatement(new GeneratedAssignment(
                GeneratedExpression.Reference(new SymbolReference(false, name), "Bool"), GeneratedExpression.Constant("Bool", "FALSE")), null)));
        }
    }
}
