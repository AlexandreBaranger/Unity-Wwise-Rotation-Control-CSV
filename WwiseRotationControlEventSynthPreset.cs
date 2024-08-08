using UnityEngine;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;

public class WwiseRotationControlEventSynthPreset : MonoBehaviour
{
    public float updateInterval = 0.1f;

    [System.Serializable]
    public class PanoramaRTPCConfig
    {
        public AK.Wwise.RTPC leftRightRTPC;
        public AK.Wwise.RTPC frontBackRTPC;
        public AK.Wwise.RTPC upDownRTPC;
        public float minPanValue = -3600f;
        public float maxPanValue = 3600f;
    }

    public List<PanoramaRTPCConfig> panoramaRTPCConfigs;

    [System.Serializable]
    public class RotationEventConfig
    {
        public AK.Wwise.Event wwiseEvent;
        public float minRotation;
        public float maxRotation;
        public float minRotationSpeed;
        public float maxRotationSpeed;
        public string csvFileName;
    }

    [System.Serializable]
    public class RTPCConfig
    {
        public AK.Wwise.RTPC rtpc;
        [Range(-3600.0f, 3600.0f)]
        public float rtpcDebugValue;
        public float minRTPCValue;
        public float maxRTPCValue;
        public bool useYAxis;
    }

    public List<RotationEventConfig> rotationYEvents;
    public List<RotationEventConfig> rotationZEvents;
    public List<RTPCConfig> rtpcConfigs;
    public GameObject targetObject;

    [SerializeField] private float currentRotationY;
    [SerializeField] private float currentRotationZ;
    [SerializeField] private float rotationSpeedY;
    [SerializeField] private float rotationSpeedZ;

    private float timeSinceLastUpdate = 0.0f;
    private Quaternion lastRotation;

    public List<CSVData> csvDataList = new List<CSVData>();

    private void Start()
    {
        if (targetObject == null)
        {
            Debug.LogError("Target Object not assigned!");
            enabled = false;
            return;
        }
        lastRotation = targetObject.transform.rotation;
    }

    private void Update()
    {
        if (targetObject == null)
        {
            Debug.LogError("Target Object not assigned!");
            enabled = false;
            return;
        }

        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= updateInterval)
        {
            Quaternion currentRotation = targetObject.transform.rotation;
            float rotationYChange = Mathf.Abs(currentRotation.eulerAngles.y - lastRotation.eulerAngles.y);
            float rotationZChange = Mathf.Abs(currentRotation.eulerAngles.z - lastRotation.eulerAngles.z);
            currentRotationY = currentRotation.eulerAngles.y;
            currentRotationZ = currentRotation.eulerAngles.z;
            rotationSpeedY = rotationYChange / timeSinceLastUpdate;
            rotationSpeedZ = rotationZChange / timeSinceLastUpdate;

            foreach (var config in rotationYEvents)
            {
                if (rotationYChange >= config.minRotation && rotationYChange <= config.maxRotation &&
                    rotationSpeedY >= config.minRotationSpeed && rotationSpeedY <= config.maxRotationSpeed)
                {
                    config.wwiseEvent.Post(gameObject);
                    LoadCSVFromRotationEvent(config.csvFileName);
                }
            }

            foreach (var config in rotationZEvents)
            {
                if (rotationZChange >= config.minRotation && rotationZChange <= config.maxRotation &&
                    rotationSpeedZ >= config.minRotationSpeed && rotationSpeedZ <= config.maxRotationSpeed)
                {
                    config.wwiseEvent.Post(gameObject);
                    LoadCSVFromRotationEvent(config.csvFileName);
                }
            }

            foreach (var rtpcConfig in rtpcConfigs)
            {
                float rotationValue = rtpcConfig.useYAxis ? currentRotationY : currentRotationZ;
                float rtpcValue = MapRotationToRTPC(rotationValue, rtpcConfig.minRTPCValue, rtpcConfig.maxRTPCValue);

                if (rtpcConfig.rtpc != null)
                {
                    rtpcConfig.rtpc.SetValue(gameObject, rtpcValue);
                    rtpcConfig.rtpcDebugValue = rtpcValue;
                }
            }

            foreach (var panoramaConfig in panoramaRTPCConfigs)
            {
                float leftRightValue = Mathf.Lerp(panoramaConfig.minPanValue, panoramaConfig.maxPanValue, Mathf.InverseLerp(-180f, 180f, currentRotationY));
                float frontBackValue = Mathf.Lerp(panoramaConfig.minPanValue, panoramaConfig.maxPanValue, Mathf.InverseLerp(-180f, 180f, currentRotationZ));
                float upDownValue = Mathf.Lerp(panoramaConfig.minPanValue, panoramaConfig.maxPanValue, Mathf.InverseLerp(-90f, 90f, currentRotation.eulerAngles.x));

                if (panoramaConfig.leftRightRTPC != null)
                    panoramaConfig.leftRightRTPC.SetValue(gameObject, leftRightValue);
                if (panoramaConfig.frontBackRTPC != null)
                    panoramaConfig.frontBackRTPC.SetValue(gameObject, frontBackValue);
                if (panoramaConfig.upDownRTPC != null)
                    panoramaConfig.upDownRTPC.SetValue(gameObject, upDownValue);
            }

            lastRotation = currentRotation;
            timeSinceLastUpdate = 0;
        }
    }

    private void LoadCSVFromRotationEvent(string fileName)
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (File.Exists(filePath))
        {
            StartCoroutine(LoadCSV(filePath));
        }
    }

    private IEnumerator LoadCSV(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("CSV file not found at path: " + filePath);
            yield break;
        }
        string[] rows = File.ReadAllLines(filePath);
        yield return null;

        csvDataList.Clear();

        foreach (string row in rows)
        {
            string[] columns = row.Split(',');
            if (columns.Length == 5)
            {
                CSVData data = new CSVData
                {
                    Volume = columns[0].Trim(),
                    Parameter = columns[1].Trim(),
                    MinRandomRange = float.Parse(columns[3].Trim(), CultureInfo.InvariantCulture),
                    MaxRandomRange = float.Parse(columns[4].Trim(), CultureInfo.InvariantCulture)
                };
                float value;
                string valueStr = columns[2].Trim();
                if (valueStr == "0.000000")
                {
                    value = 0.0f;
                }
                else if (float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    data.Value = value;
                    SendValueToWwise(data);
                }
                else
                {
                    Debug.LogWarning("Failed to parse value: " + valueStr);
                }
                csvDataList.Add(data);
            }
            else
            {
                Debug.LogWarning("Row format is incorrect: " + row);
            }
        }
    }

    private void SendValueToWwise(CSVData data)
    {
        float randomizedValue = UnityEngine.Random.Range(data.Value + data.MinRandomRange, data.Value + data.MaxRandomRange);
        string formattedValue = randomizedValue.ToString("0.000000", CultureInfo.InvariantCulture);
        AkSoundEngine.SetRTPCValue(data.Parameter, float.Parse(formattedValue, CultureInfo.InvariantCulture));
    }

    private float MapRotationToRTPC(float rotationValue, float minRTPC, float maxRTPC)
    {
        return Mathf.Lerp(minRTPC, maxRTPC, Mathf.InverseLerp(0.0f, 360.0f, rotationValue));
    }

    [System.Serializable]
    public class CSVData
    {
        public string Volume;
        public string Parameter;
        public float Value;
        public float MinRandomRange;
        public float MaxRandomRange;
    }
}
