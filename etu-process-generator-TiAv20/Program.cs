using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HW.Features;
using System.Drawing;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HmiUnified.UI.Widgets;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.HmiConnections;
using Siemens.Engineering.HmiUnified.UI.Controls;
using Siemens.Engineering.HmiUnified.UI.Parts;
using Siemens.Engineering.HmiUnified.HmiLogging;
using Siemens.Engineering.HmiUnified.LoggingTags;
using Siemens.Engineering.HW.Extensions;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SiVArc;
using System.Xml.Linq;
using System.Xml;
using TIA_LIB;
using TIA_LIB.Devices;
using TIA_LIB.SignalStaging;
using Siemens.Engineering.Compiler;

using Siemens.Engineering.HmiUnified.UI.Events;
using Siemens.Engineering.HmiUnified.UI.Enum;
using Siemens.Engineering.HmiUnified.UI.Base;
namespace EtuProcessGeneratorTiaV20
{
    
    public class Program: PlcProject
    {
        static void Main(string[] args)
        {
            PlcProject.StagingMode = SelectSignalStagingMode();
            Console.WriteLine("Signal staging mode: " + (PlcProject.StagingMode == SignalStagingMode.GeneratedDbUdt ? "Generated DB/UDT staging" : "Marker memory"));

            DeleteGeneratedOutput("Plant.xml");

            new Project();
            return;

            //Enumerate Blocks
            Console.WriteLine("Enumerate all blocks");
            //portal.EnumerateAllBlockGroupsAndSubgroups();

            //portal.DeleteAllBlocks();
            //portal.ImportAllBlocks();

            //Console.WriteLine("Press any key to exit.");
            //Console.ReadKey();

            //portal.CompareListToDelete();

            //portal.DeleteAllBlocks();

            //portal.ImportAllBlocks();
            //PlcProject.Upload();
            //portal.CreateAllinstance();

           
            //Enumerate Blocks
            //Console.WriteLine("Enumerate all blocks");
            //portal.EnumerateAllBlockGroupsAndSubgroups();

            //Compile Software after generate
            //portal.CompilePlc();

            //Generate SiVarc
            //Console.WriteLine("Generate objects visu");
            //portal.SiVarcGenerate("RT_Unified", "CPU");

            //Delete renamed screens
            //portal.DeleteRenamedScreens();

            //new UserInterface();

            //create hmi tags
            //portal.createAllhmiTags();

            //Set tags GMP
            //portal.SetAllTagsGMP();

            //set tags ranges
            //portal.SetAllTagsRange();

            //Create list of plcBlocks
            //if (File.Exists(@"c:\temp\MyTest.txt")) File.Delete(@"c:\temp\MyTest.txt");

            //portal.EnumerateAllBlockGroupsAndSubgroups();

            //portal.Sort("FB");
            //portal.Sort("FC");
            //portal.Sort("OB");
            //portal.Sort("GlobalDB");
            //portal.Sort("InstanceDB");

            //Exit TIA
            Console.WriteLine("TIA Portal wird getrennt!");
            //portal.Portal.Dispose();

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }


        private static SignalStagingMode SelectSignalStagingMode()
        {
            while (true)
            {
                Console.WriteLine("Select signal staging mode:");
                Console.WriteLine("1. Marker memory (current default)");
                Console.WriteLine("2. Generated DB/UDT staging (experimental)");
                Console.WriteLine();
                Console.Write("Choice [1]: ");

                string choice = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(choice) || choice.Trim() == "1")
                {
                    return SignalStagingMode.MarkerMemory;
                }

                if (choice.Trim() == "2")
                {
                    return SignalStagingMode.GeneratedDbUdt;
                }

                Console.WriteLine("Invalid choice. Enter 1 or 2.");
                Console.WriteLine();
            }
        }

        private static void DeleteGeneratedOutput(string fileName)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

