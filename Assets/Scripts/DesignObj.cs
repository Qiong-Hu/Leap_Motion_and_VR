// This script is attached to Prefab obj CreationObj
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using System;
using System.Text;
using System.IO;
using Parabox.STL;
using STLImporter;
using SimpleJSON;

namespace FARVR.Design {
    public class DesignObj : MonoBehaviour {
        /// <summary>
        /// A Material used to display on the Prefab Object
        /// </summary>
        public Material MaterialPrefab;

        /// <summary>
        /// Text displayer prefab
        /// </summary>
        public GameObject TextDisplayer;

        /// <summary>
        /// The ID of the created obj
        /// </summary>
        private int ID;

        /// <summary>
        /// The type of obj that will be created/displayed
        /// </summary>
        private string type;

        /// <summary>
        /// The parameters defining the generated obj
        /// </summary>
        private Dictionary<string, dynamic> parameters;
        private Dictionary<string, string> paramtypes = new Dictionary<string, string>();

        /// <summary>
        /// List to store the different text display objects
        /// </summary>
        private List<GameObject> texts = new List<GameObject>();

        // For displaying parameter texts
        private string formatter = "{0}: {1}";
        private float linespacing = 0.08f;

        /// <summary>
        /// The available object names from backend design compilers
        /// used to verify the object created
        /// </summary>
        private List<string> objectList = new List<string>();
        public void RegisterNameList(List<string> nameList) {
            foreach (string name in nameList) {
                objectList.Add(name);
            }
        }

        /// <summary>
        /// The url link of backend compiler (passed by frontend during Create)
        /// </summary>
        private string urlLink = "";

        /// <summary>
        /// Auto obtain parameters from backend compiler
        /// To replace obsolete static FurnitureCatalog
        /// </summary>
        private Dictionary<string, dynamic> DefaultParameters(string url, string ftype) {
            Dictionary<string, dynamic> defaultParams = new Dictionary<string, dynamic>();

            string link = "{0}/{1}.json";
            link = string.Format(link, url, ftype);

            string jsonString = "";
            using (UnityWebRequest www = UnityWebRequest.Get(link)) {
                www.SendWebRequest();
                while (!www.isDone) ;

                if (www.isNetworkError || www.isHttpError) {
                    Debug.Log(www.error);
                }
                else {
                    jsonString = www.downloadHandler.text;
                }
            }

            if (jsonString != "") {
                JSONNode jsonNode = SimpleJSON.JSON.Parse(jsonString);

                foreach (KeyValuePair<string, JSONNode> kvp in (JSONObject)jsonNode) {
                    if (kvp.Value.Count == 1) {
                        defaultParams[kvp.Key] = float.Parse(kvp.Value["default"].Value);
                    }
                    else {
                        paramtypes[kvp.Key] = kvp.Value["paramtype"].Value;
                        if (kvp.Value["paramtype"].Value == "count") {
                            defaultParams[kvp.Key] = int.Parse(kvp.Value["default"].Value);
                        }
                        else {
                            defaultParams[kvp.Key] = float.Parse(kvp.Value["default"].Value);
                        }
                    }
                }
                return defaultParams;
            }
            else {
                Debug.Log("Fail to find json file for " + ftype + ".");
                return null;
            }
        }

        #region Creates a new design object
        /// <summary>
        /// Generates a design object by setting all the parameters in the object.
        /// </summary>
        /// <returns>RETURN CODE: 0 = Successful creation of obj; 1 = Failed to retrieve mesh from server; 2 = Invalid parameters</returns>
        /// <param name="localparameters">Parameters used to generate a mesh</param>
        /// <param name="ftype">The name of the design obj that we are to generate</param>
        /// <param name="id">A unique ID for the design obj.</param>
        // Obsolete name: MakeFurniture
        public int MakeDesign(string url, Dictionary<string, dynamic> localparameters, string ftype, int id, Vector3 location, Quaternion rotation, Vector3 scale)
        {
            urlLink = url;
            if (!VerifyDesign(ftype, localparameters))
            {
                return 2;
            }

            // Hold the name of the design obj same as type
            type = ftype;

            // Hold the id of the design obj
            ID = id;

            // Hold the parameters of the design obj
            parameters = localparameters;

            gameObject.name = type + ID.ToString();
            gameObject.AddComponent<MeshFilter>();
            gameObject.AddComponent<MeshRenderer>();

            // TODO: Add Physics collision to object
            // .gameObject.AddComponent<MeshCollider> ();
            Mesh[] meshes = GetSTL(url, ftype);
            if (meshes == null)
            {
                Debug.Log("Failed to get mesh");
                return 1;
            }
            else
            {
                gameObject.GetComponent<MeshFilter>().mesh = meshes[0];
                gameObject.AddComponent<MeshCollider>();
                gameObject.GetComponent<MeshCollider>().sharedMesh = meshes[0];
                gameObject.GetComponent<MeshCollider>().convex = true;
                gameObject.GetComponent<MeshCollider>().sharedMaterial = Resources.Load("Prefabs/DesignObjPhy") as PhysicMaterial;
                gameObject.GetComponent<MeshCollider>().cookingOptions = MeshColliderCookingOptions.InflateConvexMesh;
                Renderer rend = gameObject.GetComponent<Renderer>();
                rend.material = MaterialPrefab;

                /* Transforms the design obj based on what we have initialized it to be */
                TransformDesign(location, rotation, scale);

                /* Update: Added a Collider tag that allows us to distinguish whether an Existing design obj was hit or not */
                gameObject.tag = "Design";

                /* Add the text displays */
                int Vcounter = 1;
                foreach (KeyValuePair<string, dynamic> entry in parameters)
                {
                    GameObject textobj = Instantiate(TextDisplayer) as GameObject;
                    textobj.GetComponent<TextMesh>().transform.position = gameObject.transform.position + gameObject.transform.up * linespacing * Vcounter++;
                    textobj.GetComponent<TextMesh>().color = Color.black;
                    //textobj.GetComponent<TextMesh>().text = string.Format(formatter, entry.Key, ((int)entry.Value).ToString());
                    textobj.GetComponent<TextMesh>().transform.parent = gameObject.transform;
                    texts.Add(textobj);
                } 
            }
            return 0;
        }

        // The following are overloaded functions that act as an alternative to default parameters
        // Optional rotation AND scale
        public int MakeDesign(string url, Dictionary<string, dynamic> localparameters, string ftype, int id, Vector3 location)
        {
            return MakeDesign(url, localparameters, ftype, id, location, Quaternion.identity, new Vector3(1f, 1f, 1f));
        }

        // Optional rotation
        public int MakeDesign(string url, Dictionary<string, dynamic> localparameters, string ftype, int id, Vector3 location, Vector3 scale)
        {
            return MakeDesign(url, localparameters, ftype, id, location, Quaternion.identity, scale);
        }

        // Optional scale
        public int MakeDesign(string url, Dictionary<string, dynamic> localparameters, string ftype, int id, Vector3 location, Quaternion rotate)
        {
            return MakeDesign(url, localparameters, ftype, id, location, rotate, new Vector3(1f, 1f, 1f));
        }

        // Make design obj using catalog and predefined parameters
        /// <summary>
        /// Generates a design object by setting all the parameters in the object. 
        /// Returns the parameters for the design obj
        /// </summary>
        /// <returns>Dictionary of the parameters that were used to generate the obj; NULL = Invalid parameters</returns>
        /// <param name="ftype">The name of the design obj that we are to generate</param>
        /// <param name="id">A unique ID for the design obj.</param>
        public Dictionary<string, dynamic> MakeDesign(string url, string ftype, int id, Vector3 location, Quaternion rotation, Vector3 scale)
        {
            urlLink = url;
            //Verify that the design obj is valid
            if (!VerifyDesign(ftype))
            {
                Debug.Log("Failed to find valid design object");
                return null;
            }
            else
            {

                //If the design obj is valid
                parameters = DefaultParameters(url, ftype);

                type = ftype;
                ID = id;

                gameObject.name = type + ID.ToString();
                gameObject.AddComponent<MeshFilter>();
                gameObject.AddComponent<MeshRenderer>();

                // TODO: Add Physics collision to object
                // .gameObject.AddComponent<MeshCollider> ();
                Mesh[] meshes = GetSTL(url, ftype);
                if (meshes == null)
                {
                    Debug.Log("Failed to get mesh");
                    return null;
                }
                else
                {
                    gameObject.GetComponent<MeshFilter>().mesh = meshes[0];
                    gameObject.AddComponent<MeshCollider>();
                    gameObject.GetComponent<MeshCollider>().sharedMesh = meshes[0];
                    gameObject.GetComponent<MeshCollider>().convex = true;
                    gameObject.GetComponent<MeshCollider>().sharedMaterial = Resources.Load("Prefabs/DesignObjPhy") as PhysicMaterial;
                    gameObject.GetComponent<MeshCollider>().cookingOptions = MeshColliderCookingOptions.WeldColocatedVertices;
                    Renderer rend = gameObject.GetComponent<Renderer>();
                    rend.material = MaterialPrefab;

                    /*  Transform the design obj based on provided parameters */
                    TransformDesign(location, rotation, scale);

                    /* Update: Added a Collider tag that allows us to distinguish whether an Existing design obj was hit or not */
                    gameObject.tag = "Design";

                    /* Add the text displays */
                    int Vcounter = 1;
                    foreach (KeyValuePair<string, dynamic> entry in parameters)
                    {
                        GameObject textobj = Instantiate(TextDisplayer) as GameObject;
                        textobj.GetComponent<TextMesh>().transform.position = gameObject.transform.position + gameObject.transform.up * linespacing * Vcounter++;
                        textobj.GetComponent<TextMesh>().color = Color.black;
                        //textobj.GetComponent<TextMesh>().text = string.Format(formatter, entry.Key, ((int)entry.Value).ToString());
                        textobj.GetComponent<TextMesh>().transform.parent = gameObject.transform;
                        texts.Add(textobj);
                    }
                }
            }
            return parameters;
        }

        // The following are overloaded functions that act as an alternative to default parameters
        // Optional rotation AND scale
        public Dictionary<string, dynamic> MakeDesign(string url, string ftype, int id, Vector3 location)
        {
            return MakeDesign(url, ftype, id, location, Quaternion.identity, new Vector3(1f, 1f, 1f));
        }

        // Optional rotation
        public Dictionary<string, dynamic> MakeDesign(string url, string ftype, int id, Vector3 location, Vector3 scale)
        {
            return MakeDesign(url, ftype, id, location, Quaternion.identity, scale);
        }

        // Optional scale
        public Dictionary<string, dynamic> MakeDesign(string url, string ftype, int id, Vector3 location, Quaternion rotate)
        {
            return MakeDesign(url, ftype, id, location, rotate, new Vector3(1f, 1f, 1f));
        }
        #endregion

        /// <summary>
        /// Updates the design obj. Return and update the corresponding mesh of the object.
        /// </summary>
        /// <returns>RETURN CODE: 0 = Successfully updated design obj; 1 = Failed to retrieve mesh</returns>
        /// <param name="localparameters">Local parameters of the given design obj.</param>
        // Obsolete name: UpdateFurniture
        public int UpdateDesign(Dictionary<string, dynamic> localparameters)
        {
            parameters = localparameters;
            // Assuming we need to update the design obj
            Mesh[] meshes = GetSTL(urlLink, type);
            meshes[0].RecalculateNormals();
            if (meshes == null)
            {
                Debug.Log("Failed to get mesh");
                return 1;
            }
            else
            {
                gameObject.GetComponent<MeshFilter>().mesh = meshes[0];
                gameObject.GetComponent<MeshCollider>().sharedMesh = meshes[0];
                gameObject.GetComponent<MeshCollider>().convex = true;
                gameObject.GetComponent<MeshCollider>().sharedMaterial = Resources.Load("Prefabs/DesignObjPhy") as PhysicMaterial;
                gameObject.GetComponent<MeshCollider>().cookingOptions = MeshColliderCookingOptions.WeldColocatedVertices;

                MeshRenderer rend = gameObject.GetComponent<MeshRenderer>();
                rend.material = MaterialPrefab;

                //UpdateVisuals();
            }
            return 0;
        }

        void UpdateVisuals()
        {
            for (int i = 0; i < texts.Count; i++)
            {
                TextMesh holder = texts[i].GetComponent<TextMesh>();
                string c = "";
                int j = 0;
                while (holder.text[j] != ':')
                {
                    c += holder.text[j];
                    j++;
                }
                holder.text = string.Format(formatter, c, ((int)parameters[c]).ToString());
            }
        }

        /// <summary>
        /// Transforms the design obj.
        /// </summary>
        /// <returns>RETURN CODE: 0 = Successful update of transformation; 1 = Invalid parameters</returns>
        /// <param name="translate">Vector3 for new object position.</param>
        /// <param name="rotate">Quarternion rotation for new object rotation.</param>
        /// <param name="scale">Vector3 for new object localscale.</param>
        // Obsolete name: TransformFurniture
        public void TransformDesign(Vector3 translate, Quaternion rotate, Vector3 scale)
        {
            if (translate != gameObject.transform.position)
            {
                gameObject.transform.position = translate;
            }

            if (rotate != gameObject.transform.rotation)
            {
                gameObject.transform.rotation = rotate;
            }

            if (scale != gameObject.transform.localScale)
            {
                gameObject.transform.localScale = scale;
            }
        }

        #region Pass info of current design obj to outside
        public Vector3 GetPosition()
        {
            return gameObject.transform.position;
        }

        public Vector3 GetScale()
        {
            Vector3 scale = gameObject.transform.localScale;
            return (new Vector3(scale.x, scale.y , scale.z));
            //return (new Vector3(scale.x / 0.01f, scale.y / 0.01f, scale.z / 0.01f));
        }

        public Quaternion GetRotation()
        {
            return gameObject.transform.rotation;
        }

        public string GetFType()
        {
            return type;
        }

        public string GetID()
        {
            return ID.ToString();
        }

        public string GetName() {
            return gameObject.name;
        }

        public Dictionary<string, dynamic> GetParameters()
        {
            return parameters;
        }

        public Dictionary<string, dynamic> GetDefaultsAny(string url, string ftype)
        {
            return DefaultParameters(url, ftype);
        }

        public Dictionary<string, dynamic> GetDefaultsCurr() {
            return GetDefaultsAny(urlLink, type);
        }

        public Dictionary<string, string> GetParamType() {
            return paramtypes;
        }

        // Obsolete name: GetFurniture
        public GameObject GetGameobject()
        {
            return gameObject;
        }
        #endregion

        /// <summary>
        /// Display all the data about the current design obj. For Debugging Purposes.
        /// </summary>
        public void DebugParams()
        {
            // Log the type
            Debug.Log("Type: " + type);

            // Log the ID
            Debug.Log("ID: " + ID.ToString());

            // Log the local param
            // Because we do not know the type of the design obj is beforehand, we can't simply call the key
            foreach (KeyValuePair<string, dynamic> entry in parameters)
            {
                Debug.Log("Local Parameter: " + entry.Key + " = " + entry.Value.ToString());
            }
        }

        // Save and export the STL File and parameters from the object
        // This should be the same for all of the children, which is why it should be modified here
        /// <summary>
        /// Export this instance.
        /// </summary>
        /// <returns>RETURN CODE: 0 = Successfully exported STL File; 1 = Failed to export STL file</returns>
        public int Export(string filefolder, string filename) {
            if (!ExportParams(filefolder, filename) || !ExportSTL(filefolder, filename)) {
                return 1;
            }
            else {
                return 0;
            }
        }

        // Overload Export to default filename
        public int Export(string filefolder) {
            if ( !ExportParams(filefolder) || !ExportSTL(filefolder)) {
                return 1;
            }
            else {
                return 0;
            }
        }

        // Save parameters to a text file
        private bool ExportParams(string filefolder, string objname) {
            string filePath = filefolder + "/ExportParams.txt";

            try {
                using (StreamWriter writer = new StreamWriter(filePath, true)) {
                    writer.WriteLine(objname + ": ");
                    foreach (KeyValuePair<string, dynamic> entry in parameters) {
                        writer.WriteLine(entry.Key + ": " + entry.Value.ToString("F3"));
                    }
                    writer.WriteLine("scale: " + (gameObject.transform.localScale / 10f).ToString("F3"));
                    writer.WriteLine();
                }

                Debug.Log("Export parameters to " + filePath + ".");
                return true;
            }
            catch {
                Debug.Log("Fail to export parameters of " + gameObject.name);
                return false;
            }
        }

        // Overload ExportParams to default name
        // Used to distinguish whether an export is a auto save
        private bool ExportParams(string filefolder) {
            string objname = gameObject.name + System.DateTime.Now.ToString("_yyyy-MM-dd_HH-mm-ss");
            return ExportParams(filefolder, objname);
        }

        // Obsolete name: RemoveFurniture
        public void RemoveDesign()
        {
            Destroy(gameObject);
        }

        // Verify design obj parameters via FurnitureCatalog
        private bool VerifyDesign(string ftype, Dictionary<string, dynamic> parameter) {
            bool match = true;
            if (objectList.Contains(ftype)) {
                Dictionary<string, dynamic> defaultParams = DefaultParameters(urlLink, ftype);
                List<string> defaultKeyList = new List<string>(defaultParams.Keys);
                foreach (string paramkey in parameter.Keys) {
                    if (!defaultKeyList.Contains(paramkey)) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    return true;
                }
                else {
                    return false;
                }
            }
            else {
                return false;
            }
        }

        // Overloaded version of Verification that only takes type
        private bool VerifyDesign(string ftype)
        {
            if (objectList.Contains(ftype)) {
                return true;
            }
            else {
                return false;
            }
        }

        // The single function we will use to get an STL binary file
        // url = "http://ayeaye.ee.ucla.edu:5001", "https://roco.mehtank.com", "http://localhost:5001"
        private Mesh[] GetSTL(string url, string type)
        {
            //Store Mesh
            Mesh[] holder = null;

            string link = "{0}/{1}.stl?{2}";

            //Stage 1: Get the STL Binary from the server
            string param = LinkParam(parameters);

            link = string.Format(link, url, type, param);

            using (UnityWebRequest www = UnityWebRequest.Get(link))
            {
                www.SendWebRequest();
                while (!www.isDone) ;

                if (www.isNetworkError || www.isHttpError)
                {
                    Debug.Log(www.error);
                }
                else
                {
                    // Retrieve raw data from compiler
                    byte[] results = www.downloadHandler.data;

                    holder = MakeMesh(results);
                }
            }
            return holder;
        }

        // Exports STL into a .stl file for printing and manufacturing
        private bool ExportSTL(string filefolder, string filename)
        {
            GameObject[] garr = new GameObject[1];

            garr[0] = gameObject;

            string filePath = filefolder + "/" + filename + ".stl";

            if (pb_Stl_Exporter.Export(filePath, garr, FileType.Ascii))
            {
                Debug.Log(gameObject.name + " is exported to " + filePath + ".");
                return true;
            }
            else
            {
                Debug.Log("Fail to export " + gameObject.name + ".");
                return false;
            }
        }

        // Reload ExportSTL with default file name
        private bool ExportSTL(string filefolder) {
            string filename = gameObject.name + System.DateTime.Now.ToString("_yyyy-MM-dd_HH-mm-ss");
            return ExportSTL(filefolder, filename);
        }

        // A function to read stl into design object
        private Mesh[] MakeMesh(byte[] data)
        {
            //Stage 2: Transform the STL into a working Mesh
            MemoryStream stream = new MemoryStream(data);

            Mesh[] meshes = Importer.ImportAscii(stream);
            //Mesh[] meshes = Importer.ImportBinary(stream);

            Vector3[] vertices = meshes[0].vertices;
            Vector2[] UVArr = new Vector2[vertices.Length];
            for (int i = 0; i < UVArr.Length; i++)
            {
                UVArr[i] = new Vector2(vertices[i].x, vertices[i].z);
                //UVArr[i] = new Vector2(vertices[i].x * 0.01f, vertices[i].z * 0.01f);
            }

            meshes[0].uv = UVArr;

            meshes[0].RecalculateNormals();
            meshes[0].RecalculateBounds();

            // In this case, the importer automatically merges the meshes together, so the only mesh in the array is at index 0
            return meshes;
        }

        // A function used to produce the strings for the parameters for the url
        public string LinkParam(Dictionary<string, dynamic> parameters)
        {
            string result = "";

            foreach (KeyValuePair<string, dynamic> entry in parameters)
            {
                // Case where we have to input a discrete value
                if (entry.Key == "legs" || entry.Key == "angle")
                {
                    int valuenum = (int)entry.Value;
                    result += (entry.Key + "=" + valuenum.ToString() + "&");
                    continue;
                }

                // Case where we can input a decimal value
                result += (entry.Key + "=" + entry.Value.ToString() + "&");
            }

            // For the last string we have an extra ampersand sign at the end
            result = result.Substring(0, result.Length - 1);
            result += "&%24thickness=3";

            return result;
        }
    };
}


