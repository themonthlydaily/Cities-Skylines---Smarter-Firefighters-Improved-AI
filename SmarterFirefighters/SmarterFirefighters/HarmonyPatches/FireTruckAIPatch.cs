using HarmonyLib;
using System;
using ColossalFramework;
using UnityEngine;
using System.Reflection;

namespace SmarterFirefighters.HarmonyPatches
{ 
	[HarmonyPatch(typeof(FireTruckAI))]
    public static class FireTruckAIPatch
    {
        private delegate void CarAISimulationStepDelegate(CarAI instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics);
        private static readonly CarAISimulationStepDelegate BaseSimulationStep = AccessTools.MethodDelegate<CarAISimulationStepDelegate>(typeof(CarAI).GetMethod("SimulationStep", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vehicle.Frame).MakeByRefType(), typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(int) }, new ParameterModifier[] { }), null, false);

        private delegate bool ArriveAtTargetDelegate(FireTruckAI instance, ushort vehicleID, ref Vehicle data);
        private static ArriveAtTargetDelegate ArriveAtTarget = AccessTools.MethodDelegate<ArriveAtTargetDelegate>(typeof(FireTruckAI).GetMethod("ArriveAtTarget", BindingFlags.Instance | BindingFlags.NonPublic), null, false);

        private delegate bool ExtinguishFireDelegate(FireTruckAI instance, ushort vehicleID, ref Vehicle data, ushort buildingID, ref Building buildingData);
        private static ExtinguishFireDelegate ExtinguishFire = AccessTools.MethodDelegate<ExtinguishFireDelegate>(typeof(FireTruckAI).GetMethod("ExtinguishFire", BindingFlags.Instance | BindingFlags.NonPublic), null, false);

        private delegate void SetTargetDelegate(FireTruckAI instance, ushort vehicleID, ref Vehicle data, ushort targetBuilding);
        private static SetTargetDelegate SetTarget = AccessTools.MethodDelegate<SetTargetDelegate>(typeof(FireTruckAI).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.Public), null, false);

        private delegate bool CanLeaveDelegate(FireTruckAI instance, ushort vehicleID, ref Vehicle vehicleData);
        private static CanLeaveDelegate CanLeave = AccessTools.MethodDelegate<CanLeaveDelegate>(typeof(FireTruckAI).GetMethod("CanLeave", BindingFlags.Instance | BindingFlags.Public), null, false);

        private delegate bool ShouldReturnToSourceDelegate(FireTruckAI instance, ushort vehicleID, ref Vehicle data);
        private static ShouldReturnToSourceDelegate ShouldReturnToSource = AccessTools.MethodDelegate<ShouldReturnToSourceDelegate>(typeof(FireTruckAI).GetMethod("ShouldReturnToSource", BindingFlags.Instance | BindingFlags.NonPublic), null, false);

        [HarmonyPatch(typeof(FireTruckAI), "SimulationStep", 
            new Type[] { typeof(ushort), typeof(Vehicle), typeof(Vehicle.Frame), typeof(ushort), typeof(Vehicle), typeof(int) }, 
            new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal })]
        [HarmonyPrefix]

        public static bool SimulationStep(FireTruckAI __instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
        {
            frameData.m_blinkState = (((vehicleData.m_flags & (Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2)) == 0) ? 0f : 10f);
		    BaseSimulationStep(__instance, vehicleID, ref vehicleData, ref frameData, leaderID, ref leaderData, lodPhysics);
		    bool flag = false;
            ushort newTarget = 0;
		    if (vehicleData.m_targetBuilding != 0)
		    {
			    BuildingManager instance = Singleton<BuildingManager>.instance;
			    Vector3 vector = instance.m_buildings.m_buffer[vehicleData.m_targetBuilding].CalculateSidewalkPosition();
			    flag = (vector - frameData.m_position).sqrMagnitude < 4096f;
			    bool flag2 = (vehicleData.m_flags & Vehicle.Flags.Stopped) != 0 || frameData.m_velocity.sqrMagnitude < 0.0100000007f;
			    if (flag && (vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0)
			    {
				    vehicleData.m_flags = (vehicleData.m_flags & ~Vehicle.Flags.Emergency2) | Vehicle.Flags.Emergency1;
			    }
			    if (flag && flag2)
			    {
				    if (vehicleData.m_blockCounter > 8)
				    {
					    vehicleData.m_blockCounter = 8;
				    }
				    if (vehicleData.m_blockCounter == 8 && (vehicleData.m_flags & Vehicle.Flags.Stopped) == 0)
				    {
					    ArriveAtTarget(__instance, leaderID, ref leaderData);
				    }
                    // if finish extinguish search for new 
				    if (ExtinguishFire(__instance, vehicleID, ref vehicleData, vehicleData.m_targetBuilding, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[vehicleData.m_targetBuilding]))
				    {
                        newTarget = NewFireAI.FindBurningBuilding(vehicleData.GetLastFramePosition(), 40f);
                        if(newTarget != 0)
                        {
                            SetTarget(__instance, vehicleID, ref vehicleData, newTarget);
                        }
                        else
                        {
                            SetTarget(__instance, vehicleID, ref vehicleData, 0);
                        }
				    }
			    }
			    else if (instance.m_buildings.m_buffer[vehicleData.m_targetBuilding].m_fireIntensity == 0)
			    {
				    SetTarget(__instance, vehicleID, ref vehicleData, 0);
			    }
		    }
		    if ((vehicleData.m_flags & Vehicle.Flags.Stopped) != 0 && !flag && CanLeave(__instance, vehicleID, ref vehicleData))
		    {
                if(newTarget != 0)
                {
                    NewFireAI.TargetCimsParentVehicleTarget(vehicleID, ref vehicleData);
                }
                else
                {
                    vehicleData.m_flags &= ~Vehicle.Flags.Stopped;
			        vehicleData.m_flags |= Vehicle.Flags.Leaving;
                }
		    }
		    if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) == 0)
		    {
			    if (ShouldReturnToSource(__instance, vehicleID, ref vehicleData))
			    {
				    SetTarget(__instance, vehicleID, ref vehicleData, 0);
			    }
		    }
		    else if (((Singleton<SimulationManager>.instance.m_currentFrameIndex >> 4) & 0xF) == (vehicleID & 0xF) && !ShouldReturnToSource(__instance, vehicleID, ref vehicleData))
		    {
			    TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
			    offer.Priority = 3;
			    offer.Vehicle = vehicleID;
			    offer.Position = frameData.m_position;
			    offer.Amount = 1;
			    offer.Active = true;
			    Singleton<TransferManager>.instance.AddIncomingOffer((TransferManager.TransferReason)vehicleData.m_transferType, offer);
		    }
            return false;
        }
    }
}