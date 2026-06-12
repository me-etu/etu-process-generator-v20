using System;
using System.Collections.Generic;
using Siemens.Engineering.SW.Blocks;
using TIA_LIB.Devices;
using TIA_LIB.Xml;
using TIA_LIB.Xml.Network;
using Siemens.Engineering.HmiUnified.UI.Screens;
using System.Xml.Linq;

namespace TIA_LIB.Xml
{
    public class XmlUnit : XmlBlock
    {
        public XmlUnit(String name) : base(XmlPlant.Current.UserGroup, name, name + ".xml", "Xml/EmptyUnit.xml", name, true)
        {
            Name = name;

            XmlCall call = null;

            string calledBlockName;
            XmlNetwork network;

            //Unit Block
            calledBlockName = "fbUnit";
            network = FindNetwork(Name);

            if (network == null)
            {
                network = GetNetwork(Name);
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", Name);
                network.SetParameter(call, "en", "Input", "Bool");
                network.SetParameter(call, "OP_REQ", "Input", "Bool", "sfc_" + Name + "|" + "OP_REQ");
            }
            else
            {
                if (call == null)
                {
                    call = network.GetCall(calledBlockName, "FB", "LocalVariable", Name);
                }
            }

            call = null;

            //Interlocks
            //calledBlockName = Name + "_Interlocks";
            //network = FindNetwork("Interlocks");
            //
            //if (network == null)
            //{
            //    network = GetNetwork("Interlocks");
            //    call = network.GetCall(calledBlockName, "FB", "LocalVariable", Name + "_Interlocks");
            //    network.SetParameter(call, "en", "Input", "Bool");
            //}
            //
            //GetInterlock(Name);


            //for (var i = 1; i <= 50; i++)
            //{
            //    precond.GetLocalInstance(phName + "_PRECOND_" + (i < 10 ? "0" + i.ToString() : i.ToString()), "Bool");
            //}


            //call = null;

            //Preconditions
            calledBlockName = "fbIntlck50";
            network = FindNetwork("Preconditions " + Name);

            if (network == null)
            {
                network = GetNetwork("Preconditions " + Name);
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", "precond_" + Name);
                network.SetParameter(call, "en", "Input", "Bool");
                network.SetParameter(call, "START", "Input", "Bool", null + "|" + "START");
            }

            call = null;


            //Sfc control unit
            calledBlockName = "fbControlSfc";
            network = FindNetwork("Unit sfc control");

            if (network == null)
            {
                network = GetNetwork("Unit sfc control");
                call = network.GetCall(calledBlockName, "FB", "LocalVariable", "sfc_" + Name);

                network.SetParameter(call, "en", "Input", "Bool");

                network.SetParameter(call, "isUnit", "Input", "Bool", "True");
                network.SetParameter(call, "isOp", "Input", "Bool", "False");
                network.SetParameter(call, "isPh", "Input", "Bool", "False");

                network.SetParameter(call, "IDLE", "Output", "Bool", null + "|" + "IDLE");
                network.SetParameter(call, "HELD", "Output", "Bool", null + "|" + "HELD");
                network.SetParameter(call, "START", "Output", "Bool", null + "|" + "START");
               
                network.SetParameter(call, "STOP", "Output", "Bool", null + "|" + "STOP");
                network.SetParameter(call, "ABORT", "Output", "Bool", null + "|" + "ABORT");
                
                network.SetParameter(call, "PAUSE", "Output", "Bool", null + "|" + "PAUSE");
                network.SetParameter(call, "COMPLETED", "Output", "Bool", null + "|" + "COMPLETED");

                network.SetParameter(call, "STARTING", "Output", "Bool", null + "|" + "STARTING");
                network.SetParameter(call, "ABORTING", "Output", "Bool", null + "|" + "ABORTING");
                network.SetParameter(call, "STOPPING", "Output", "Bool", null + "|" + "STOPPING");

                network.SetParameter(call, "MODE_RECIPE", "Output", "Bool", null + "|" + "MODE_RECIPE");
                network.SetParameter(call, "MODE_OPERATOR", "Output", "Bool", null + "|" + "MODE_OPERATOR");

                network.SetParameter(call, "Reset", "Input", "Bool", "HMI|" + "RESET", true);

                network.SetParameter(call, "PRECOND_SSS", "Input", "Bool", "precond_" + Name + "|" + "QLOCK");    
            }


            Console.WriteLine("Block unit was found " + Name);

        }
        public GeneratedObject GetDevice(string tagName)
        {
            GeneratedObject device;
            Devices.TryGetValue(tagName, out device);
            return device;
        }

        public XmlOperation GetOP(string name)
        {
            XmlOperation op;

            if (!Operations.TryGetValue(name, out op))
            {
                op = new XmlOperation(this, name);
                Operations.Add(name, op);

                var network = FindNetwork(name);

                if (network == null)
                {
                    network = GetNetwork(name);
                    var call = network.GetCall(name, "FB", "LocalVariable", name);
                    network.SetParameter(call, "en", "Input", "Bool");
                    network.SetParameter(call, "InterfaceInOut", "Input", "DWord", "sfc_" + op.Unit.Name + "|" + "InterfaceInOut");
                }

            }
            return op;
        }

        public void SetFaceplateProperties()
        {
        }

        public Dictionary<string, GeneratedObject> Devices = new Dictionary<string, GeneratedObject>();
        public Dictionary<string, XmlOperation> Operations = new Dictionary<string, XmlOperation>();
    }
    }
