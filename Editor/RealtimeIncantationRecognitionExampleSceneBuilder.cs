using H1M4W4R1.Incantia.Examples;
using H1M4W4R1.Incantia.Integration.QuinAI;
using LeastSquares.Undertone;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace H1M4W4R1.Incantia.Editor
{
    /// <summary>Creates the self-contained Incantia real-time Whisper recognition demonstration scene.</summary>
    public static class RealtimeIncantationRecognitionExampleSceneBuilder
    {
        private const string ScenePath = "Assets/H1M4W4R1/Incantia/Examples/Scenes/RealtimeIncantationRecognitionExample.unity";

        [MenuItem("Incantia/Create Realtime Recognition Example Scene")]
        public static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("Incantia Realtime Recognition Example");

            GameObject speechEngineObject = new GameObject("Quin.AI Speech Engine");
            speechEngineObject.transform.SetParent(root.transform, false);
            SpeechEngine speechEngine = speechEngineObject.AddComponent<SpeechEngine>();
            speechEngine.SelectedModel = "whisper-tiny.en";
            speechEngine.SelectedLanguage = "en";
            speechEngine.TranslateToEnglish = false;
            speechEngine.NumOfBeams = 1;
            speechEngine.Verbose = true;

            GameObject transcriberObject = new GameObject("Quin.AI Incantation Transcriber");
            transcriberObject.transform.SetParent(root.transform, false);
            QuinAiIncantationTranscriber transcriber = transcriberObject.AddComponent<QuinAiIncantationTranscriber>();
            transcriber.SetEngine(speechEngine);

            GameObject controllerObject = new GameObject("Realtime Incantation Recognition UI");
            controllerObject.transform.SetParent(root.transform, false);
            RealtimeIncantationRecognitionExampleController controller = controllerObject.AddComponent<RealtimeIncantationRecognitionExampleController>();
            controller.SetTranscriber(transcriber);
            controller.BuildUserInterface();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
        }
    }
}
