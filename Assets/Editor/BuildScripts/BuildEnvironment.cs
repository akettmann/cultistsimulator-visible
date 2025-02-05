﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SecretHistories.Utility;
using JetBrains.Annotations;

using UnityEditor;
using UnityEngine;
using SecretHistories.Enums;

namespace SecretHistories.Editor.BuildScripts
{
    public class BuildEnvironment
    {
        public string BuildRoot { get; private set; }
        private const string BASE_OS_BUILDS = "BASE_OS_BUILDS";
        private const string GAME_ID_TOKEN = "GAMEID";
        public GameId Game { get; private set; }


        public BuildEnvironment(GameId game, string root)
        {
            Game= game;
            BuildRoot = root.Replace(GAME_ID_TOKEN, Game.ToString());
        }

        public string GetBaseBuildsPath()
        {
            return NoonUtility.JoinPaths(BuildRoot, BASE_OS_BUILDS);
        }

        public string GetProductWithOSBuildPath(BuildProduct p, BuildOS o)
        {
            string path= NoonUtility.JoinPaths(GetBaseBuildsPath(), p.GetRelativePath(), o.OSId.ToString());
            path = path.Replace(GAME_ID_TOKEN, p.GetGameId().ToString().ToLower());
            return NoonUtility.JoinPaths(GetBaseBuildsPath(), p.GetRelativePath(), o.OSId.ToString());
        }

        public void Log(string message)
        {
            //not using NoonUtility.Log, because we don't want to put messages in the in-game scene file
            Debug.Log(message);
        }

        public void LogError(string message)
        {
            //not using NoonUtility.Log, because we don't want to put messages in the in-game scene file
            Debug.LogWarning(message);
        }

        public void DeleteProductWithOSBuildPath(BuildProduct p, BuildOS o)
        {
            // Clear the build directory of any of the intermediate results of a previous build
            DirectoryInfo ProductOSDir = new DirectoryInfo(GetProductWithOSBuildPath(p,o));

            if(ProductOSDir.Exists)
            {
                foreach (var file in ProductOSDir.GetFiles())
                    File.Delete(file.FullName);
                foreach (var directory in ProductOSDir.GetDirectories())
                    Directory.Delete(directory.FullName, true);
            }
        }

    }
}
