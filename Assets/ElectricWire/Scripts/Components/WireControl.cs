
//(c8

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElectricWire
{
    [Serializable]
    public class WireJsonData
    {
        public List<Vector3> segments;

        public WireJsonData(List<Vector3> newSegments)
        {
            segments = newSegments;
        }
    }

    public class WireControl : MonoBehaviour, ISaveJsonData
    {
        [HideInInspector] public GameObject instantiatedFrom;

        [Header("If you do not want this component save")]
        public bool skipSave = false;
        [Header("If you do not want this component have undo/redo")]
        public bool skipUndoRedo = false;

        public LineRenderer lineRenderer;


        // temp position while loading
        public List<Vector3> segmentsT;

        private int checkConnectionTry = 0;
        private int checkConnectionMaxTry = 5;
        private float checkConnectionSecTry = 1f;

        // Wire connections
        public WireConnector wireConnectorInput;
        public WireConnector wireConnectorOutput;
        private WireConnector wireConnectorTemp;
        private bool firstIsInput = false;

        public void Start()
        {
            Debug.Log("WireControl Start");
            // When start with already placed prefabs in the scene
            if (wireConnectorInput != null && wireConnectorOutput != null)
            {
                Debug.Log("WireControl Connection");
                wireConnectorInput.ConnectWire(gameObject);
                wireConnectorOutput.ConnectWire(gameObject);
            }
            Debug.Log($"WireControl Input: {wireConnectorInput?.name}");
            Debug.Log($"WireControl Output: {wireConnectorOutput?.name}");

            segmentsT.Clear(); // Clear any existing data

            // Add the positions of the input and output connectors
            if (wireConnectorInput != null)
                segmentsT.Add(wireConnectorInput.transform.position);

            if (wireConnectorOutput != null)
                segmentsT.Add(wireConnectorOutput.transform.position);

            Debug.Log($"Initialized segmentsT with {segmentsT.Count} positions.");

            SetupLine(segmentsT, true);

        }

        private void OnDisable()
        {
            // TODO : Remove connected to?
        }

        public string GetJsonPosition()
        {
            string jsonData = JsonUtility.ToJson(new PositionJsonData(gameObject.name.Replace("(Clone)", ""), transform.position, transform.rotation));
            return jsonData;
        }

        public string GetJsonData()
        {
            string jsonData = JsonUtility.ToJson(new WireJsonData(segmentsT));
            return jsonData;
        }

        public void SetupFromJsonData(string jsonData)
        {
            WireJsonData wireJsonData = JsonUtility.FromJson<WireJsonData>(jsonData);
            if (wireJsonData == null)
            {
                Debug.LogWarning("No json data found for: " + name + ". Resave could fix that.");
                return;
            }

            SetupLine(wireJsonData.segments, true);
        }

        public void ReSetupLine()
        {
            checkConnectionTry++;
            if (checkConnectionTry < checkConnectionMaxTry)
            {
                SetupLine(segmentsT, true);
            }
            else
            {
                Debug.LogWarning("Wire: " + gameObject.name + " position: " + gameObject.transform.position + " did not find a connector, object removed!");
                Destroy(gameObject);
            }
        }

        public Vector3 SetupLine(List<Vector3> segments, bool isLoading = false)
        {
            segmentsT = segments;

            // Debugging: Check the number of segments in the list
            Debug.Log("SetupLine: Number of segments in the list: " + segmentsT.Count);

            // Find connection at each end
            FindConnectionAtEachEnd();

            // Do we have a connection at each end?
            if (wireConnectorInput == null || wireConnectorOutput == null)
            {
                Debug.LogWarning("SetupLine: Wire connectors are null.");
                // When loading from database, check number of time for a late spawn of connector
                if (isLoading)
                    Invoke(nameof(ReSetupLine), checkConnectionSecTry);
                return Vector3.zero;
            }

            // Wire start is at the output position
            if (!isLoading)
            {
                transform.position = firstIsInput ? wireConnectorOutput.transform.position : wireConnectorInput.transform.position;
                Debug.Log("SetupLine: Setting wire position to: " + transform.position);
            }

            lineRenderer.positionCount = segmentsT.Count;
            for (int i = 0; i < segmentsT.Count; i++)
            {
                // Debugging: Log each segment position being set in the LineRenderer
                lineRenderer.SetPosition(i, segmentsT[i]);
                Debug.Log($"SetupLine: Setting position for segment {i}: {segmentsT[i]}");
            }

            // Adding colliders for each segment
            for (int i = 0; i < segmentsT.Count - 1; i++)
            {
                var seg = new GameObject("seg" + i);
                var newCollider = seg.AddComponent<BoxCollider>();
                newCollider.isTrigger = true;
                newCollider.center = new Vector3(0f, 0f, 0.05f);
                newCollider.size = new Vector3(0.05f, 0.05f, 0.1f);
                seg.transform.position = segmentsT[i];
                seg.transform.SetParent(transform, true);

                // Debugging: Log collider setup for each segment
                seg.transform.rotation = Quaternion.LookRotation((segmentsT[i + 1] - segmentsT[i]).normalized);
                seg.transform.localScale = new Vector3(1f, 1f, Vector3.Distance(segmentsT[i + 1], segmentsT[i]) * 10f);

                Debug.Log($"SetupLine: Segment {i} collider created with position: {segmentsT[i]}");
            }

            // Connect wire
            wireConnectorInput.ConnectWire(gameObject);
            wireConnectorOutput.ConnectWire(gameObject);

            // Debugging: Confirm wire connection
            Debug.Log("SetupLine: Wire connected from input to output");

            // Return the second target to make the collider follow wire
            return firstIsInput ? wireConnectorOutput.transform.position : wireConnectorInput.transform.position;
        }


        private void FindConnectionAtEachEnd()
        {
            // Find connection at each end
            Collider[] hitColliders = Physics.OverlapSphere(segmentsT[0], 0.01f);
            Debug.Log("Number of colliders found: " + hitColliders.Length);
            for (int i = 0; i < hitColliders.Length; i++)
            {
                wireConnectorTemp = hitColliders[i].GetComponent<WireConnector>();
                Debug.Log("Hello");
                if (wireConnectorTemp != null)
                {
                    if (wireConnectorTemp.isInput)
                    {
                        firstIsInput = true;
                        wireConnectorInput = wireConnectorTemp;
                        Debug.Log("Wire: " + gameObject.name + " found input connector: " + wireConnectorInput.name);
                    }
                    else
                        wireConnectorOutput = wireConnectorTemp;
                    Debug.Log("Wire: " + gameObject.name + " found output connector: " + wireConnectorOutput.name);
                    break;
                }
            }
            hitColliders = Physics.OverlapSphere(segmentsT[segmentsT.Count - 1], 0.01f);
            for (int i = 0; i < hitColliders.Length; i++)
            {
                wireConnectorTemp = hitColliders[i].GetComponent<WireConnector>();
                if (wireConnectorTemp != null)
                {
                    Debug.Log("Wire: " + gameObject.name + " found connector: " + wireConnectorTemp.name);
                    if (wireConnectorTemp.isInput)
                        wireConnectorInput = wireConnectorTemp;
                    else
                        wireConnectorOutput = wireConnectorTemp;
                    break;
                }
            }

            wireConnectorTemp = null;
        }

        public void DisconnectWire()
        {
            if (wireConnectorInput != null)
                wireConnectorInput.DisconnectWire();
            if (wireConnectorOutput != null)
                wireConnectorOutput.DisconnectWire();

            Destroy(gameObject);
        }

        public void DisableWire()
        {
            if (wireConnectorInput != null)
                wireConnectorInput.DisconnectWire();
            if (wireConnectorOutput != null)
                wireConnectorOutput.DisconnectWire();

            wireConnectorInput = null;
            wireConnectorOutput = null;

            gameObject.SetActive(false);
        }

        public void ReSetupWire()
        {
            FindConnectionAtEachEnd();

            if (wireConnectorInput == null || wireConnectorOutput == null)
                return;

            // Connect wire
            wireConnectorInput.ConnectWire(gameObject);
            wireConnectorOutput.ConnectWire(gameObject);
        }
    }
}
