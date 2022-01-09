using HarmonyLib;
using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;
using System.Reflection;

namespace SmarterFirefighters
{
    public class NewFireAI
    {
        // This method returns the ID of the nearest burning building within the specified maximum distance.
        // If no burning building exists within the specified maximum distance, it returns 0
        public static ushort FindBurningBuilding(Vector3 pos, float maxDistance)
        {
            // Initialize BuildingManager using <singleton> as in FindBurningTree
            BuildingManager instance = Singleton<BuildingManager>.instance;
            int minx = Mathf.Max((int)((pos.x - maxDistance) / 64f + 135f), 0);
            int minz = Mathf.Max((int)((pos.z - maxDistance) / 64f + 135f), 0);
            int maxx = Mathf.Min((int)((pos.x + maxDistance) / 64f + 135f), 269);
            int maxz = Mathf.Min((int)((pos.z + maxDistance) / 64f + 135f), 269);

            // Initialize default result if no burning building is found and specify maximum distance
            ushort result = 0;
            float shortestSquaredDistance = maxDistance * maxDistance;

            // Loop through every building grid within maximum distance
            for (int i = minz; i <= maxz; i++)
            {
                for (int j = minx; j <= maxx; j++)
                {
                    ushort currentBuilding = instance.m_buildingGrid[i * 270 + j];
                    int num7 = 0;

                    // Iterate through all buildings at this grid location
                    while (currentBuilding != 0)
                    {
                        if (instance.m_buildings.m_buffer[currentBuilding].m_fireIntensity != 0)
                        {
                            // If the new burning building is closer than the current result, set the new building as the result
                            // TODO: Test adding randomizer as found in FindBurningTree
                            // float currentSqauredDistance = Vector3.SqrMagnitude(pos - instance.m_buildings.m_buffer[currentBuilding].m_position);
                            float currentSqauredDistance = VectorUtils.LengthSqrXZ(pos - instance.m_buildings.m_buffer[currentBuilding].m_position);
                            if (currentSqauredDistance < shortestSquaredDistance)
                            {
                                result = currentBuilding;
                                shortestSquaredDistance = currentSqauredDistance;
                            }
                        }
                        currentBuilding = instance.m_buildings.m_buffer[currentBuilding].m_nextGridBuilding;
                        if (++num7 >= 49152)
                        {
                            CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                            break;
                        }
                    }
                }
            }
            return result;
        }

        // This method retargets the individual firefighter citizens spawned by fire trucks to the target of the parent vehicle
        public static void  TargetCimsParentVehicleTarget(ushort vehicleID, ref Vehicle vehicleData)
        {
            CitizenManager instance = Singleton<CitizenManager>.instance;
            uint numCitizenUnits = instance.m_units.m_size;
            uint num = vehicleData.m_citizenUnits;
            int num2 = 0;
            while (num != 0)
            {
                uint nextUnit = instance.m_units.m_buffer[num].m_nextUnit;
                for (int i = 0; i < 5; i++)
                {
                    uint citizen = instance.m_units.m_buffer[num].GetCitizen(i);
                    if (citizen == 0)
                    {
                        continue;
                    }
                    ushort instance2 = instance.m_citizens.m_buffer[citizen].m_instance;
                    if (instance2 == 0)
                    {
                        continue;
                    }
                    CitizenInfo info = instance.m_instances.m_buffer[instance2].Info;
                    info.m_citizenAI.SetTarget(instance2, ref instance.m_instances.m_buffer[instance2], vehicleData.m_targetBuilding);

                    //// Print debug data to log
                    //UnityEngine.Debug.Log("Cim " + instance2 + " from Firetruck ID: " + vehicleID + " retargeted to " + vehicleData.m_targetBuilding);
                }
                num = nextUnit;
                if (++num2 > numCitizenUnits)
                {
                    CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                    break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(FireTruckAI))]
    public static class FireTruckAISimulationStepPatch
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

    // Firecopters respond to fires that they fly near when waiting for a target or going back to the depot
    [HarmonyPatch(typeof(FireCopterAI))]
    public static class FireCopterAISimulationStepPatch
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
