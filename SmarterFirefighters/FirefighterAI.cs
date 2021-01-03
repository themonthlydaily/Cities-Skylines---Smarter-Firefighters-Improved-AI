using HarmonyLib;
using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace SmarterFirefighters
{
    public class NewFireAI
    {
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
    }

    // Instruct firetrucks to respond to fires that they drive by when returning to the station or nearby fires when waiting for a new target
    // example from https://github.com/pardeike/Harmony/issues/132
    // [HarmonyPatch(typeof(PedestrianPathAI), nameof(PedestrianPathAI.SimulationStep), new Type[] { typeof(ushort), typeof(NetSegment) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref })]
    [HarmonyPatch(typeof(FireTruckAI), "SimulationStep", new Type[] { typeof(ushort), typeof(Vehicle), typeof(Vehicle.Frame), typeof(ushort), typeof(Vehicle), typeof(int) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal })]
    public static class FireTruckAISimulationStepPatch
    {
        public static void Postfix(ushort vehicleID, ref Vehicle vehicleData)
        {
            if ((vehicleData.m_flags & (Vehicle.Flags.GoingBack | Vehicle.Flags.WaitingTarget)) != 0)
            {
                ushort newTarget = NewFireAI.FindBurningBuilding(vehicleData.GetLastFramePosition(), 50f);
                if (newTarget != 0)
                {
                    vehicleData.Info.m_vehicleAI.SetTarget(vehicleID, ref vehicleData, newTarget);

                    //// Print debug data to log
                    //BuildingManager instance = Singleton<BuildingManager>.instance;
                    //float tempDistance = VectorUtils.LengthXZ(vehicleData.GetLastFramePosition() - instance.m_buildings.m_buffer[newTarget].m_position);
                    //UnityEngine.Debug.Log("Firetruck ID: " + vehicleID + " target set " + newTarget + " distance " + tempDistance);
                }
            }
        }
    }

    // Firecopters respond to fires that they fly near when waiting for a target or going back to the depot
    [HarmonyPatch(typeof(FireCopterAI), "SimulationStep", new Type[] { typeof(ushort), typeof(Vehicle), typeof(Vehicle.Frame), typeof(ushort), typeof(Vehicle), typeof(int) }, new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Normal })]
    public static class FireCopterAISimulationStepPatch
    {
        public static void Postfix(ushort vehicleID, ref Vehicle vehicleData)
        {
            if ((vehicleData.m_flags & Vehicle.Flags.GoingBack) != 0)
            {
                ushort newTarget = NewFireAI.FindBurningBuilding(vehicleData.GetLastFramePosition(), 500f);
                if (newTarget != 0)
                {
                    vehicleData.Info.m_vehicleAI.SetTarget(vehicleID, ref vehicleData, newTarget);

                    //// Print debug data to log
                    //BuildingManager instance = Singleton<BuildingManager>.instance;
                    //float tempDistance = VectorUtils.LengthXZ(vehicleData.GetLastFramePosition() - instance.m_buildings.m_buffer[newTarget].m_position);
                    //UnityEngine.Debug.Log("FireCopter ID: " + vehicleID + " target set " + newTarget + " distance " + tempDistance);
                }
            }
        }
    }
}
