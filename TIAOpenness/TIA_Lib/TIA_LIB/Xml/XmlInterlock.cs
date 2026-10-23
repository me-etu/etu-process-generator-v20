using System;
using System.Collections.Generic;
using System.Windows.Documents;
using Siemens.Engineering.SW.Blocks;
using TIA_LIB.Devices;
using TIA_LIB.Xml.Network;


namespace TIA_LIB.Xml
{
    public class XmlInterlock : XmlBlock
    {
        public XmlInterlock(XmlPhase ph): base(ph.Op.Unit.UserGroup, ph.Op.Name + "_Interlocks", ExportFileName(ph), "Xml/EmptyBlock.xml", "Interlocks", false, ph.Op.Name,
            preserveOnly: Generation.ModularGenerationRuntime.Current == null && PlcProject.InterlockLanguage == OperationInterlockLanguage.Scl,
            expectedLanguage: Generation.ModularGenerationRuntime.Current != null ? null : PlcProject.InterlockLanguage == OperationInterlockLanguage.Scl ? "SCL" : "FBD", parentBlock: ph.Op.Unit)
        {
            Operation = ph.Op;
        }

        private readonly XmlOperation Operation;
        private OperationInterlockDescriptor descriptor;
        private OperationInterlockBatch batch;
        internal OperationInterlockDescriptor FinalizedDescriptor()
        {
            return new OperationInterlockDescriptor(Operation.Unit.Name, Operation.Name, Operation.PhaseOrder);
        }

        private static string ExportFileName(XmlPhase ph)
        {
            if (PlcProject.InterlockLanguage == OperationInterlockLanguage.Fbd)
                return ph.Op.Name + "_" + ph.phName + "_Interlocks.xml";
            return new OperationInterlockDescriptor(ph.Op.Unit.Name, ph.Op.Name, new[] { ph.phName }).ArtifactStem + ".xml";
        }

        public static void PrepareAll(IEnumerable<XmlInterlock> interlocks)
        {
            var targets = new Dictionary<OperationInterlockDescriptor, XmlInterlock>();
            foreach (var interlock in interlocks)
            {
                interlock.descriptor = new OperationInterlockDescriptor(interlock.Operation.Unit.Name,
                    interlock.Operation.Name, interlock.Operation.PhaseOrder, PlcProject.InterlockLanguage == OperationInterlockLanguage.Scl);
                targets.Add(interlock.descriptor, interlock);
            }
            foreach (var target in targets)
            {
                foreach (var other in XmlBlock.Blocks)
                    if (!(other is XmlInterlock) && string.Equals(other.Name, target.Key.BlockName, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Requested operation interlock " + target.Key.Identity + "/" + target.Key.BlockName +
                            " conflicts with generated " + other.GetType().Name + " at " + other.FileName + ". Reconcile the CPU-global names explicitly.");
            }
            var prepared = new OperationInterlockBatch(targets.Keys, new OperationInterlockPortalBackend(targets), PlcProject.InterlockLanguage);
            foreach (var interlock in targets.Values) interlock.batch = prepared;
        }

        public override void Upload()
        {
            if (PlcProject.InterlockLanguage != OperationInterlockLanguage.Scl) { base.Upload(); return; }
            if (batch == null) throw new InvalidOperationException("Prepare all operation interlocks before upload.");
            batch.Upload(descriptor);
        }
    }
}
