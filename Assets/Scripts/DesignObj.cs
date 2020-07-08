// This script is attached to Prefab obj CreationObj
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.IO;
using STLImporter;
using Parabox.STL;

namespace FARVR.Design {
    public class DesignObj : MonoBehaviour {
        // A Material used to display on the Prefab Object
        public Material MaterialPrefab;

        // Text displayer prefab
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
        private Dictionary<string, float> parameters;

        /// <summary>
        /// List to store the different text display objects
        /// </summary>
        private List<GameObject> texts = new List<GameObject>();

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

        // TODO: auto gain from backend compiler
        /// <summary>
        /// The obj catalog. Contains all available furnitures in current class that users can generate
        /// </summary>
        public static Dictionary<string, Dictionary<string, float>> FurnitureCatalog = new Dictionary<string, Dictionary<string, float>>() {
            {"Stool", new Dictionary<string, float>() {
                    {"height", 80},
                    {"legs", 3},
                    {"radius", 20},
                    {"angle", 60}
                }},
            {"SimpleTable", new Dictionary<string, float>() {
                    {"height", 40},
                    {"width", 50},
                    {"length", 70},
                    {"thickness", 10},
                    {"taper", 0.5f}
                }},
            {"SimpleChair", new Dictionary<string, float>() {
                    {"taper", 0.5f},
                    {"recline", 110},
                    {"height", 40},
                    {"width", 70},
                    {"depth", 50},
                    {"gapheight", 20},
                    {"backheight", 40},
                    {"thickness", 10}
                }},
            {"RockerChair", new Dictionary<string, float>() {
                    {"rocker", 10},
                    {"recline", 110},
                    {"height", 40},
                    {"width", 70},
                    {"depth", 50},
                    {"gapheight", 20},
                    {"backheight", 40},
                    {"thickness", 10}
                }},
            {"Paperbot", new Dictionary<string, float>() {
                    {"battery_thickness", 4.9f},
                    {"battery_width", 60},
                    {"robot_length", 80},
                    {"wheel_radius", 25}
                }},
            {"BoatBase", new Dictionary<string, float>() {
                    {"width", 50},
                    {"length", 250},
                    {"stern", 0f},
                    {"depth", 20},
                    {"bow", 45}
                }},
            {"Canoe", new Dictionary<string, float>() {
                    {"width", 50},
                    {"length", 250},
                    {"stern", 0f},
                    {"bow", 45},
                    {"depth", 20},
                    {"n", 3}
                }},
            {"Catamaran", new Dictionary<string, float>() {
                    {"width", 60},
                    {"depth", 50},
                    {"length", 200},
                    {"height", 30}
            }},
            {"Trimaran", new Dictionary<string, float>() {
                    {"width", 50},
                    {"length", 250},
                    {"stern", 0f},
                    {"bow", 45},
                    {"spacing", 25},
                    {"depth", 20},
                    {"n", 6}
            }},
            {"CatFoil", new Dictionary<string, float>() {
                    {"width", 60},
                    {"length", 200},
                    {"dl", 0.1f},
                    {"depth", 50},
                    {"height", 30}
            }},
            {"Tug", new Dictionary<string, float>() {
                    {"width", 60},
                    {"depth", 50},
                    {"length", 200},
                    {"height", 30}
            }}
        };

        // Creates a design object
        /// <summary>
        /// Generates a design object by setting all the parameters in the object.
        /// </summary>
        /// <returns>RETURN CODE: 0 = Successful creation of obj; 1 = Failed to retrieve mesh from server; 2 = Invalid parameters</returns>
        /// <param name="localparameters">Parameters used to generate a mesh</param>
        /// <param name="ftype">The name of the design obj that we are to generate</param>
        /// <param name="id">A unique ID for the design obj.</param>
        // Obsolete name: MakeFurniture
        public int MakeDesign(string url, Dictionary<string, float> localparameters, string ftype, int id, Vector3 location, Quaternion rotation, Vector3 scale)
        {
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
            Mesh[] meshes = GetSTL(url);
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
                gameObject.GetComponent<MeshCollider>().sharedMaterial = Resources.Load("Assets/Prefabs/CreationPhy") as PhysicMaterial;
                gameObject.GetComponent<MeshCollider>().cookingOptions = MeshColliderCookingOptions.InflateConvexMesh;
                Renderer rend = gameObject.GetComponent<Renderer>();
                rend.material = MaterialPrefab;

                /* Transforms the design obj based on what we have initialized it to be */
                TransformDesign(location, rotation, scale);

                /* Update: Added a Collider tag that allows us to distinguish whether an Existing design obj was hit or not */
                gameObject.tag = "Design";

                /* Add the text displays */
                int Vcounter = 1;
                foreach (KeyValuePair<string, float> entry in parameters)
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
        public int MakeDesign(string url, Dictionary<string, float> localparameters, string ftype, int id, Vector3 location)
        {
            return MakeDesign(url, localparameters, ftype, id, location, Quaternion.identity, new Vector3(1f, 1f, 1f));
        }

        // Optional rotation
        public int MakeDesign(string url, Dictionary<string, float> localparameters, string ftype, int id, Vector3 location, Vector3 scale)
        {
            return MakeDesign(url, localparameters, ftype, id, location, Quaternion.identity, scale);
        }

        // Optional scale
        public int MakeDesign(string url, Dictionary<string, float> localparameters, string ftype, int id, Vector3 location, Quaternion rotate)
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
        public Dictionary<string, float> MakeDesign(string url, string ftype, int id, Vector3 location, Quaternion rotation, Vector3 scale)
        {
            //Verify that the design obj is valid
            if (!VerifyDesign(ftype))
            {
                Debug.Log("Failed to find valid design object");
                return null;
            }
            else
            {

                //If the design obj is valid
                parameters = FurnitureCatalog[ftype];

                type = ftype;
                ID = id;

                gameObject.name = type + ID.ToString();
                gameObject.AddComponent<MeshFilter>();
                gameObject.AddComponent<MeshRenderer>();

                // TODO: Add Physics collision to object
                // .gameObject.AddComponent<MeshCollider> ();
                Mesh[] meshes = GetSTL(url);
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
                    gameObject.GetComponent<MeshCollider>().sharedMaterial = Resources.Load("Assets/Prefabs/CreationPhy") as PhysicMaterial;
                    gameObject.GetComponent<MeshCollider>().cookingOptions = MeshColliderCookingOptions.WeldColocatedVertices;
                    Renderer rend = gameObject.GetComponent<Renderer>();
                    rend.material = MaterialPrefab;

                    /*  Transform the design obj based on provided parameters */
                    TransformDesign(location, rotation, scale);

                    /* Update: Added a Collider tag that allows us to distinguish whether an Existing design obj was hit or not */
                    gameObject.tag = "Design";

                    /* Add the text displays */
                    int Vcounter = 1;
                    foreach (KeyValuePair<string, float> entry in parameters)
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
        public Dictionary<string, float> MakeDesign(string url, string ftype, int id, Vector3 location)
        {
            return MakeDesign(url, ftype, id, location, Quaternion.identity, new Vector3(1f, 1f, 1f));
        }

        // Optional rotation
        public Dictionary<string, float> MakeDesign(string url, string ftype, int id, Vector3 location, Vector3 scale)
        {
            return MakeDesign(url, ftype, id, location, Quaternion.identity, scale);
        }

        // Optional scale
        public Dictionary<string, float> MakeDesign(string url, string ftype, int id, Vector3 location, Quaternion rotate)
        {
            return MakeDesign(url, ftype, id, location, rotate, new Vector3(1f, 1f, 1f));
        }

        /// <summary>
        /// Updates the design obj. Return and update the corresponding mesh of the object.
        /// </summary>
        /// <returns>RETURN CODE: 0 = Successfully updated design obj; 1 = Failed to retrieve mesh</returns>
        /// <param name="localparameters">Local parameters of the given design obj.</param>
        // Obsolete name: UpdateFurniture
        public int UpdateDesign(string url, Dictionary<string, float> localparameters)
        {
            parameters = localparameters;
            // Assuming we need to update the design obj
            Mesh[] meshes = GetSTL(url);
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
                gameObject.GetComponent<MeshCollider>().sharedMaterial = Resources.Load("Assets/Prefabs/CreationPhy") as PhysicMaterial;
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
        public int TransformDesign(Vector3 translate, Quaternion rotate, Vector3 scale)
        {
            if (translate != gameObject.transform.position)
            {
                gameObject.transform.position = translate;
            }

            if (rotate != gameObject.transform.rotation)
            {
                gameObject.transform.rotation = rotate;
            }

            Vector3 scaler = new Vector3(scale.x, scale.y, scale.z);
            //Vector3 scaler = new Vector3(0.01f * scale.x, 0.01f * scale.y, 0.01f * scale.z);

            if (scaler != gameObject.transform.localScale)
            {
                gameObject.transform.localScale = scaler;
            }

            return 0;
        }

        // To obtain transform info of design obj
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

        public string GetID()
        {
            return (gameObject.name);
        }

        public Dictionary<string, float> GetParameters()
        {
            return parameters;
        }

        public string GetFType()
        {
            return type;
        }

        public Dictionary<string, float> GetDefaults(string ftype)
        {
            return FurnitureCatalog[ftype];
        }

        // Obsolete name: GetFurniture
        public GameObject GetGameobject()
        {
            return gameObject;
        }

        /// <summary>
        /// Display all the data about the current design obj. For Debugging Purposes.
        /// </summary>
        public void Display()
        {
            // Log the type
            Debug.Log("Type: " + type);

            // Log the ID
            Debug.Log("ID: " + ID.ToString());

            // Log the local param
            // Because we do not know the type of the design obj is beforehand, we can't simply call the key
            foreach (KeyValuePair<string, float> entry in parameters)
            {
                Debug.Log("Local Parameter: " + entry.Key + " = " + entry.Value.ToString());
            }
        }

        // Save and export the STL File from the object
        /// <summary>
        /// Export this instance.
        /// </summary>
        /// <returns>RETURN CODE: 0 = Successfully exported STL File; 1 = Failed to export STL file</returns>
        public int Export()
        {
            // This should be the same for all of the children, which is why it should be modified here 
            if (!ExportSTL())
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }

        // Obsolete name: RemoveFurniture
        public void RemoveDesign()
        {
            Destroy(gameObject);
        }

        // Verify design obj parameters via FurnitureCatalog
        private bool VerifyDesign(string type, Dictionary<string, float> parameter)
        {
            foreach (KeyValuePair<string, Dictionary<string, float>> entry in FurnitureCatalog)
            {
                // Check if the type of the design obj exists
                if (entry.Key == type)
                {
                    // If the type exists, we verify the parameters are valid
                    // Search the dictionary from FurnitureCatalog and the parameters to find similarity
                    foreach (KeyValuePair<string, float> requirement in entry.Value)
                    {
                        // To verify all parameters, we ensure we can find one of each value entry
                        bool match = false;
                        foreach (KeyValuePair<string, float> comparer in parameter)
                        {
                            //Once we find a match, we don't have to search exhaustively
                            if (comparer.Key == requirement.Key)
                            {
                                match = true;
                                break;
                            }
                        }

                        // If we do not find a match after searching through all the parameters, then we can confirm that parameters are invalid
                        if (!match)
                        {
                            return false;
                        }
                    }

                    // If we search through entire parameter and all match, then it is valid
                    return true;
                }
            }

            // If we can't find the correct type, then it is invalid
            return false;
        }

        // Overloaded version of Verification that only takes type
        private bool VerifyDesign(string type)
        {
            foreach (string objectName in objectList) {
                if (objectName == type)
                {
                    return true;
                }
            }

            return false;
        }

        // The single function we will use to get an STL binary file
        // url = "http://ayeaye.ee.ucla.edu:5001", "https://roco.mehtank.com", "http://localhost:5001"
        private Mesh[] GetSTL(string url)
        {
            //Store Mesh
            Mesh[] holder = null;

            string link = "/{0}.stl?{1}";

            //Stage 1: Get the STL Binary from the server
            string param = LinkParam(parameters);

            link = url + string.Format(link, type, param);

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
                    // Or retrieve results as binary data
                    byte[] results = www.downloadHandler.data;

                    holder = MakeMesh(results);
                }
            }
            return holder;
        }

        // Exports STL into a .stl file for printing and manufacturing
        private bool ExportSTL()
        {
            GameObject[] garr = new GameObject[1];

            garr[0] = gameObject;

            string fileName = Application.persistentDataPath + "/" + type + ID + ".stl";

            if (pb_Stl_Exporter.Export(fileName, garr, FileType.Binary))
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        // A function to read the bytes into design object
        private Mesh[] MakeMesh(byte[] data)
        {
            //Stage 2: Transform the STL into a working Mesh
            MemoryStream stream = new MemoryStream(data);

            Mesh[] meshes = Importer.ImportBinary(stream);

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
        public string LinkParam(Dictionary<string, float> parameters)
        {
            string result = "";

            foreach (KeyValuePair<string, float> entry in parameters)
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


