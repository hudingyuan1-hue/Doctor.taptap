using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Doctor
{
    // Only starts with explicit -doctorValidate. Uses real engine frames and physics.
    public sealed class RuntimeValidation : MonoBehaviour
    {
        IEnumerator Start()
        {
            var game=GetComponent<DoctorGame>();
            yield return null;
            string output=Path.Combine(Application.dataPath,"../Validation");Directory.CreateDirectory(output);
            var probe=GameObject.CreatePrimitive(PrimitiveType.Cube);probe.transform.position=new Vector3(0,2,0);probe.AddComponent<Rigidbody>();
            yield return new WaitForFixedUpdate();
            game.SetMenuForValidation(true);
            var before=probe.transform.position;float world=game.Model.S.worldSeconds;
            yield return new WaitForSecondsRealtime(.4f);
            bool paused=before==probe.transform.position && game.Model.S.worldSeconds==world;
            game.SetMenuForValidation(false);
            yield return new WaitForSeconds(.5f);
            bool resumed=probe.transform.position!=before && game.Model.S.worldSeconds>world;
            ScreenCapture.CaptureScreenshot(Path.Combine(output,"runtime.png"));
            yield return new WaitForEndOfFrame();
            File.WriteAllText(Path.Combine(output,"runtime-result.json"),"{\"engine\":\""+Application.unityVersion+"\",\"pausePhysicsAndClock\":"+paused.ToString().ToLower()+",\"resume\":"+resumed.ToString().ToLower()+"}");
            Debug.Log("DOCTOR_RUNTIME_VALIDATION pause="+paused+" resume="+resumed);
            if(Application.isBatchMode)Application.Quit(paused&&resumed?0:2);
        }
    }
}
