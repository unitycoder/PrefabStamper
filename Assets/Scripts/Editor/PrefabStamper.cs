// PrefabStamper by http://www.eastshade.com/

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace EastshadeStudio
{
    public class PrefabStamper : EditorWindow
    {
        static LayerMask terrainLayerMask = 1 << LayerMask.NameToLayer("Terrain"); // this needs to be different from stamped objects

        const string stampDataFolder = "/Text/Stamps/";

        StampData Stamp;
        List<Transform> stampedTransforms = new List<Transform>();

        Vector3 lastHitPoint;
        Vector3 lastHitNormal;
        bool useNormal = true;
        bool randomYRot = true;
        bool sizeVariation = true;
        float smallest = .85f;
        float largest = 1.15f;
        static PrefabStamper instance;

        [MenuItem("Tools/Prefab Stamper/Drop Selection to Terrain", false, 2)]
        static void DropSelectedToTerrain()
        {
            Undo.RecordObjects(Selection.transforms, "Drop to Terrain");
            DropToTerrain(Selection.transforms);
        }

        static void DropToTerrain(Transform[] toDrop)
        {

            foreach (Transform t in toDrop)
            {

                RaycastHit hit;
                if (Physics.Raycast(t.position + (Vector3.up * 7), Vector3.down, out hit, Mathf.Infinity, terrainLayerMask))
                {

                    t.position = hit.point;

                    if (t.tag != "Trees")
                    {
                        Vector3 left = Vector3.Cross(t.forward, hit.normal);//note: unity use left-hand system, and Vector3.Cross obey left-hand rule.
                        Vector3 newForward = Vector3.Cross(hit.normal, left);
                        Quaternion newRotation = Quaternion.LookRotation(newForward, hit.normal);
                        t.rotation = newRotation;
                    }
                }
            }

        }

        [MenuItem("Tools/Prefab Stamper/Create Stamp from Selection", false, 0)]
        static void CreateStamp()
        {
            if (Selection.gameObjects.Length == 0)
            {
                return;
            }

            // extra check: selected objects must be in scene
            if (Selection.activeGameObject.activeInHierarchy == true)
            {
                // In Hierarchy, ok
            } else
            {
                // In Project View, cannot use
                Debug.LogError("Select prefabbed objects from scene/hierarcy, not from the project folder..");
                return;
            }

            foreach (GameObject go in Selection.gameObjects)
            {
                if (!go.activeInHierarchy) continue;

                GameObject parentPrefab = PrefabUtility.GetPrefabParent(go) as GameObject;

                if (parentPrefab == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(parentPrefab)))
                {
                    Debug.LogError("All selected GameObjects must be prefabs to make a stamp.");
                    return;
                }
            }

            StampData stampData = new StampData();
            Vector3 center = Vector3.zero;

            foreach (GameObject go in Selection.gameObjects)
            {

                if (!go.activeInHierarchy) continue;

                StampChild sc = new StampChild();
                sc.PrefabPath = AssetDatabase.GetAssetPath(PrefabUtility.GetPrefabParent(go));
                sc.position = go.transform.position;
                sc.eulerAngles = go.transform.eulerAngles;
                sc.scale = go.transform.localScale;
                center += sc.position;
                stampData.prefabs.Add(sc);
            }

            stampData.center = center / stampData.prefabs.Count;

            // string jsonString = Serializer.Serialize(stampData, true); // EasyJSON plugin
            string jsonString = JsonUtility.ToJson(stampData, true);

            string assetPath = stampDataFolder + "new stamp.JSON";
            string fullPath = Application.dataPath + assetPath;

            if (Directory.Exists(Path.GetDirectoryName(fullPath)) == true)
            {
                StreamWriter stream = File.CreateText(fullPath);
                stream.WriteLine(jsonString);
                stream.Close();

                Debug.Log("Created new stamp at " + fullPath);

                AssetDatabase.Refresh();
            } else
            {
                Debug.LogError("Missing stamp data folder: " + fullPath);
            }
        }

        [MenuItem("Assets/Stamp")]
        static void Arm()
        {
            Init();
            Object obj = Selection.activeObject;
            if (obj.GetType() == typeof(TextAsset))
            {
                // instance.Stamp = Serializer.Deserialize<StampData>((Selection.activeObject as TextAsset).text); // EasyJson plugin
                instance.Stamp = JsonUtility.FromJson<StampData>((Selection.activeObject as TextAsset).text);
            } else
            { // we will generate a stamp from this single prefab
                StampData singleStamp = new StampData();
                StampChild singleChild = new StampChild();
                singleChild.PrefabPath = AssetDatabase.GetAssetPath(obj);
                singleStamp.prefabs.Add(singleChild);
                instance.Stamp = singleStamp;
            }
        }

        [MenuItem("Assets/Stamp", true)]
        static bool stampCheck()
        {
            Object candidate = Selection.activeObject;

            if (candidate == null)
            {
                return false;
            } else if (Selection.activeObject.GetType() == typeof(GameObject))
            {
                return true;
            } else if (Selection.activeObject.GetType() == typeof(TextAsset))
            {
                try
                {
                    // if (Serializer.Deserialize<StampData>((candidate as TextAsset).text) == null) // EasyJson plugin
                    if (JsonUtility.FromJson((candidate as TextAsset).text, typeof(StampData)) == null)
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        public static void Init()
        {
            // Get existing open window or if none, make a new one:
            PrefabStamper window = (PrefabStamper)GetWindow(typeof(PrefabStamper));
            window.Show();
            window.titleContent.text = "PrefabStamper";
            window.minSize = new Vector2(238, 128);
            window.maxSize = new Vector2(240, 130);
            instance = window;
        }

        void OnEnable()
        {
            SceneView.onSceneGUIDelegate += OnScene;
        }
        void OnDisable()
        {
            SceneView.onSceneGUIDelegate -= OnScene;
        }

        void OnGUI()
        {
            if (instance == null)
            {
                instance = (PrefabStamper)GetWindow(typeof(PrefabStamper));
            }
            useNormal = EditorGUILayout.Toggle("Use Normal", useNormal);
            randomYRot = EditorGUILayout.Toggle("Randomize Y Rotation", randomYRot);
            sizeVariation = EditorGUILayout.Toggle("Randomize Scale", sizeVariation);
            if (sizeVariation == true)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Smallest/Largest", GUILayout.MaxWidth(120));
                smallest = EditorGUILayout.FloatField(smallest);
                largest = EditorGUILayout.FloatField(largest);
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Done", GUILayout.MinHeight(30)))
            {
                instance.Close();
            }
        }

        // main loop
        static void OnScene(SceneView sceneview)
        {

            if (Event.current.alt) // lat key is down
            {
                return;
            }

            if (instance == null)
            {
                instance = (PrefabStamper)GetWindow(typeof(PrefabStamper));
            }

            if (instance.Stamp == null) return;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                LayStamp();
                Event.current.Use();
            }

            if (Event.current.type == EventType.MouseUp)
            {
                instance.stampedTransforms.Clear();
            }

            if (instance.stampedTransforms.Count > 0)
            {
                UpdateStamped();
            }

            if (Event.current.type == EventType.Layout)
            { // stop stuff from selecting
                HandleUtility.AddDefaultControl(0);
            }
        }

        static void UpdateStamped()
        {

            float scroll = (Event.current.type == EventType.scrollWheel) ? Event.current.delta.y * 4f : 0f;

            if (Event.current.type == EventType.scrollWheel)
            {
                Event.current.Use();
            }

            foreach (Transform t in instance.stampedTransforms)
            {
                t.RotateAround(instance.lastHitPoint, instance.lastHitNormal, scroll);
            }

            Vector2 guiPosition = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            //Physics.Raycast(ray); //?

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayerMask))
            {
                foreach (Transform t in instance.stampedTransforms)
                {
                    t.position += hit.point - instance.lastHitPoint;
                }
                instance.lastHitPoint = hit.point;
                instance.lastHitNormal = hit.normal;
            }

            DropToTerrain(instance.stampedTransforms.ToArray());
        }

        static int undoGroupIncrementer = 0;

        static void LayStamp()
        {

            Undo.IncrementCurrentGroup();

            GameObject newStampsGroup = GameObject.Find("new stamps");
            if (newStampsGroup == null || newStampsGroup.transform.root != newStampsGroup.transform)
                newStampsGroup = new GameObject("new stamps");

            Vector2 guiPosition = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(guiPosition);
            Physics.Raycast(ray);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, terrainLayerMask))
            {
                instance.lastHitPoint = hit.point;
                instance.lastHitNormal = hit.normal;
                instance.stampedTransforms.Clear();

                foreach (StampChild child in instance.Stamp.prefabs)
                {
                    var obj = AssetDatabase.LoadAssetAtPath(child.PrefabPath, typeof(GameObject));
                    GameObject newGo = PrefabUtility.InstantiatePrefab(obj) as GameObject;
                    //newGo.name = newGo.name.Split('(')[0];
                    newGo.transform.parent = newStampsGroup.transform;
                    newGo.transform.position = (child.position - instance.Stamp.center) + hit.point;
                    newGo.transform.eulerAngles = child.eulerAngles;
                    newGo.transform.localScale = child.scale;

                    if (instance.sizeVariation == true)
                    {
                        newGo.transform.localScale *= Random.Range(instance.smallest, instance.largest);
                    }

                    instance.stampedTransforms.Add(newGo.transform);
                    Undo.RegisterCreatedObjectUndo(newGo, "lay stamp" + undoGroupIncrementer.ToString());
                }

                undoGroupIncrementer++;
            }
        }

        [System.Serializable]
        class StampData
        {
            public Vector3 center = Vector3.zero;
            public List<StampChild> prefabs = new List<StampChild>();
        }

        [System.Serializable]
        class StampChild
        {
            public string PrefabPath;
            public Vector3 position = Vector3.zero;
            public Vector3 eulerAngles = Vector3.zero;
            public Vector3 scale = Vector3.one;
        }
    }
}
