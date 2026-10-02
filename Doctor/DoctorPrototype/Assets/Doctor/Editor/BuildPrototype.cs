using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Doctor.Editor
{
    public static class BuildPrototype
    {
        [MenuItem("Doctor/Open First Room")]
        public static void Open(){EditorSceneManager.OpenScene("Assets/Doctor/Scenes/FirstRoom.unity");}
        [MenuItem("Doctor/Build Windows Prototype")]
        public static void Build()
        {
            Open();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes=new[]{"Assets/Doctor/Scenes/FirstRoom.unity"},locationPathName="Build/Doctor.exe",
                target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Windows build failed: "+report.summary.result);
        }
        public static void VerifyScene()
        {
            Open();var game=Object.FindFirstObjectByType<DoctorGame>();
            if(game==null || game.actor==null || game.cat==null || game.mother==null || game.roomCamera==null || game.damageMarks.Length!=10)
                throw new System.Exception("Scene wiring incomplete");
            Debug.Log("DOCTOR_SCENE_VALIDATED");
        }
    }
}
