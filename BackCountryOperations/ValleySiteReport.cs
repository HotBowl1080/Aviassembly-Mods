using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace MachWorks.BackCountryOperations
{
    [DataContract]
    internal sealed class ValleySiteReport
    {
        [DataMember] private string worldAsset { get; set; } = "LocalizedVersion";
        [DataMember] private int worldVersionStartingFrom { get; set; } = 24;
        [DataMember] private readonly SurveyPoint thresholdA;
        [DataMember] private readonly SurveyPoint thresholdB;
        [DataMember] private readonly float length;
        [DataMember] private readonly float width;
        [DataMember] private readonly float gradePercent;
        [DataMember] internal int BlockerCount { get; private set; }

        internal ValleySiteReport(Vector3 first, Vector3 last, float length, float width, int blockers)
        {
            thresholdA = new SurveyPoint(first);
            thresholdB = new SurveyPoint(last);
            this.length = length;
            this.width = width;
            gradePercent = 100f * (last.y - first.y) / length;
            BlockerCount = blockers;
        }

        internal bool Write(ManualLogSource logger, string region = "valley")
        {
            try
            {
                var directory = Path.Combine(Paths.BepInExRootPath, "BackCountryOperations", region + "-surveys");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, $"candidate-{Guid.NewGuid():N}.json");
                using (var output = File.Create(path + ".tmp"))
                {
                    new DataContractJsonSerializer(typeof(ValleySiteReport)).WriteObject(output, this);
                }

                File.Move(path + ".tmp", path);
                logger.LogInfo($"Runway survey saved to {path}. Obstructing colliders: {BlockerCount}.");
                return true;
            }
            catch (Exception error)
            {
                logger.LogWarning($"Could not save runway survey: {error}");
                return false;
            }
        }

        [DataContract]
        private struct SurveyPoint
        {
            [DataMember] private float x;
            [DataMember] private float y;
            [DataMember] private float z;

            internal SurveyPoint(Vector3 point)
            {
                x = point.x;
                y = point.y;
                z = point.z;
            }
        }
    }
}
