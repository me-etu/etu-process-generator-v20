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

            #region Unit BL170

            #region Sensors
            AddAnalog("BL170", "QC-B24", 2, 0, "uS/cm", 3, 0, 500);
            AddAnalog("BL170", "QC-B14", 2, 0, "uS/cm", 3, 0, 500);

            // Niveauschalter; Schliesser
            AddDigital("BL170", "Bl171-B13", 2, 0, 0, true, false);
            AddDigital("BL170", "Bl172-B13", 2, 0, 0, true, false);
            #endregion

            #region Valves
            AddValve("BL170", "Q11", 2, 0, 0, false, false, false, false, true);
            AddValve("BL170", "Q21", 2, 0, 0, false, false, false, false, true);

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
