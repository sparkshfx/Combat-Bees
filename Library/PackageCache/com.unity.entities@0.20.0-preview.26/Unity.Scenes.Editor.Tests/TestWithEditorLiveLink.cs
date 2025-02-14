﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Entities.Hybrid.Tests;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Scenes.Editor.Tests
{
    [Serializable]
    public struct TestWithEditorLiveLink {
        [SerializeField] public TestWithTempAssets Assets;
        [SerializeField] public TestWithCustomDefaultGameObjectInjectionWorld DefaultWorld;
        [SerializeField] public TestWithSubScenes SubSceneTest;
        [SerializeField] public TestWithLiveConversion LiveConversionTest;

        [SerializeField] EnterPlayModeOptions m_enterPlayModeOptions;
        [SerializeField] bool m_useEnterPlayerModeOptions;

        public bool IsSetUp => Assets.TempAssetDir != null;

        public bool OneTimeSetUp()
        {
            if (IsSetUp || EditorApplication.isPlaying)
            {
                // This setup code is run again when we switch to playmode from the editor
                return false;
            }

            // Create a temporary folder for test assets
            Assets.SetUp();
            DefaultWorld.Setup();
            SubSceneTest.Setup();
            LiveConversionTest.Setup();
            m_enterPlayModeOptions = EditorSettings.enterPlayModeOptions;
            m_useEnterPlayerModeOptions = EditorSettings.enterPlayModeOptionsEnabled;

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            return true;
        }

        public void OneTimeTearDown()
        {
            if (EditorApplication.isPlaying)
                return;
            // Clean up all test assets
            Assets.TearDown();
            DefaultWorld.TearDown();
            SubSceneTest.TearDown();
            LiveConversionTest.TearDown();
            EditorSettings.enterPlayModeOptions = m_enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = m_useEnterPlayerModeOptions;
        }

        public void SetUp()
        {
            if (EditorApplication.isPlaying)
                return;
            World.DefaultGameObjectInjectionWorld?.Dispose();
            World.DefaultGameObjectInjectionWorld = null;
        }

        public void OpenAllSubScenes() => SubSceneInspectorUtility.EditScene(SubScene.AllSubScenes.ToArray());

        public Scene CreateTmpScene() => SubSceneTestsHelper.CreateScene(Assets.GetNextPath() + ".unity");

        public SubScene CreateSubSceneFromObjects(string name, bool keepOpen, Func<List<GameObject>> createObjects) =>
            SubSceneTestsHelper.CreateSubSceneInSceneFromObjects(name, keepOpen, CreateTmpScene(), createObjects);

        public SubScene CreateEmptySubScene(string name, bool keepOpen) => CreateSubSceneFromObjects(name, keepOpen, null);

        public World GetLiveLinkWorldForEditMode() => GetLiveLinkWorld(Mode.Edit, true);

        public World GetLiveLinkWorld(Mode playmode, bool removeWorldFromPlayerLoop = true)
        {
            if (playmode == Mode.Edit)
                DefaultWorldInitialization.DefaultLazyEditModeInitialize();

            var world = World.DefaultGameObjectInjectionWorld;
            if (removeWorldFromPlayerLoop)
            {
                // This should be a fresh world, but make sure that it is not part of the player loop so we have manual
                // control on its updates.
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
            }

            return world;
        }

        public IEditModeTestYieldInstruction GetEnterPlayMode(Mode playmode)
        {
            if (playmode == Mode.Play)
                return new EnterPlayMode();
            // ensure that the editor world is initialized
            var world = GetLiveLinkWorld(playmode);
            world.Update();
            return null;
        }

        public IEnumerator UpdateEditorAndWorld(World w)
        {
            yield return null;
            w.Update();
        }

        public enum Mode
        {
            Play,
            Edit
        }
    }
}
