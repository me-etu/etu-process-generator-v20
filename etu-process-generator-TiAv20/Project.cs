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


                #region Unit BV260-B210
                AddAnalog("BV260-B210", "LC-B13", 2, 0, "%", 0, 0, 100); // Radarsensor BV260 B13
                AddAnalog("BV260-B210", "QC-B14", 2, 0, "pH", 2, 0, 14); // pH measurement BV260 B14
                AddAnalog("BV260-B210", "QC-B24", 2, 0, "uS/cm", 1, 0, 1000); // Conductivity measurement BV260 B24
                AddAnalog("BV260-B210", "FI-B12", 4, 0, "l/h", 1, 0, 9600); // Ultrasonic flow meter BV260 B12
                AddAnalog("BV260-B210", "PS-B52", 3, 0, "bar", 1, 0, 10); // Pressure sensor for pumps BV260 B52
                AddAnalog("BV260-B210", "TC-B35", 2, 0, "°C", 1, -50, 200); // Temperature measurement distillate BV260 B35
                AddAnalog("BV260-B210", "TI-B15", 3, 0, "°C", 1, -50, 200); // Coolant temperature measurement On BV260 B15
                AddAnalog("BV260-B210", "TI-B25", 4, 0, "°C", 1, -50, 200); // Coolant temperature measurement Off BV260 B25
                AddDigital("BV260-B210", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV260 B23
                AddDigital("BV260-B210", "XS-B14", 1, 0, 0, false, false); // pH measurement calibration signal BV260 B16
                AddDigital("BV260-B210", "MS-G31", 1, 0, 0, false, false); // Maintenance switch BV260 G31 OK
                AddDigital("BV260-B210", "MS-G41", 1, 0, 0, false, false); // Maintenance switch BV260 G41 OK
                AddValve("BV260-B210", "Q21", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV260 Q21
                AddValve("BV260-B210", "Q31", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV260 Q31
                AddValve("BV260-B210", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV260 Q11
                AddValve("BV260-B210", "Q41", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV260 Q41
                AddMotorControl("BV260-B210", "G31-P1", 6, 0, 0, "%", 1, false, false, -1, -1); // Pump BV260 G31 Run
                AddMotorControl("BV260-B210", "G41-P2", 7, 0, 0, "%", 1, false, false, -1, -1); // Pump BV260 G41 Run
                // PID limits from payload verification are source-only until AddPidControl exposes them.
                AddPidControl("BV260-B210", "PID-G31-G41", 1, "bar", 2, "%", 1); // PID for pressure control pumps G31-P1, G41-P2
                #endregion


                #if false
                // units to skip generate
                   #region Unit BV130_B10_20
                AddAnalog("BV130_B10_20", "LC-B13", 2, 0, "%", 0, 0, 100); // Level radar sensor BV130 B13
                AddDigital("BV130_B10_20", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV130 B23
                AddDigital("BV130_B10_20", "MS-G11", 1, 0, 0, false, false); // Maintenance switch BV130 G11
                AddValve("BV130_B10_20", "Q11", 1, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV140 Q11
                AddMotor("BV130_B10_20", "G11", 5, 0, 0, false, false, -1, -1); // Pump BV130 G11 Run
                #endregion

                #region Unit BV140-B50
                AddAnalog("BV140-B50", "LC-B13", 2, 0, "%", 0, 0, 100); // Level radar sensor BV140 B13
                AddDigital("BV140-B50", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV140 B23
                #endregion

                #region Unit BV230-B120_130
                AddAnalog("BV230-B120_130", "LC-B13", 2, 0, "%", 0, 0, 100); // Level radar sensor BV230 B13
                AddDigital("BV230-B120_130", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV230 B23
                AddDigital("BV230-B120_130", "MS-G11", 1, 0, 0, false, false); // Maintenance switch BV230 G11
                AddValve("BV230-B120_130", "Q11", 1, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV240 Q11
                AddMotor("BV230-B120_130", "G11", 5, 0, 0, false, false, -1, -1); // Pump BV230 G11 Run
                #endregion

                #region Unit 4 BV240-B150
                AddAnalog("BV240-B150", "LC-B13", 2, 0, "%", 0, 0, 100); // Radarsensor BV240 B13
                AddDigital("BV240-B150", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV240 B23
                #endregion

                #region Unit BV160-B100
                AddDigital("BV160-B100", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV160 B23
                AddDigital("BV160-B100", "MS-G11", 1, 0, 0, false, false); // Maintenance switch BV160 G11
                AddDigital("BV160-B100", "XS-B32", 1, 0, 0, false, false); // Reed contact switch BV160 B32
                AddMotor("BV160-B100", "G11", 5, 0, 0, false, false, -1, -1); // Pump BV160 G11 run
                AddValve("BV160-B100", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV160 Q11
                AddMotor("BV160-B100", "E11", 6, 0, 0, false, false, -1, -1); // UV lamp BV160 E11 Enable
                #endregion


                #region Unit BV260-B220
                AddAnalog("BV260-B220", "LC-B43", 2, 0, "%", 0, 0, 100); // Radarsensor BV260 B43
                AddDigital("BV260-B220", "LSA-B33", 2, 0, 0, false, false); // Overfill protection BV260 B33
                AddDigital("BV260-B220", "FS-B42", 4, 0, 0, false, false); // Reed contact switch BV260 B42
                AddDigital("BV260-B220", "MS-G51", 1, 0, 0, false, false); // Maintenance switch BV260 G51
                AddDigital("BV260-B220", "MS-G61", 1, 0, 0, false, false); // Maintenance switch BV260 G61
                AddDigital("BV260-B220", "XS-B32", 1, 0, 0, false, false); // Reed contact switch BV260 B32
                AddMotor("BV260-B220", "G51", 5, 0, 0, false, false, -1, -1); // Pump BV260 G51 Run
                AddMotor("BV260-B220", "G61", 5, 0, 0, false, false, -1, -1); // Pump BV260 G61 Run
                AddMotor("BV260-B220", "E11", 6, 0, 0, false, false, -1, -1); // UV lamp BV260 E11 Enable
                #endregion

                #region Unit BV180-B250
                AddAnalog("BV180-B250", "LC-B13", 2, 0, "%", 0, 0, 100); // Radar sensor BV180 B13
                AddAnalog("BV180-B250", "PS-B12", 3, 0, "bar", 1, 0, 10); // Pressure sensor for pumps BV180 B12
                AddDigital("BV180-B250", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV180 B23
                AddDigital("BV180-B250", "MS-G21", 1, 0, 0, false, false); // Maintenance switch BV180 G21
                AddDigital("BV180-B250", "MS-G31", 1, 0, 0, false, false); // Maintenance switch BV180 G31
                AddDigital("BV180-B250", "MS-G11", 1, 0, 0, false, false); // Maintenance switch BV180 G11
                AddDigital("BV180-B250", "XS-B22", 1, 0, 0, false, false); // Reed contact switch BV180 B22
                AddValve("BV180-B250", "Q11", 2, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV180 Q11
                AddValve("BV180-B250", "Q21", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV180 Q21
                AddMotor("BV180-B250", "G11", 5, 0, 0, false, false, -1, -1); // Pump BV180 G11 run
                AddValve("BV180-B250", "Q31", 1, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV180 Q31
                AddMotorControl("BV180-B250", "G21-P1", 6, 0, 0, "%", 1, false, false, -1, -1); // Pump BV180 G21 Run
                AddMotorControl("BV180-B250", "G31-P2", 7, 0, 0, "%", 1, false, false, -1, -1); // Pump BV180 G31 Run
                AddMotor("BV180-B250", "E11", 6, 0, 0, false, false, -1, -1); // UV lamp BV180 E11 Enable
                // PID limits from payload verification are source-only until AddPidControl exposes them.
                AddPidControl("BV180-B250", "PID-G21-G31", 1, "bar", 2, "%", 1); // PID for pressure control pumps G21-P1, G31-P2
                #endregion

                #region Unit BV180-B270
                AddAnalog("BV180-B270", "LC-B43", 2, 0, "%", 0, 0, 100); // Radar sensor BV180 B43
                AddAnalog("BV180-B270", "PS-B42", 3, 0, "bar", 1, 0, 10); // Pressure sensor for pumps BV180 B42
                AddDigital("BV180-B270", "LSA-B33", 2, 0, 0, false, false); // Overfill protection BV180 B33
                AddDigital("BV180-B270", "MS-G41", 1, 0, 0, false, false); // Maintenance switch BV180 G41
                AddDigital("BV180-B270", "MS-G51", 1, 0, 0, false, false); // Maintenance switch BV180 G51
                AddDigital("BV180-B270", "XS-B32", 1, 0, 0, false, false); // Reed contact switch BV180 B32
                AddMotor("BV180-B270", "G51", 5, 0, 0, false, false, -1, -1); // Pump BV180 G51 run
                AddValve("BV180-B270", "Q41", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve BV180 Q41
                AddMotorControl("BV180-B270", "G41-P1", 7, 0, 0, "%", 1, false, false, -1, -1); // Pump BV180 G41 Run
                AddMotor("BV180-B270", "E31", 6, 0, 0, false, false, -1, -1); // UV lamp BV180 E31 Enable
                // PID limits from payload verification are source-only until AddPidControl exposes them.
                AddPidControl("BV180-B270", "PID-G41", 1, "bar", 2, "%", 1); // PID for pressure control pumps pumps G41-P1
                #endregion

                #region Unit BV250-B230A
                AddDigital("BV250-B230A", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV250 B23
                #endregion

                #region Unit BV250-B230B
                AddAnalog("BV250-B230B", "LC-B13", 2, 0, "%", 0, 0, 100); // Radarsensor BV250 B13
                AddDigital("BV250-B230B", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV250 B33
                AddDigital("BV250-B230B", "TIS-B15", 4, 0, 0, false, false); // Temperature measurement BV250 B15
                AddDigital("BV250-B230B", "MS-G11", 1, 0, 0, false, false); // Maintenance switch BV250 G11 OK
                AddValve("BV250-B230B", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV250 Q11
                AddValve("BV250-B230B", "Q21", 1, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV250 Q21
                AddValve("BV250-B230B", "Q31", 3, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV250 Q31
                AddValve("BV250-B230B", "Q41", 2, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV250 Q41
                AddValve("BV250-B230B", "Q51", 3, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV250 Q51
                AddMotorControl("BV250-B230B", "G11-P1", 6, 0, 0, "%", 1, false, false, -1, -1); // Pump BV250 G11 Run
                // PID limits from payload verification are source-only until AddPidControl exposes them.
                AddPidControl("BV250-B230B", "PID-G11", 1, "bar", 2, "%", 1); // PID for pressure control pumps G11-P1
                #endregion

                #region Unit BV150-B240A
                AddDigital("BV150-B240A", "MS-H11", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H11 On
                AddMotor("BV150-B240A", "H11", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H11 Run
                #endregion

                #region Unit BV150-B240B
                AddDigital("BV150-B240B", "MS-H21", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H21 On
                AddMotor("BV150-B240B", "H21", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H21 Run
                #endregion

                #region Unit BV150-B240C
                AddDigital("BV150-B240C", "MS-H31", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H31 On
                AddMotor("BV150-B240C", "H31", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H31 Run
                #endregion

                #region Unit BV150-B240D
                AddAnalog("BV150-B240D", "LC-B13", 2, 0, "%", 0, 0, 100); // Radar sensor BV150 B13
                AddDigital("BV150-B240D", "LSA-B33", 2, 0, 0, false, false); // Overfill protection BV150 B33
                AddDigital("BV150-B240D", "TIS-B15", 4, 0, 0, false, false); // Temperature measurement BV150 B15
                AddDigital("BV150-B240D", "MS-B15", 1, 0, 0, false, false); // Maintenance switch BV150 G11
                AddDigital("BV150-B240D", "MS-H41", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H41 On
                AddValve("BV150-B240D", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV150 Q11
                AddValve("BV150-B240D", "Q21", 1, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV150 Q21
                AddValve("BV150-B240D", "Q31", 3, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV150 Q31
                AddValve("BV150-B240D", "Q41", 2, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV150 Q41
                AddValve("BV150-B240D", "Q51", 3, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV150 Q51
                AddMotor("BV150-B240D", "H41", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H41 Run
                AddValve("BV150-B240D", "Q61", 2, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV150 Q61
                AddValve("BV150-B240D", "Q71", 3, 0, 0, false, false, false, false, false, -1, -1); // Open Valve BV150 Q71
                AddMotorControl("BV150-B240D", "G11-P1", 6, 0, 0, "%", 1, false, false, -1, -1); // Pump BV150 G11 Run
                // PID limits from payload verification are source-only until AddPidControl exposes them.
                AddPidControl("BV150-B240D", "PID-G11", 1, "bar", 2, "%", 1); // PID for pressure control pumps G11-P1
                #endregion

                #region Unit BV150-B260A
                AddAnalog("BV150-B260A", "LC-B23", 2, 0, "%", 0, 0, 100); // Radarsensor BV150 B23
                AddDigital("BV150-B260A", "LSA-B43", 2, 0, 0, false, false); // Overfill protection BV150 B43
                AddDigital("BV150-B260A", "MS-H51", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H51 On
                AddMotor("BV150-B260A", "H51", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H51 Run
                #endregion

                #region Unit BV150-B260B
                AddDigital("BV150-B260B", "MS-H61", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H61 On
                AddMotor("BV150-B260B", "H61", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H61 Run
                #endregion

                #region Unit BV150-B260C
                AddDigital("BV150-B260C", "MS-H71", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H71 On
                AddMotor("BV150-B260C", "H71", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H71 Run
                #endregion

                #region Unit BV150-B260D
                AddDigital("BV150-B260D", "MS-H81", 1, 0, 0, false, false); // Maintenance switch agitator BV150 H81 On
                AddMotor("BV150-B260D", "H81", 1, 0, 0, false, false, -1, -1); // Agitator BV150 H81 Run
                #endregion

                #region Unit PS110-Pump_well
                AddDigital("PS110-Pump_well", "LSA-B33", 2, 0, 0, false, false); // Overfill protection PS110 B33
                AddDigital("PS110-Pump_well", "PS110-B13", 4, 0, 0, false, false); // Float switch PS110 B13
                AddDigital("PS110-Pump_well", "PS110-B23", 4, 0, 0, false, false); // Float switch PS110 B23
                AddValve("PS110-Pump_well", "Q11", 3, 0, 0, false, false, false, false, false, -1, -1); // Open valve PS110 Q11
                AddValve("PS110-Pump_well", "Q17", 2, 0, 0, false, false, false, false, false, -1, -1); // Pump PS110 G100 Valve PS110 Q17 open
                #endregion
                #endif

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
