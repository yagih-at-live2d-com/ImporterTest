using System;
using System.Collections.Generic;
using System.Linq;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.MotionFade;
using UnityEditor;
using UnityEngine;

namespace Live2D.Cubism.Editor.Importers
{
    /// <summary>
    /// Utility methods for Cubism importers.
    /// </summary>
    internal static class ImporterUtility
    {
        /// <summary>
        /// Deduplicate and order asset paths so .model3.json files are handled first.
        /// </summary>
        /// <param name="assetPaths">Collection of asset paths.</param>
        /// <returns>Ordered and deduplicated asset paths.</returns>
        public static IEnumerable<string> OrderCubismAssetPaths(IEnumerable<string> assetPaths)
        {
            return assetPaths
                .Distinct()
                .OrderBy(path => path.EndsWith(".model3.json") ? 0 : 1)
                .ThenBy(path => path);
        }

        // Priority-based importer event registration
        private static readonly SortedDictionary<int, List<CubismImporter.ModelImportListener>> ModelHandlers
            = new SortedDictionary<int, List<CubismImporter.ModelImportListener>>();
        private static readonly SortedDictionary<int, List<CubismImporter.MotionImportHandler>> MotionHandlers
            = new SortedDictionary<int, List<CubismImporter.MotionImportHandler>>();

        static ImporterUtility()
        {
            CubismImporter.OnDidImportModel += InvokeModelHandlers;
            CubismImporter.OnDidImportMotion += InvokeMotionHandlers;
        }

        public static void RegisterModelImportHandler(CubismImporter.ModelImportListener handler, int priority)
        {
            if (!ModelHandlers.TryGetValue(priority, out var list))
            {
                list = new List<CubismImporter.ModelImportListener>();
                ModelHandlers[priority] = list;
            }
            list.Add(handler);
        }

        public static void RegisterMotionImportHandler(CubismImporter.MotionImportHandler handler, int priority)
        {
            if (!MotionHandlers.TryGetValue(priority, out var list))
            {
                list = new List<CubismImporter.MotionImportHandler>();
                MotionHandlers[priority] = list;
            }
            list.Add(handler);
        }

        private static void InvokeModelHandlers(CubismModel3JsonImporter importer, CubismModel model)
        {
            foreach (var pair in ModelHandlers)
            {
                foreach (var handler in pair.Value)
                {
                    handler?.Invoke(importer, model);
                }
            }
        }

        private static void InvokeMotionHandlers(CubismMotion3JsonImporter importer, AnimationClip clip)
        {
            foreach (var pair in MotionHandlers)
            {
                foreach (var handler in pair.Value)
                {
                    handler?.Invoke(importer, clip);
                }
            }
        }

        /// <summary>
        /// Load or create a fade motion list at the given path.
        /// </summary>
        public static CubismFadeMotionList GetOrCreateFadeMotionList(string fadeMotionListPath)
        {
            var assetList = CubismCreatedAssetList.GetInstance();
            var index = assetList.AssetPaths.Contains(fadeMotionListPath)
                ? assetList.AssetPaths.IndexOf(fadeMotionListPath)
                : -1;

            CubismFadeMotionList fadeMotions;
            if (index < 0)
            {
                fadeMotions = AssetDatabase.LoadAssetAtPath<CubismFadeMotionList>(fadeMotionListPath);
                if (fadeMotions == null)
                {
                    fadeMotions = ScriptableObject.CreateInstance<CubismFadeMotionList>();
                    fadeMotions.MotionInstanceIds = Array.Empty<int>();
                    fadeMotions.CubismFadeMotionObjects = Array.Empty<CubismFadeMotionData>();
                    AssetDatabase.CreateAsset(fadeMotions, fadeMotionListPath);
                }

                assetList.Assets.Add(fadeMotions);
                assetList.AssetPaths.Add(fadeMotionListPath);
                assetList.IsImporterDirties.Add(true);
            }
            else
            {
                fadeMotions = (CubismFadeMotionList)assetList.Assets[index];
            }

            return fadeMotions;
        }

        /// <summary>
        /// Ensure an InstanceId animation event exists on the clip.
        /// </summary>
        public static void EnsureInstanceIdEvent(AnimationClip clip, int instanceId)
        {
            var events = AnimationUtility.GetAnimationEvents(clip);
            var index = Array.FindIndex(events, e => e.functionName == "InstanceId");
            if (index == -1)
            {
                index = events.Length;
                Array.Resize(ref events, events.Length + 1);
                events[index] = new AnimationEvent();
            }

            events[index].time = 0f;
            events[index].functionName = "InstanceId";
            events[index].intParameter = instanceId;
            events[index].messageOptions = SendMessageOptions.DontRequireReceiver;

            AnimationUtility.SetAnimationEvents(clip, events);
        }
    }
}
