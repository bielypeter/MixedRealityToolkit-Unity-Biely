using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WheelchairSim
{
    /// <summary>
    /// Names the files of one test run, so the trajectory log and the task summary belong together.
    /// Files go to <c>Application.persistentDataPath/WheelchairSim/</c>.
    /// </summary>
    public static class SimSession
    {
        public static string Id { get; private set; }

        /// <summary>False while the sim scene is only a backdrop (main menu): nothing is logged.</summary>
        public static bool Recording { get; set; } = true;

        public static string Folder => Path.Combine(Application.persistentDataPath, "WheelchairSim");

        /// <summary>The interface being tested: the active scene (the sim scene is loaded additively next to it).</summary>
        public static string InterfaceName => SceneManager.GetActiveScene().name;

        /// <summary>Starts a new run id, e.g. <c>Interface1_20261018_143005</c>.</summary>
        public static string New()
        {
            Id = $"{InterfaceName}_{DateTime.Now:yyyyMMdd_HHmmss}";
            Directory.CreateDirectory(Folder);
            return Id;
        }

        /// <summary>Path of a CSV file for the current run, e.g. suffix "_summary".</summary>
        public static string PathFor(string suffix)
        {
            if (string.IsNullOrEmpty(Id))
            {
                New();
            }
            return Path.Combine(Folder, $"{Id}{suffix}.csv");
        }
    }
}
