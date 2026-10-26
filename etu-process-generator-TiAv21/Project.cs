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
using System.Windows.Controls;

namespace EtuProcessGeneratorTiaV21
{
    public class Project : PlcProject
    {
        public Project()
        {
            CultureInfo ci = CultureInfo.CreateSpecificCulture("en-US");
            Thread.CurrentThread.CurrentCulture = ci;
            bool mon_opn = true;
            bool mon_cls = true;
            bool mon_on = true;

            #region Units





            //#region Unit TK003
            //AddAnalog("TK003", "LC-B13", 2, 2, "%", 0, 0, 100); // Level radar sensor BV130 B13
            //AddDigital("TK003", "LSA-B23", 2, 0, 0, false, false); // Overfill protection BV130 B23
            //AddDigital("TK003", "MS-G11", 1, 0, 0, true, false); // Maintenance switch BV130 G11
            //AddValve("TK003", "Q11", 1, 0, 0, false, false, false, true, false, -1, -1); // Open valve BV140 Q11
            //AddMotor("TK003", "G11", 5, 0, 0, false, false, -1, -1); // Pump BV130 G11 Run
            //AddPH("TK003", "OP6_Pressure", "PH_Vent");
            //AddPH("TK003", "OP6_Pressure", "PH_Blanketing");
            //AddPH("TK003", "OP6_Charge", "PH_Charge_Black_run");
            //AddPH("TK003", "OP6_Charge", "PH_Charge_Educt");
            //AddPH("TK003", "OP6_Charge", "PH_Charge_Catalyst", 1, false, 0, 300, 0, "L");
            //AddPH("TK003", "OP6_Charge", "PH_Charge_CIP");
            //AddPH("TK003", "OP6_Agitate", "PH_Agitate", 1, false, 0, 470, 0, "rpm");
            //AddPidControl("TK003", "TIC-1174", 1, "°C", 1);// PID control loop TIC 1174
            //AddPidControl("TK003", "TIC-1176", 1, "°C", 1);// PID control loop TIC 1176
            //AddPidControl("TK003", "PIC-1164", 1, "bar(g)", 1);// PID control loop PIC 1164
            //#endregion

            //HYDROGENATOR
            AddValve("410", "XV-410-700", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-701", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-702", 1, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-703", 3, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-704", 1, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-706", 1, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-708", 1, 4, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-709", 2, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-711", 1, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-712", 3, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-715", 3, 0, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-716", 3, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-717", 1, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-718", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-719", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-720", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-721", 1, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-722", 2, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-723", 3, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-724", 1, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-725", 2, 2, 1, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-727", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-729", 2, 0, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-730", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);
            AddValve("410", "XV-410-731", 2, 1, 0, mon_opn, mon_cls, false, true, true, 1);           
            AddValve("410", "XV-410-733", 1, 2, 1, mon_opn, mon_cls, false, true, true, 1);

            AddMotorControl("410", "AG410", 1, 2, 0, "rpm", 1, mon_on, false, 2);
            AddMotorControl("410", "PU411", 3, 2, 0, "%", 1, mon_on, false, 2);

            AddValveControl("410", "CV-410-707", 1, 3);
            AddValveControl("410", "CV-410-713", 3, 1);
            AddValveControl("410", "CV-410-726", 2, 1);

            AddDigital("410", "FISL-410-200", 4, 0);
            AddDigital("410", "FISL-410-728", 4, 0, 1);
            AddDigital("410", "LS-410-500", 3, 0);
            AddDigital("410", "LS-410-505", 3, 0);
            AddDigital("410", "LS-410-510", 3, 0, 1);
            AddDigital("410", "LS-410-515", 3, 0, 1);
            AddDigital("410", "PIS-410-625", 4, 0, 1);
            AddDigital("410", "GS-410-910", 3, 0, 1);
            AddDigital("410", "LS-410-525", 3, 0, 1);
            AddDigital("410", "LS-410-530", 2, 2, 1);
            AddDigital("410", "LS-410-535", 3, 0, 1);
            AddDigital("410", "PIS-410-650", 3, 0, 1);
            AddDigital("410", "TIS-410-150", 3, 0, 1);

            AddAnalog("410", "TI-410-135", 3, 10, "°C", 1, -20, 150);
            AddAnalog("410", "TI-410-140", 3, 10, "°C", 1, -20, 150);
            AddAnalog("410", "TI2-410-140", 3, 10, "°C", 1, -20, 150);
            AddAnalog("410", "TIC-410-101", 3, 1, "°C", 1, -20, 150);
            AddAnalog("410", "TIC-410-110", 3, 1, "°C", 1, -20, 150);
            AddAnalog("410", "LI-410-526", 3, 1, "%", 1, 0, 100);
            AddAnalog("410", "PI-410-610", 4, 10, "bar", 2, -1, 16);
            AddAnalog("410", "PI-410-645", 4, 0, "bar", 2, 0, 40);
            AddAnalog("410", "PI-410-640", 2, 5, "bar", 2, -1, 10);
            AddAnalog("410", "PDI-410-645", 4, 1, "bar", 1, 0, 41);
            AddAnalog("410", "FI-410-734", 3, 1, "l/min", 1, 0, 5);
            AddAnalog("410", "FI-410-735", 2, 1, "l/min", 1, 0, 20);
            AddAnalog("410", "FT-750-201", 2, 1, "g/min", 1, 0, 10000);

            //PID
            AddPidControl("410", "PIC-410-610-A", 1, "mbar", 0);
            AddPidControl("410", "PIC-410-610-B", 1, "bar", 2);
            AddPidControl("410", "PIC-410-610-C", 1, "bar", 2);
            AddPidControl("410", "TIC-410-135", 1, "°C", 1);





            AddPH("410", "OP1_Agitate", "PH_Agitate", 1, false, 85, 263, 0, "rpm");

            AddPH("410", "OP1_Charge", "PH_Charge_CLGO", 1, false, 0, 100, 1, "%");
            AddPH("410", "OP1_Charge", "PH_Charge_CASL");
            AddPH("410", "OP1_Charge", "PH_Charge_Blackrun");
            AddPH("410", "OP1_Charge", "PH_Charge_Filter_Heel");
            AddPH("410", "OP1_Charge", "PH_Charge_CIP_from_TK430");
            AddPH("410", "OP1_Charge", "PH_Charge_CIP_Blackrun_Piping");
            AddPH("410", "OP1_Charge", "PH_Charge_CIP_Heel_Piping");
            AddPH("410", "OP1_Charge", "PH_Charge_CIP_Nozzles");
            //AddPH("410", "OP1_Charge", "PH_Charge_CIP_Product_Outlet");

            AddPH("410", "OP1_Discharge", "PH_Filling_Blackrun_FL420", 2, false, 0, 5, 2, "bar", "", false, 0, 90, 1, "min");
            AddPH("410", "OP1_Discharge", "PH_Filtration", 2, false, 0, 5, 2, "bar", "", false, 0, 600, 1, "min");
            AddPH("410", "OP1_Discharge", "PH_Discharge_CIP_Nozzles");
            AddPH("410", "OP1_Discharge", "PH_Feed_CIP_Piping", 1, true, 2, 5, 0, "", "txtList_OP1_Discharge_PH_Feed_CIP_Piping");

            AddPH("410", "OP1_Pressure", "PH_Blanketing");
            AddPH("410", "OP1_Pressure", "PH_Re_Inert");
            AddPH("410", "OP1_Pressure", "PH_N2_Replace");
            AddPH("410", "OP1_Pressure", "PH_Reaction", 2, false, 0, 10, 2, "bar", "", false, 0, 1000, 1, "min");
            AddPH("410", "OP1_Pressure", "PH_Vent");
            AddPH("410", "OP1_Pressure", "PH_H2_Replace");
            AddPH("410", "OP1_Pressure", "PH_N2_Pressurize");

            AddPH("410", "OP1_Temperature", "PH_Coils", 1, false, 10, 95, 1, "°C");
            AddPH("410", "OP1_Temperature", "PH_Vessels", 1, false, 10, 95, 1, "°C");

            AddPH("410", "OP1_Sample", "PH_Sample");

#if false
            #region Unit hydrogenerator

            #region Digitale Eingänge
            AddDigital("A2", "LS1181", 4, 0, 1);
            AddDigital("A2", "LS1182", 4, 1, 1);
            AddDigital("A2", "LS1183", 4, 1, 1);
            AddDigital("A2", "LS1184", 4, 0, 1);
            AddDigital("A2", "LS1188", 2, 0, 1);
            AddDigital("A2", "FISL1185", 3, 0, 1);
            AddDigital("A2", "PI1162", 2, 0, 1);
            AddDigital("A2", "GS1120", 2, 0, 0);
            AddDigital("A2", "FI1007", 2, 0, 3);
            #endregion

            #region Analog Eingänge
            AddAnalog("A2", "FI1006", 2, 0, "m³/h", 2, 0.28f, 2.1f);
            AddAnalog("A2", "FI1081", 4, 0, "m³/h", 2, 0.1f, 1);
            AddAnalog("A2", "FIC1083", 4, 1, "m³/h", 2, 0.43f, 3.1f);
            AddAnalog("A2", "TI1071", 4, 4, "°C", 1, -40, 150);
            AddAnalog("A2", "TI1172", 2, 2, "°C", 1, -10, 150);
            AddAnalog("A2", "TI1174", 3, 2, "°C", 1, -40, 150);
            AddAnalog("A2", "TI1175", 2, 0, "°C", 1, -40, 150);
            AddAnalog("A2", "TI1176", 3, 2, "°C", 1, -40, 150);
            AddAnalog("A2", "PI1160", 2, 0, "bar(g)", 2, 0, 40);
            AddAnalog("A2", "PDI1160", 4, 1, "bar(g)", 2, 0, 40);
            AddAnalog("A2", "PI1166", 4, 2, "bar(g)", 2, 0, 16);
            AddAnalog("A2", "PI1164", 4, 3, "bar(g)", 2, 0, 16);
            #endregion

            #region Ventile Digital
            AddValve("A2", "VV1003", 2, 0, 0, false, false, false, true, true, 1);
            AddValve("A2", "VV1008", 1, 1, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1100", 1, 2, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1101", 2, 2, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1103", 1, 2, 1, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1104", 1, 1, 1, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1105", 2, 1, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1106", 2, 2, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1107", 2, 2, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1108", 2, 1, 0, mon_opn, false, false, true, true, 1);
            AddValve("A2", "VV1109", 2, 2, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1195", 2, 2, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1196", 2, 2, 0, false, mon_cls, false, true, true, 1);
            AddValve("A2", "VV1200", 1, 2, 1, false, mon_cls, false, true, true, 1);
            #endregion

            #region Ventile Analog
            AddValveControl("A2", "VV1000", 2, 2, 1);
            AddValveControl("A2", "VV1001", 2, 1, 1);
            AddValveControl("A2", "VV1002", 2, 2);
            AddValveControl("A2", "VV1004", 2, 2, 1);
            #endregion

            #region Motoren
            AddMotorControl("A2", "AG110-M001", 1, 1, 1, "rpm", 1, mon_on, false, 11);
            #endregion

            #region PID
            AddPidControl("A2", "TIC-1174", 1, "°C", 1);
            AddPidControl("A2", "TIC-1176", 1, "°C", 1);
            AddPidControl("A2", "PIC-1164", 1, "bar(g)", 1);
            #endregion

            #region Operations & Phases
            // Operation Charge
            AddPH("A2", "OP2_Charge", "PH_Charge_Black_run");
            AddPH("A2", "OP2_Charge", "PH_Charge_Educt");
            AddPH("A2", "OP2_Charge", "PH_Charge_Catalyst", 1, false, 0, 300, 0, "L");
            AddPH("A2", "OP2_Charge", "PH_Charge_CIP");
            // Operation discharge
            AddPH("A2", "OP2_Discharge", "PH_Filtration");
            AddPH("A2", "OP2_Discharge", "PH_Discharge");
            // Operation pressure
            AddPH("A2", "OP2_Pressure", "PH_Blanketing");
            AddPH("A2", "OP2_Pressure", "PH_Re-Inert");
            AddPH("A2", "OP2_Pressure", "PH_H2-replace");
            AddPH("A2", "OP2_Pressure", "PH_N2-replace");
            AddPH("A2", "OP2_Pressure", "PH_Reaction", 2, false, 0.5, 10, 1, "bar(g)", "", false, 5, 1000, 0, "min");
            AddPH("A2", "OP2_Pressure", "PH_Vent");
            // Operation Agitate
            AddPH("A2", "OP2_Agitate", "PH_Agitate", 1, false, 0, 470, 0, "rpm");
            // Operation Heating Cooling
            AddPH("A2", "OP2_Heating_Cooling", "PH_Jacket", 1, false, -25, 140, 0, "°C");
            AddPH("A2", "OP2_Heating_Cooling", "PH_Internal", 1, false, -25, 140, 0, "°C");
            #endregion

            #endregion
#endif

#if false
                // units to skip generate
                
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
