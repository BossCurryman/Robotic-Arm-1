using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // Initialising Vars
        // Blocks within the block group
        List<IMyMotorAdvancedStator> rotors = new List<IMyMotorAdvancedStator>();
        List<IMyShipWelder> welders = new List<IMyShipWelder>();
        List<IMyCockpit> seatCockpit = new List<IMyCockpit>();
        List<IMyTextSurfaceProvider> seatPanels = new List<IMyTextSurfaceProvider>();

        // Other vars
        bool resetEnabled = false;
        double heightMem, angleMem;
        double r1RefAngle, r2RefAngle, phiRefAngle, r1AVelocity, phiAVelocity, r2InstVelocity, r2AVelocity;
        double omega1, omega2, omegaPhi;
        int screenSelect, modelTime;

        const int ARM_LENGTH = 6;
        const int HORIZON_VELOCITY = 2;
        const int TICKRATE = 1 / 60;

        // Sorting function for blocks, Alphabetical order based on in-game name
        private int BlockSort(IMyCubeBlock x, IMyCubeBlock y)
        {
            return x.DisplayNameText.CompareTo(y.DisplayNameText);
        }

        // Constructor
        public Program()
        {
            // Set update frequency of Main method
            Runtime.UpdateFrequency = UpdateFrequency.Update1;

            // Initialise and grab in-game block group with name "robotArmGroup"
            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName("robotArmGroup");
            if (group == null)
            {
                Echo("Group with name 'robotArmGroup' cannot be found");
            }

            // Initialise each block type to be used within the group
            group.GetBlocksOfType(rotors);
            group.GetBlocksOfType(welders);
            group.GetBlocksOfType(seatCockpit);
            group.GetBlocksOfType(seatPanels);

            // Sort position sensitive blocks
            rotors.Sort(BlockSort);

        }

        public void Save()
        {
            // Pass
        }

        // Draws information on each screen of the command seat
        public void DrawScreens(int _case = 0)
        {
            // Left screen
            string leftScreen = $"Base Rotor Angle:\n {rotors[1].Angle}\n" +
                $"Base Rotor Speed:\n {rotors[1].TargetVelocityRPM}\n" +
                $"Sin of Angle:\n {Math.Sin(rotors[1].Angle)}";
            seatPanels[0].GetSurface(1).FontSize = 2;
            seatPanels[0].GetSurface(1).WriteText(leftScreen);

            // Center screen
            Vector3 directionV = seatCockpit[0].MoveIndicator;
            Vector2 rotationV = seatCockpit[0].RotationIndicator;
            string centerScreen;
            // Changes what the center screen will display
            switch (_case)
            {
                case 0:
                    // Displays direction and rotation input of the command seat
                    centerScreen = $"X: {directionV.X}\nY: {directionV.Y}\nZ: {directionV.Z}\n" +
                        $"RotationX: {rotationV.X}\nRotationY: {rotationV.Y}";
                    seatPanels[0].GetSurface(0).WriteText(centerScreen);
                    break;

                case 1:
                    // Displays vertical arm model telemetry (Angles, velocities, heights, etc.)
                    centerScreen = $"h: {heightMem}\nL: 6\n" +
                        $"t1: {180 * r1RefAngle / Math.PI}\n" +
                        $"phi: {180 * phiRefAngle / Math.PI}\n" +
                        $"t2: {180 * r2RefAngle / Math.PI}\nomega2:" +
                        $" {(float)Math.Asin((heightMem / 6) - (Math.Sin(r1RefAngle)))}";
                    seatPanels[0].GetSurface(0).WriteText(centerScreen);
                    break;

                case 2:
                    // Displays vertical arm model telemetry (Angles, velocities, heights, etc.)
                    centerScreen = $"h: {heightMem}\nL: 6\n" +
                        $"o1: {180 * r1AVelocity / Math.PI}\n" +
                        $"Phi: {180 * phiAVelocity / Math.PI}\n" +
                        $"phi: {180 * phiRefAngle / Math.PI}\n" +
                        $"o2: {180 * r2AVelocity / Math.PI}\n" +
                        $"L0: {((2 * (Math.Pow(ARM_LENGTH, 2))) * (1 - Math.Cos(phiAVelocity))) + Math.Pow(heightMem, 2)}";

                    seatPanels[0].GetSurface(0).WriteText(centerScreen);
                    break;
            }
        }

        // Moves the base of the arm using the mouse input from the control seat
        public void ArmBase()
        {
            Vector2 rotationV = seatCockpit[0].RotationIndicator;

            rotors[0].RotorLock = false;
            rotors[0].TargetVelocityRPM = (rotationV.Y) / 2;
            rotors[1].RotorLock = false;
            rotors[1].TargetVelocityRPM = (rotationV.X) / 2;
        }

        // Moves the arm using the WASD C input from the control seat
        public void ArmMove()
        {
            Vector3 movementV = seatCockpit[0].MoveIndicator;

            // Unlocks all relevant rotors (Rotors B - D)
            for (int i = 1; i <= 3; i++)
            {
                rotors[i].RotorLock = false;
            }

            // Cheacks for the the input direction and activates the appropriate rotors
            if (movementV.Y != 0)
            {
                rotors[1].TargetVelocityRPM = movementV.Y;
                rotors[2].TargetVelocityRPM = 2 * movementV.Y;
                rotors[3].TargetVelocityRPM = movementV.Y;
            }
            else if (movementV.Z != 0)
            {
                rotors[1].TargetVelocityRad = -movementV.Z / 2;
                //    rotors[2].TargetVelocityRPM = (float)phiAVelocity;
                //    rotors[3].TargetVelocityRPM = (float)r2AVelocity;
            }
        }

        // Resets the head to 0
        public void ResetArm()
        {
            IMyMotorAdvancedStator headRotor = rotors[4];
            headRotor.RotorLock = false;
            if (headRotor.Angle < Math.PI && rotors[4].Angle > 0.1)
            {
                headRotor.TargetVelocityRPM = -1;
            }
            else if (headRotor.Angle > Math.PI && rotors[4].Angle < Math.PI * 2 - 0.1)
            {
                headRotor.TargetVelocityRPM = 1;
            }
            else if (headRotor.Angle < 0.05 || headRotor.Angle > Math.PI * 2 - 0.05)
            {
                headRotor.TargetVelocityRPM = 0;
                headRotor.RotorLock = true;
                resetEnabled = false;
            }
        }

        // Does arm-model calculations for other methods
        public void DoMath()
        {
            r1RefAngle = (rotors[1].Angle + Math.PI / 2) % (Math.PI * 2);
            r2RefAngle = Math.Asin((heightMem / ARM_LENGTH) - Math.Sin(r1RefAngle)) % (Math.PI * 2);
            phiRefAngle = (rotors[2].Angle + Math.PI) % (Math.PI * 2);

            r1AVelocity = (rotors[1].TargetVelocityRPM);
            //phiAVelocity =  (angleMem - phiRefAngle) / TICKRATE;
            //r2AVelocity = (Math.Asin((heightMem/ARM_LENGTH) - Math.Sin(r1AVelocity)));
            r2InstVelocity = (r1AVelocity + r2AVelocity);

        }

        public void MathDTime(int time = 0)
        {
            //shorthand varible names to keep math expressions tidy
            double aSqr = Math.Pow((HORIZON_VELOCITY * time), 2);
            double hSqr = Math.Pow(heightMem, 2);
            double lSqr = Math.Pow(ARM_LENGTH, 2);

            omega1 = ((Math.Pow(HORIZON_VELOCITY, 2) * time) /
                (2 * ARM_LENGTH * (Math.Sqrt(aSqr + hSqr)) * Math.Sqrt(1 - ((aSqr + hSqr) / (4 * lSqr)))))
                +
                (heightMem /
                (HORIZON_VELOCITY * Math.Pow(time, 2) * ((hSqr / aSqr) + 1)));

            omega2 = ((Math.Pow(HORIZON_VELOCITY, 2) * time) /
                (2 * ARM_LENGTH * (Math.Sqrt(aSqr + hSqr)) * Math.Sqrt(1 - ((aSqr + hSqr) / (4 * lSqr)))))
                -
                (heightMem /
                (HORIZON_VELOCITY * Math.Pow(time, 2) * ((hSqr / aSqr) + 1)));

            omegaPhi = ((Math.Pow(HORIZON_VELOCITY, 2) * time) /
                (ARM_LENGTH * (Math.Sqrt(aSqr + hSqr)) * Math.Sqrt(1 - ((aSqr + hSqr) / (4 * lSqr)))));

        }


        // Main method
        public void Main(string argument, UpdateType updateSource)
        {
            DoMath();

            // Input arguments handling
            switch (argument)
            {
                case "reset":
                    resetEnabled = true;
                    break;

                case "inputs":
                    screenSelect = 0;
                    break;

                case "vertmodel":
                    screenSelect = 1;
                    break;

                case "horzmodel":
                    screenSelect = 2;
                    break;
            }

            if (resetEnabled == true) ResetArm();

            // Checks for mouse input of control seat and either acts upon the input or locks the rotors if no input
            Vector2 a = seatCockpit[0].RotationIndicator;
            if (a.X != 0 || a.Y != 0)
            {
                ArmBase();
            }
            else if (rotors[0].RotorLock == false)
            {
                //Locks every mouse input affected rotor (Rotors A,B)
                for (int i = 0; i <= 1; i++)
                {
                    rotors[i].RotorLock = true;
                }
            }

            // Checks for movement input of control seat and either acts upon the input or locks the rotors if no input
            Vector3 b = seatCockpit[0].MoveIndicator;
            if (b.Y != 0 || b.Z != 0)
            {
                modelTime = modelTime + TICKRATE;
                ArmMove();
            }
            else if (rotors[3].RotorLock == false)
            {
                //Locks every mouse input affected rotor (Rotors B - D)
                for (int i = 1; i <= 3; i++)
                {
                    rotors[i].RotorLock = true;
                    rotors[i].TargetVelocityRPM = 0;
                    modelTime = 0;
                }
            }
            else
            {
                /* Calculates the current height(h) of the arm for the arm-model calculations that take place in the 
                 * ArmMove method.
                 * This is calculated here as having this recalculated as the ArmMove method is being excecuted would
                 * result in, due to FLOP errors and Clang, a constantly changing height value. The height in the model
                 * is constant and not intantaneous essentially.
                 */
                heightMem = ARM_LENGTH * Math.Sin(r1RefAngle) + ARM_LENGTH * Math.Sin(r2RefAngle);
            }

            DrawScreens(screenSelect);
            //angleMem = phiRefAngle;
        }
    }
}
