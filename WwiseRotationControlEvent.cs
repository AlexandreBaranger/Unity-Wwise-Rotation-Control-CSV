using UnityEngine;

public class WwiseRotationControlEvent : MonoBehaviour
{
    public GameObject targetObject;
    public float updateInterval = 0.1f;
    public float minRotationYForEvent = 1.0f; 
    public float minRotationZForEvent = 1.0f; 
    private float timeSinceLastUpdate = 0.0f;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private bool isRotatingY = false;
    private bool isRotatingZ = false;
    public AK.Wwise.Event rotationYChangeEvent;
    public AK.Wwise.Event rotationZChangeEvent;

    private void Start()
    {
        lastRotation = targetObject.transform.rotation;
    }

    private void Update()
    {
        timeSinceLastUpdate += Time.deltaTime;
        if (timeSinceLastUpdate >= updateInterval)
        {
            Vector3 currentPosition = targetObject.transform.position;
            Quaternion currentRotation = targetObject.transform.rotation;
            float rotationYChange = Mathf.Abs(currentRotation.eulerAngles.y - lastRotation.eulerAngles.y);
            float rotationZChange = Mathf.Abs(currentRotation.eulerAngles.z - lastRotation.eulerAngles.z);
            if (rotationYChange >= minRotationYForEvent && !isRotatingY)
            {
                rotationYChangeEvent.Post(gameObject);
                isRotatingY = true;
            }
            else if (rotationYChange < minRotationYForEvent && isRotatingY)
            {
                rotationYChangeEvent.Stop(gameObject);
                isRotatingY = false;
            }
            if (rotationZChange >= minRotationZForEvent && !isRotatingZ)
            {
                rotationZChangeEvent.Post(gameObject);
                isRotatingZ = true;
            }
            else if (rotationZChange < minRotationZForEvent && isRotatingZ)
            {
                rotationZChangeEvent.Stop(gameObject);
                isRotatingZ = false;
            }
            lastRotation = currentRotation;
            timeSinceLastUpdate = 0;
        }
    }
}
