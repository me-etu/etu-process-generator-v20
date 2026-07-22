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

            #region Unit BV130_B10_20
            AddAnalog("BV130_B10_20", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddDigital("BV130_B10_20", "LSA-B23", 2, 0, 0, false, false);
            AddDigital("BV130_B10_20", "MS-G11", 1, 0, 0, false, false);
            AddValve("BV130_B10_20", "Q11", 1, 0, 0, false, false, false, false, false, -1, -1);
            AddMotor("BV130_B10_20", "G11", 5, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV140-B50
            AddAnalog("BV140-B50", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddDigital("BV140-B50", "LSA-B23", 2, 0, 0, false, false);
            #endregion

            #region Unit BV230-B120_130
            AddAnalog("BV230-B120_130", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddDigital("BV230-B120_130", "LSA-B23", 2, 0, 0, false, false);
            AddDigital("BV230-B120_130", "MS-G11", 1, 0, 0, false, false);
            AddValve("BV230-B120_130", "Q11", 1, 0, 0, false, false, false, false, false, -1, -1);
            AddMotor("BV230-B120_130", "G11", 5, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV240-B150
            AddAnalog("BV240-B150", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddDigital("BV240-B150", "LSA-B23", 2, 0, 0, false, false);
            #endregion

            #region Unit BV160-B100
            AddDigital("BV160-B100", "LSA-B23", 2, 0, 0, false, false);
            AddDigital("BV160-B100", "MS-G11", 1, 0, 0, false, false);
            AddDigital("BV160-B100", "XS-B32", 1, 0, 0, false, false);
            AddMotor("BV160-B100", "G11", 5, 0, 0, false, false, -1, -1);
            AddValve("BV160-B100", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddMotor("BV160-B100", "E11", 6, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV260-B210
            AddAnalog("BV260-B210", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddAnalog("BV260-B210", "QC-B14", 2, 0, "pH", 2, 0, 14);
            AddAnalog("BV260-B210", "QC-B24", 2, 0, "uS/cm", 1, 0, 1000);
            AddAnalog("BV260-B210", "FI-B12", 4, 0, "l/h", 1, 0, 9600);
            AddAnalog("BV260-B210", "PS-B52", 3, 0, "bar", 1, 0, 10);
            AddAnalog("BV260-B210", "TC-B35", 2, 0, "°C", 1, -50, 200);
            AddAnalog("BV260-B210", "TI-B15", 3, 0, "°C", 1, -50, 200);
            AddAnalog("BV260-B210", "TI-B25", 4, 0, "°C", 1, -50, 200);
            AddDigital("BV260-B210", "LSA-B23", 2, 0, 0, false, false);
            AddDigital("BV260-B210", "XS-B14", 1, 0, 0, false, false);
            AddDigital("BV260-B210", "MS-G31", 1, 0, 0, false, false);
            AddDigital("BV260-B210", "MS-G41", 1, 0, 0, false, false);
            AddValve("BV260-B210", "Q21", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV260-B210", "Q31", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV260-B210", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV260-B210", "Q41", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddMotorControl("BV260-B210", "G31-P1", 6, 0, 0, "%", 1, false, false, -1, -1);
            AddMotorControl("BV260-B210", "G41-P2", 7, 0, 0, "%", 1, false, false, -1, -1);
            // PID limits from payload verification are source-only until AddPidControl exposes them.
            AddPidControl("BV260-B210", "PID-G31-G41", 1, "bar", 2, "%", 1);
            #endregion

            #region Unit BV260-B220
            AddAnalog("BV260-B220", "LC-B43", 2, 0, "%", 0, 0, 100);
            AddDigital("BV260-B220", "LSA-B33", 2, 0, 0, false, false);
            AddDigital("BV260-B220", "FS-B42", 4, 0, 0, false, false);
            AddDigital("BV260-B220", "MS-G51", 1, 0, 0, false, false);
            AddDigital("BV260-B220", "MS-G61", 1, 0, 0, false, false);
            AddDigital("BV260-B220", "XS-B32", 1, 0, 0, false, false);
            AddMotor("BV260-B220", "G51", 5, 0, 0, false, false, -1, -1);
            AddMotor("BV260-B220", "G61", 5, 0, 0, false, false, -1, -1);
            AddMotor("BV260-B220", "E11", 6, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV180-B250
            AddAnalog("BV180-B250", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddAnalog("BV180-B250", "PS-B12", 3, 0, "bar", 1, 0, 10);
            AddDigital("BV180-B250", "LSA-B23", 2, 0, 0, false, false);
            AddDigital("BV180-B250", "MS-G21", 1, 0, 0, false, false);
            AddDigital("BV180-B250", "MS-G31", 1, 0, 0, false, false);
            AddDigital("BV180-B250", "MS-G11", 1, 0, 0, false, false);
            AddDigital("BV180-B250", "XS-B22", 1, 0, 0, false, false);
            AddValve("BV180-B250", "Q11", 2, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV180-B250", "Q21", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddMotor("BV180-B250", "G11", 5, 0, 0, false, false, -1, -1);
            AddValve("BV180-B250", "Q31", 1, 0, 0, false, false, false, false, false, -1, -1);
            AddMotorControl("BV180-B250", "G21-P1", 6, 0, 0, "%", 1, false, false, -1, -1);
            AddMotorControl("BV180-B250", "G31-P2", 7, 0, 0, "%", 1, false, false, -1, -1);
            AddMotor("BV180-B250", "E11", 6, 0, 0, false, false, -1, -1);
            // PID limits from payload verification are source-only until AddPidControl exposes them.
            AddPidControl("BV180-B250", "PID-G21-G31", 1, "bar", 2, "%", 1);
            #endregion

            #region Unit BV180-B270
            AddAnalog("BV180-B270", "LC-B43", 2, 0, "%", 0, 0, 100);
            AddAnalog("BV180-B270", "PS-B42", 3, 0, "bar", 1, 0, 10);
            AddDigital("BV180-B270", "LSA-B33", 2, 0, 0, false, false);
            AddDigital("BV180-B270", "MS-G41", 1, 0, 0, false, false);
            AddDigital("BV180-B270", "MS-G51", 1, 0, 0, false, false);
            AddDigital("BV180-B270", "XS-B32", 1, 0, 0, false, false);
            AddMotor("BV180-B270", "G51", 5, 0, 0, false, false, -1, -1);
            AddValve("BV180-B270", "Q41", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddMotorControl("BV180-B270", "G41-P1", 7, 0, 0, "%", 1, false, false, -1, -1);
            AddMotor("BV180-B270", "E31", 6, 0, 0, false, false, -1, -1);
            // PID limits from payload verification are source-only until AddPidControl exposes them.
            AddPidControl("BV180-B270", "PID-G41", 1, "bar", 2, "%", 1);
            #endregion

            #region Unit BV250-B230A
            AddDigital("BV250-B230A", "LSA-B23", 2, 0, 0, false, false);
            #endregion

            #region Unit BV250-B230B
            AddAnalog("BV250-B230B", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddDigital("BV250-B230B", "LSA-B23", 2, 0, 0, false, false);
            AddDigital("BV250-B230B", "TIS-B15", 4, 0, 0, false, false);
            AddDigital("BV250-B230B", "MS-G11", 1, 0, 0, false, false);
            AddValve("BV250-B230B", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV250-B230B", "Q21", 1, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV250-B230B", "Q31", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV250-B230B", "Q41", 2, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV250-B230B", "Q51", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddMotorControl("BV250-B230B", "G11-P1", 6, 0, 0, "%", 1, false, false, -1, -1);
            // PID limits from payload verification are source-only until AddPidControl exposes them.
            AddPidControl("BV250-B230B", "PID-G11", 1, "bar", 2, "%", 1);
            #endregion

            #region Unit BV150-B240A
            AddDigital("BV150-B240A", "MS-H11", 1, 0, 0, false, false);
            AddMotor("BV150-B240A", "H11", 1, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV150-B240B
            AddDigital("BV150-B240B", "MS-H21", 1, 0, 0, false, false);
            AddMotor("BV150-B240B", "H21", 1, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV150-B240C
            AddDigital("BV150-B240C", "MS-H31", 1, 0, 0, false, false);
            AddMotor("BV150-B240C", "H31", 1, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV150-B240D
            AddAnalog("BV150-B240D", "LC-B13", 2, 0, "%", 0, 0, 100);
            AddDigital("BV150-B240D", "LSA-B33", 2, 0, 0, false, false);
            AddDigital("BV150-B240D", "TIS-B15", 4, 0, 0, false, false);
            AddDigital("BV150-B240D", "MS-B15", 1, 0, 0, false, false);
            AddDigital("BV150-B240D", "MS-H41", 1, 0, 0, false, false);
            AddValve("BV150-B240D", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV150-B240D", "Q21", 1, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV150-B240D", "Q31", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV150-B240D", "Q41", 2, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV150-B240D", "Q51", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddMotor("BV150-B240D", "H41", 1, 0, 0, false, false, -1, -1);
            AddValve("BV150-B240D", "Q61", 2, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("BV150-B240D", "Q71", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddMotorControl("BV150-B240D", "G11-P1", 6, 0, 0, "%", 1, false, false, -1, -1);
            // PID limits from payload verification are source-only until AddPidControl exposes them.
            AddPidControl("BV150-B240D", "PID-G11", 1, "bar", 2, "%", 1);
            #endregion

            #region Unit BV150-B260A
            AddAnalog("BV150-B260A", "LC-B23", 2, 0, "%", 0, 0, 100);
            AddDigital("BV150-B260A", "LSA-B43", 2, 0, 0, false, false);
            AddDigital("BV150-B260A", "MS-H51", 1, 0, 0, false, false);
            AddMotor("BV150-B260A", "H51", 1, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV150-B260B
            AddDigital("BV150-B260B", "MS-H61", 1, 0, 0, false, false);
            AddMotor("BV150-B260B", "H61", 1, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV150-B260C
            AddDigital("BV150-B260C", "MS-H71", 1, 0, 0, false, false);
            AddMotor("BV150-B260C", "H71", 1, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit BV150-B260D
            AddDigital("BV150-B260D", "MS-H81", 1, 0, 0, false, false);
            AddMotor("BV150-B260D", "H81", 1, 0, 0, false, false, -1, -1);
            #endregion

            #region Unit PS110-Pump_well
            AddDigital("PS110-Pump_well", "LSA-B33", 2, 0, 0, false, false);
            AddDigital("PS110-Pump_well", "PS110-B13", 4, 0, 0, false, false);
            AddDigital("PS110-Pump_well", "PS110-B23", 4, 0, 0, false, false);
            AddValve("PS110-Pump_well", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1);
            AddValve("PS110-Pump_well", "Q17", 2, 0, 0, false, false, false, false, false, -1, -1);
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
