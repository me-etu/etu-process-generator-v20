using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TIA_LIB;
using Siemens.Engineering.HmiUnified.UI.Screens;
using Siemens.Engineering.HmiUnified.UI.Events;
using Siemens.Engineering.HmiUnified.UI.Dynamization;
using Siemens.Engineering.HmiUnified.UI.Dynamization.Script;
using Siemens.Engineering.HmiUnified.HmiTags;
using GemBox.Spreadsheet;
using Siemens.Engineering.SW.Blocks;
using System.Drawing;
using System.Globalization;
using System.Threading;

namespace EtuProcessGeneratorTiaV20
{
    public class Project : PlcProject
    {
        public Project()
        {
            CultureInfo ci = CultureInfo.CreateSpecificCulture("en-US");
            Thread.CurrentThread.CurrentCulture = ci;
            bool mon_opn = true;
            bool mon_cls = true;

            #region Units

            #region Unit Vakuumzentrale

            #region Sensors
            // Drucksensor Vakuumzentrale; Messbereich 0..1 bar abs.
            AddAnalog("Vakuumzentrale", "100-BP1", 2, 0, "bar abs.", 3, 0, 1);

            AddDigital("Vakuumzentrale", "101-MA1-WARTUNG", 0, 0, 0, false, false);
            AddDigital("Vakuumzentrale", "102-MA1-WARTUNG", 0, 0, 0, false, false);
            AddDigital("Vakuumzentrale", "103-MA1-WARTUNG", 0, 0, 0, false, false);
            AddDigital("Vakuumzentrale", "104-MA1-WARTUNG", 0, 0, 0, false, false);
            #endregion

            #region Motors
            // Analog-controlled motors; no direct motor command hardware tag was present in source evidence.
            AddMotorControl("Vakuumzentrale", "101-MA1", 6, 0, 0, "%", 1, false, false, -1, -1);
            AddMotorControl("Vakuumzentrale", "102-MA1", 6, 0, 0, "%", 1, false, false, -1, -1);
            AddMotorControl("Vakuumzentrale", "103-MA1", 6, 0, 0, "%", 1, false, false, -1, -1);
            AddMotorControl("Vakuumzentrale", "104-MA1", 6, 0, 0, "%", 1, false, false, -1, -1);
            #endregion

            #region Valves
            // Pump valves with open and closed feedback; QualityBit=false per workbook decision.
            AddValve("Vakuumzentrale", "101-MB1", 3, 0, 0, true, true, false, false, true, -1, -1);
            AddValve("Vakuumzentrale", "102-MB1", 3, 0, 0, true, true, false, false, true, -1, -1);
            AddValve("Vakuumzentrale", "103-MB1", 3, 0, 0, true, true, false, false, true, -1, -1);
            AddValve("Vakuumzentrale", "104-MB1", 3, 0, 0, true, true, false, false, true, -1, -1);

            // Line valves without feedback; QualityBit=false per workbook decision.
            AddValve("Vakuumzentrale", "110-MB1", 3, 0, 0, false, false, false, false, true, -1, -1);
            AddValve("Vakuumzentrale", "110-MB2", 3, 0, 0, false, false, false, false, true, -1, -1);
            AddValve("Vakuumzentrale", "110-MB3", 3, 0, 0, false, false, false, false, true, -1, -1);
            #endregion

            #endregion

            #endregion

            CreateTags();

            Upload();

            Portal.CheckTags();
           // GenerateSiVarc();

            Portal.DeleteRenamedTags();

            //if (Portal.CheckTags())
            //{
            //    GenerateSiVarc();
            //}

            UserInterface();

            //Exit TIA
            Console.WriteLine("TIA Portal wird getrennt!");
            Portal.Portal.Dispose();

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();

        }
    }
}
