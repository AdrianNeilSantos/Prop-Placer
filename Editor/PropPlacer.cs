using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Linq;



public class PropPlacer : EditorWindow
{
    const float TAU = Mathf.PI * 2;
    private const float GUI_SPACING = 10f;
    // appearance
    public Color color = Color.white;
    [Range(0f, 1f)]
    public float opacity = 1f;
    [Range(1f, 10f)]
    public float thickness = 1f;
    public float radius = 2f;
    public int spawnCount = 8;
    public GameObject spawnPrefab = null;
    public Material previewMaterial;


    SerializedObject so;
    SerializedProperty propRadius; 
    SerializedProperty propSpawnCount;
    SerializedProperty propColor;
    SerializedProperty propOpacity;
    SerializedProperty propThickness;
    SerializedProperty propSpawnPrefab;
    SerializedProperty propPreviewMaterial;

    public struct RandomData{
        public Vector2 pointInDisc;
        public float randAngleDeg;
        public void SetRandomValues(){
            pointInDisc = Random.insideUnitCircle;
            randAngleDeg = Random.value * 360;
        }
    }

    public struct TangentSpace{
        public Vector3 origin;
        public Vector3 normal;
        public Vector3 tangent;
        public Vector3 bitangent;
    }


    RandomData[] randPoints;

    GameObject[] prefabsList;
    List<Pose> hitPoses = new List<Pose>();


    [MenuItem("Tools/Prop Placer")]
    public static void ShowWindow() => GetWindow<PropPlacer>("Prop Placer");


    private void OnEnable() {
        so = new SerializedObject(this);
        propColor = so.FindProperty("color");
        propOpacity = so.FindProperty("opacity");
        propThickness = so.FindProperty("thickness");
        propRadius = so.FindProperty("radius");
        propSpawnCount = so.FindProperty("spawnCount");
        propSpawnPrefab = so.FindProperty("spawnPrefab");
        propPreviewMaterial = so.FindProperty("previewMaterial");


        string folderPath = "Assets/Prefabs";

        // Check if the folder already exists, and if not, create it.
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // load prefabs list
        string[] guids = AssetDatabase.FindAssets("t:prefab", new[]{folderPath});
        IEnumerable<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath);
        prefabsList = paths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();


        // Initialize spawn prefab
        if(prefabsList.Length > 0){
            spawnPrefab = prefabsList[0];
        }


        LoadConfigurations();


        GenerateRandomPoints();
        SceneView.duringSceneGui += DuringSceneGUI;



    } 

    private void OnDisable() {
        SaveConfigurations();
        SceneView.duringSceneGui -= DuringSceneGUI;
    }


    void DuringSceneGUI(SceneView sceneView){


        DrawPrefabsSelection();


        Handles.zTest = CompareFunction.LessEqual;
        Transform camTf = sceneView.camera.transform;

        // Repaints on mouse move
        if(Event.current.type == EventType.MouseMove){
            sceneView.Repaint();
        }

        ControlRadius();
        ControlSpawnCount();

        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        Handles.color = new Color(color.r, color.g, color.b, opacity);
        if(Physics.Raycast(ray, out RaycastHit hit)){
            // Setting up tanget space
            TangentSpace hitTangentSpace = new TangentSpace();
            hitTangentSpace.origin = hit.point;
            hitTangentSpace.normal = hit.normal;
            hitTangentSpace.tangent = Vector3.Cross(hitTangentSpace.normal, camTf.up).normalized;
            hitTangentSpace.bitangent = Vector3.Cross(hitTangentSpace.normal, hitTangentSpace.tangent);


            DrawPrefabs(hitTangentSpace);
            DrawRadius(hitTangentSpace);
            // spawn on press
            if(Event.current.keyCode == KeyCode.G && Event.current.type == EventType.KeyDown ){
                SpawnObjects(hitPoses);
            }
        }

        Handles.color = Color.white;

    }


    private void OnGUI() {
        so.Update();

        GUILayout.Space(GUI_SPACING);

        // Category 0: Descriptions
        using(new GUILayout.VerticalScope(EditorStyles.helpBox)){
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Change Radius: Alt + Scroll Wheel");
            EditorGUILayout.LabelField("Change Spawn Count: Ctrl + Scroll Wheel ");
            EditorGUILayout.LabelField("Spawn Prefab: G");
        }

        GUILayout.Space(GUI_SPACING); 
        // Category 1: Appearance
        using(new GUILayout.VerticalScope(EditorStyles.helpBox)){
            EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propColor);
            EditorGUILayout.PropertyField(propOpacity);
            EditorGUILayout.PropertyField(propThickness);
            // EditorGUILayout.PropertyField(propPreviewMaterial);
        }

        // Category 2: Tool Properties
        GUILayout.Space(GUI_SPACING);
        using(new GUILayout.VerticalScope(EditorStyles.helpBox)){
            EditorGUILayout.LabelField("Tool Properties", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(propRadius);
            propRadius.floatValue = Mathf.Max(1f, propRadius.floatValue);
            EditorGUILayout.PropertyField(propSpawnCount);
            propSpawnCount.intValue = Mathf.Max(1, propSpawnCount.intValue);
            // EditorGUILayout.PropertyField(propSpawnPrefab);
        }

        if(so.ApplyModifiedProperties()){
            GenerateRandomPoints();
            SceneView.RepaintAll();
        }

        // If left mouse button is clicked in editor
        if(Event.current.type == EventType.MouseDown && Event.current.button == 0){
            GUI.FocusControl(null); // remove focus
            Repaint();
        }

    }


    // Helper Functions
    void GenerateRandomPoints(){
        randPoints = new RandomData[spawnCount];
        for(int i = 0; i < spawnCount; i++){
            randPoints[i].SetRandomValues();
        }
    }

    void DrawSphere(Vector3 pos){
        Handles.SphereHandleCap(-1, pos, Quaternion.identity, thickness/10f, EventType.Repaint);
    }


    void SpawnObjects(List<Pose> hitPoses){
        if(spawnPrefab == null){
            return;
        }

        foreach(Pose pose in hitPoses){
            // spawn prefab
            GameObject spawnedPrefab = (GameObject) PrefabUtility.InstantiatePrefab(spawnPrefab);
            Undo.RegisterCreatedObjectUndo(spawnedPrefab, "spawned prefab");
            spawnedPrefab.transform.position = pose.position;
            spawnedPrefab.transform.rotation = pose.rotation;
            // Parent the spawned prefab to the currently selected object (if any)
            GameObject parentObject = Selection.activeGameObject;
            if (parentObject != null)
            {
                Undo.SetTransformParent(spawnedPrefab.transform, parentObject.transform, "Parent Prefab");
            }
        }
        GenerateRandomPoints(); // update points
    }


    Ray GetTangentRay(Vector2 tangentSpacePos, TangentSpace hitTangentSpace){
        Vector3 rayOrigin = hitTangentSpace.origin + (hitTangentSpace.tangent * tangentSpacePos.x + hitTangentSpace.bitangent * tangentSpacePos.y) * radius; // converting to world position
        rayOrigin += hitTangentSpace.normal * 2; // offset margin to make the point higher in terms of normal
        Vector3 rayDirection = -hitTangentSpace.normal;
        Ray ptRay = new Ray(rayOrigin, rayDirection);
        return ptRay;
    }

    void DrawRadius(TangentSpace hitTangentSpace){
        Handles.color = new Color(color.r, color.g, color.b, opacity);
        // Draw circle adapted to the terrain
        const int circleDetail = 128;
        Vector3[] ringPoints = new Vector3[circleDetail];
        for(int i = 0; i < circleDetail; i++){
            float t = i/((float) circleDetail - 1); // go back to 0/1 position
            float angRad = t * TAU;
            Vector2 dir = new Vector2(Mathf.Cos(angRad), Mathf.Sin(angRad));
            Ray r = GetTangentRay(dir, hitTangentSpace);
            if(Physics.Raycast(r, out RaycastHit cHit)){
                ringPoints[i] = cHit.point + cHit.normal * 0.02f;
            }else{
                ringPoints[i] = r.origin;
            }
        }
        // Drawing radius
        Handles.DrawAAPolyLine(thickness, ringPoints);
        Handles.color = Color.white;
    }

    void DrawPrefabs(TangentSpace hitTangentSpace){
        // Drawing points
        Handles.color = new Color(color.r, color.g, color.b, opacity);
        hitPoses = new List<Pose>();

        foreach(RandomData randomDataPoint in randPoints){
            // create ray for point
            Ray ptRay = GetTangentRay(randomDataPoint.pointInDisc, hitTangentSpace);
            
            // Raycast to find point on surface
            if(Physics.Raycast(ptRay, out RaycastHit ptHit)){

                // calculate rotation and assign to pose together with position
                Quaternion randRot = Quaternion.Euler(0f, 0f, randomDataPoint.randAngleDeg);
                Quaternion rot = Quaternion.LookRotation(ptHit.normal) *  (randRot * Quaternion.Euler(90f, 0f, 0f));
                Pose pose = new Pose(ptHit.point, rot);

                hitPoses.Add(pose);

                // draw sphere and normal on surface
                // DrawSphere(ptHit.point);
                // Handles.DrawAAPolyLine(ptHit.point, ptHit.point + ptHit.normal);

                // mesh
                if(spawnPrefab != null){
                    Matrix4x4 poseToWorldMtx = Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one);
                    MeshFilter[] filters = spawnPrefab.GetComponentsInChildren<MeshFilter>();
                    foreach(MeshFilter filter in filters){
                        Matrix4x4 childToPoseMtx = filter.transform.localToWorldMatrix;
                        Matrix4x4 childToWorldMtx = poseToWorldMtx * childToPoseMtx;

                        Mesh mesh = filter.sharedMesh;
                        Material mat = filter.GetComponent<MeshRenderer>().sharedMaterial;
                        mat.SetPass(0);
                        Graphics.DrawMeshNow(mesh, childToWorldMtx);
                    }
                }

            }
        }    
        Handles.color = Color.white;
    }


    void DrawPrefabsSelection(){
        // UI
        Handles.BeginGUI();
        Rect rect = new Rect(8,8,64,64);
        for(int i = 0; i < prefabsList.Length; i++){
            GameObject prefab = prefabsList[i];
            Texture icon = AssetPreview.GetAssetPreview(prefab);

            bool isSelected = spawnPrefab == prefab;

            if(GUI.Toggle(rect,  isSelected, new GUIContent(icon))){
                spawnPrefab = prefab;
            }

            rect.y += rect.height + 2;
        }


        Handles.EndGUI();
    }

    

    void ControlRadius(){
        bool holdingAlt = (Event.current.modifiers & EventModifiers.Alt) != 0;

        // Change radius
        if(Event.current.type == EventType.ScrollWheel && holdingAlt){
            float scrollDir = Mathf.Sign(Event.current.delta.y);
            so.Update();
            float scaleSpeed = 0.05f;
            propRadius.floatValue *= 1f + scrollDir * scaleSpeed; 
            so.ApplyModifiedProperties();
            Repaint(); // updates the editor window
            Event.current.Use(); // consume the event
        }
    }

    void ControlSpawnCount(){
        bool holdingCtrl = (Event.current.modifiers & EventModifiers.Control) != 0;
        // Change spawnCount
        if(Event.current.type == EventType.ScrollWheel && holdingCtrl){
            float scrollDir = Mathf.Sign(Event.current.delta.y);
            so.Update();
            propSpawnCount.intValue += 1 * (int) scrollDir; 
            GenerateRandomPoints();
            so.ApplyModifiedProperties();
            Repaint(); // updates the editor window
            Event.current.Use(); // consume the event
        }
    }


    private void LoadConfigurations(){
        color = new Color(EditorPrefs.GetFloat("PROP_PLACER_color_R", 1f),
                    EditorPrefs.GetFloat("PROP_PLACER_color_G", 1f),
                    EditorPrefs.GetFloat("PROP_PLACER_color_B", 1f),
                    1f);
        opacity = EditorPrefs.GetFloat("PROP_PLACER_opacity", 1f);
        thickness = EditorPrefs.GetFloat("PROP_PLACER_thickness", 4f);
        radius = EditorPrefs.GetFloat("PROP_PLACER_radius", 5f);
        spawnCount = EditorPrefs.GetInt("PROP_PLACER_spawn_count", 4);
    }


    private void SaveConfigurations(){
        EditorPrefs.SetFloat("PROP_PLACER_color_R", color.r);
        EditorPrefs.SetFloat("PROP_PLACER_color_G", color.g);
        EditorPrefs.SetFloat("PROP_PLACER_color_B", color.b);
        EditorPrefs.SetFloat("PROP_PLACER_opacity", opacity);
        EditorPrefs.SetFloat("PROP_PLACER_thickness", thickness);
        EditorPrefs.SetFloat("PROP_PLACER_radius", radius);
        EditorPrefs.SetInt("PROP_PLACER_spawn_count", spawnCount);


    }

}
