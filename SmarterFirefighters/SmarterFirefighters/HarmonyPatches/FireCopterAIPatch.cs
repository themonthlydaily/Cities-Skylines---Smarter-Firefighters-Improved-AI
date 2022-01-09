using HarmonyLib;
using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;
using System.Reflection;

namespace SmarterFirefighters.HarmonyPatches
{
    // Firecopters respond to fires that they fly near when waiting for a target or going back to the depot
    [HarmonyPatch(typeof(FireCopterAI))]
    public static class FireCopterAIPatch
    {
        private delegate void FindFillLocationDelegate(FireCopterAI instance, ushort vehicleID, ref Vehicle data);
        private static FindFillLocationDelegate FindFillLocation = AccessTools.MethodDelegate<FindFillLocationDelegate>(typeof(FireTruckAI).GetMethod("FindFillLocation", BindingFlags.Instance | BindingFlags.NonPublic), null, false);

        private delegate void HelicopterAISimulationStepDelegate(HelicopterAI instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics);
        private static readonly HelicopterAISimulationStepDelegate BaseSimulationStep = AccessTools.MethodDelegate<HelicopterAISimulationStepDelegate>(typeof(HelicopterAI).GetMethod("SimulationStep", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(Vehicle.Frame).MakeByRefType(), typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(int) }, new ParameterModifier[] { }), null, false);
        
        private delegate uint FindBurningTreeDelegate(FireCopterAI instance, int seed, Vector3 pos, float maxDistance, Vector3 priorityPos);
        private static FindBurningTreeDelegate FindBurningTree = AccessTools.MethodDelegate<FindBurningTreeDelegate>(typeof(FireTruckAI).GetMethod("FindBurningTree", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static), null, false);

        private delegate bool ExtinguishFire1Delegate(FireCopterAI instance, ushort vehicleID, ref Vehicle data, ushort buildingID, ref Building buildingData);
        private static ExtinguishFire1Delegate ExtinguishFire1 = AccessTools.MethodDelegate<ExtinguishFire1Delegate>(typeof(FireTruckAI).GetMethod("ExtinguishFire", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(ushort), typeof(Building).MakeByRefType() }, new ParameterModifier[] { }), null, false);

        private delegate bool ExtinguishFire2Delegate(FireCopterAI instance, ushort vehicleID, ref Vehicle data, uint treeID, ref TreeInstance treeData);
        private static ExtinguishFire2Delegate ExtinguishFire2 = AccessTools.MethodDelegate<ExtinguishFire2Delegate>(typeof(FireTruckAI).GetMethod("ExtinguishFire", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(ushort), typeof(Vehicle).MakeByRefType(), typeof(uint), typeof(TreeInstance).MakeByRefType() }, new ParameterModifier[] { }), null, false);

        private delegate void SetTargetDelegate(FireCopterAI instance, ushort vehicleID, ref Vehicle data, ushort targetBuilding);
        private static SetTargetDelegate SetTarget = AccessTools.MethodDelegate<SetTargetDelegate>(typeof(FireTruckAI).GetMethod("SetTarget", BindingFlags.Instance | BindingFlags.Public), null, false);

        private delegate bool ShouldReturnToSourceDelegate(FireCopterAI instance, ushort vehicleID, ref Vehicle data);
        private static ShouldReturnToSourceDelegate ShouldReturnToSource = AccessTools.MethodDelegate<ShouldReturnToSourceDelegate>(typeof(FireTruckAI).GetMethod("ShouldReturnToSource", BindingFlags.Instance | BindingFlags.NonPublic), null, false);

        [HarmonyPatch(typeof(FireCopterAI), "SimulationStep", 
        new Type[] { typeof(ushort), typeof(Vehicle), typeof(Vehicle.Frame), typeof(ushort), typeof(Vehicle), typeof(int) }, 
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal })]
        [HarmonyPrefix]

        public static bool SimulationStep(FireCopterAI __instance, ushort vehicleID, ref Vehicle vehicleData, ref Vehicle.Frame frameData, ushort leaderID, ref Vehicle leaderData, int lodPhysics)
	    {
		    frameData.m_blinkState = (((leaderData.m_flags & (Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2)) == 0) ? 0f : 10f);
		    if (leaderData.m_transferSize == 0 && (leaderData.m_flags & Vehicle.Flags.GoingBack) == 0 && (leaderData.m_flags & (Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2)) != 0)
		    {
			    FindFillLocation(__instance, leaderID, ref leaderData);
		    }
		    BaseSimulationStep(__instance, vehicleID, ref leaderData, ref frameData, leaderID, ref leaderData, lodPhysics);
		    bool flag = false;
		    if (leaderData.m_targetBuilding != 0)
		    {
			    if (leaderData.m_transferType == 68)
			    {
				    BuildingManager instance = Singleton<BuildingManager>.instance;
				    BuildingInfo info = instance.m_buildings.m_buffer[leaderData.m_targetBuilding].Info;
				    Vector3 position = instance.m_buildings.m_buffer[leaderData.m_targetBuilding].m_position;
				    float maxDistance = info.m_buildingAI.MaxFireDetectDistance(leaderData.m_targetBuilding, ref instance.m_buildings.m_buffer[leaderData.m_targetBuilding]);
				    uint num = FindBurningTree(__instance, vehicleID, position, maxDistance, frameData.m_position);
				    if (num != 0)
				    {
					    TreeManager instance2 = Singleton<TreeManager>.instance;
					    position = Singleton<TreeManager>.instance.m_trees.m_buffer[num].Position;
					    if (VectorUtils.LengthSqrXZ(position - frameData.m_position) < 100f)
					    {
						    if ((leaderData.m_flags & Vehicle.Flags.Emergency2) != 0)
						    {
							    leaderData.m_flags = (leaderData.m_flags & ~Vehicle.Flags.Emergency2) | Vehicle.Flags.Emergency1;
						    }
						    if (ExtinguishFire2(__instance, leaderID, ref leaderData, num, ref instance2.m_trees.m_buffer[num]) && FindBurningTree(__instance, vehicleID, position, maxDistance, frameData.m_position) == 0)
						    {
							    SetTarget(__instance, leaderID, ref leaderData, 0);
						    }
					    }
					    else if ((leaderData.m_flags & Vehicle.Flags.Emergency1) != 0 && VectorUtils.LengthSqrXZ(position - frameData.m_position) >= 900f)
					    {
						    leaderData.m_flags = (leaderData.m_flags & ~Vehicle.Flags.Emergency1) | Vehicle.Flags.Emergency2;
					    }
				    }
				    else
				    {
					    SetTarget(__instance, leaderID, ref leaderData, 0);
				    }
			    }
			    else
			    {
				    BuildingManager instance3 = Singleton<BuildingManager>.instance;
				    instance3.m_buildings.m_buffer[leaderData.m_targetBuilding].CalculateMeshPosition(out var meshPosition, out var _);
				    if (VectorUtils.LengthSqrXZ(meshPosition - frameData.m_position) < 64f)
				    {
					    if ((leaderData.m_flags & Vehicle.Flags.Emergency2) != 0)
					    {
						    leaderData.m_flags = (leaderData.m_flags & ~Vehicle.Flags.Emergency2) | Vehicle.Flags.Emergency1;
					    }
					    if (ExtinguishFire1(__instance, leaderID, ref leaderData, leaderData.m_targetBuilding, ref instance3.m_buildings.m_buffer[leaderData.m_targetBuilding]))
					    {
						    SetTarget(__instance, leaderID, ref leaderData, 0);
					    }
				    }
				    else
				    {
					    if ((leaderData.m_flags & Vehicle.Flags.Emergency1) != 0)
					    {
						    leaderData.m_flags = (leaderData.m_flags & ~Vehicle.Flags.Emergency1) | Vehicle.Flags.Emergency2;
					    }
					    if (instance3.m_buildings.m_buffer[leaderData.m_targetBuilding].m_fireIntensity == 0)
					    {
						    SetTarget(__instance, leaderID, ref leaderData, 0);
					    }
				    }
			    }
		    }
            if ((leaderData.m_flags & Vehicle.Flags.GoingBack) != 0)
		    {
			    ushort newTarget = NewFireAI.FindBurningBuilding(vehicleData.GetLastFramePosition(), 500f);
                if (newTarget != 0)
                {
                    // Switch to building extinguishing material if carrying tree extinguishing material
                    // If this is not done, copter will despawn if targeted at a burning building
                    if(leaderData.m_transferType == 68)
                    {
                        leaderData.m_transferType = 71;
                    }
                    SetTarget(__instance, leaderID, ref leaderData, newTarget);
                }
		    }
		    if ((leaderData.m_flags & Vehicle.Flags.GoingBack) == 0)
		    {
			    if (ShouldReturnToSource(__instance, leaderID, ref leaderData))
			    {
				    SetTarget(__instance, leaderID, ref leaderData, 0);
			    }
			    else if (leaderData.m_transferSize == 0 && (leaderData.m_flags & (Vehicle.Flags.Emergency1 | Vehicle.Flags.Emergency2)) != 0)
			    {
				    FindFillLocation(__instance, leaderID, ref leaderData);
			    }
		    }
		    else if (((Singleton<SimulationManager>.instance.m_currentFrameIndex >> 4) & 0xF) == (leaderID & 0xF) && !ShouldReturnToSource(__instance, leaderID, ref leaderData))
		    {
			    TransferManager.TransferOffer offer = default(TransferManager.TransferOffer);
			    offer.Priority = 3;
			    offer.Vehicle = leaderID;
			    offer.Position = frameData.m_position;
			    offer.Amount = 1;
			    offer.Active = true;
			    Singleton<TransferManager>.instance.AddIncomingOffer((TransferManager.TransferReason)leaderData.m_transferType, offer);
		    }
            return false;
	    }
    }
}
