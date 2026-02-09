using UnityEngine;

public class EyeContact : MonoBehaviour
{
    [Header("Komponenty")]
    public SkinnedMeshRenderer characterMesh;

    [Tooltip("Head Bone")]
    public Transform headBone;
    public Transform targetToLookAt;

    [Header("Blendshapes Oèí")]
    public string eyeLeft = "blendShape1.AU_61_EyesTurnLeft";
    public string eyeRight = "blendShape1.AU_62_EyesTurnRight";
    public string eyeUp = "blendShape1.AU_63_EyesUp";
    public string eyeDown = "blendShape1.AU_64_EyesDown";

    [Header("Kalibrace")]
    [Tooltip("Pokud se oèi hýbou, ale na opaènou stranu horizontálnì.")]
    public bool invertX = false;
    [Tooltip("Pokud se oèi hýbou, ale na opaènou stranu vertikálnì.")]
    public bool invertY = false;

    public Vector3 rotationOffset = Vector3.zero;

    [Header("Konfigurace")]
    public float lookSpeed = 10f;
    public float maxEyeAngle = 45f; // Maximální úhel, kam až oèi mohou zajet

    // Random pohyb
    public float randomMoveInterval = 2.5f;
    public float randomRange = 0.5f;

    private bool isPlayerTalking = false;

    // Promìnné pro pohyb
    private float targetX = 0;
    private float targetY = 0;
    private float currentX = 0;
    private float currentY = 0;
    private float idleTimer = 0;

    private void Start()
    {
        // Pro testování zapnuto
        //isPlayerTalking = true;
        if (headBone == null) headBone = transform;
    }

    void LateUpdate() 
    {
        if (characterMesh == null || headBone == null) return;

        if (isPlayerTalking && targetToLookAt != null)
        {
            Quaternion correction = Quaternion.Euler(rotationOffset);

            Quaternion correctedHeadRot = headBone.rotation * correction;

            Vector3 directionGlobal = targetToLookAt.position - headBone.position;

            Vector3 localDirection = Quaternion.Inverse(correctedHeadRot) * directionGlobal;

            float angleX = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;

            float angleY = Mathf.Atan2(localDirection.y, localDirection.z) * Mathf.Rad2Deg;

            float rawX = Mathf.Clamp(angleX / maxEyeAngle, -1f, 1f);
            float rawY = Mathf.Clamp(angleY / maxEyeAngle, -1f, 1f);

            if (invertX) rawX = -rawX;
            if (invertY) rawY = -rawY;

            targetX = rawX;
            targetY = rawY;
        }
        else
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0)
            {
                targetX = Random.Range(-1f, 1f) * randomRange;
                targetY = Random.Range(-0.5f, 0.5f) * randomRange;
                idleTimer = Random.Range(1.0f, randomMoveInterval);
            }
        }
        

        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * lookSpeed);
        currentY = Mathf.Lerp(currentY, targetY, Time.deltaTime * lookSpeed);

        SetBlendshape(eyeLeft, currentX < 0 ? -currentX * 100 : 0);  // Když koukám doleva (záporné X)
        SetBlendshape(eyeRight, currentX > 0 ? currentX * 100 : 0);  // Když koukám doprava (kladné X)

        SetBlendshape(eyeDown, currentY < 0 ? -currentY * 100 : 0); // Když koukám dolu
        SetBlendshape(eyeUp, currentY > 0 ? currentY * 100 : 0);    // Když koukám nahoru
    }

    public void SetTalkingState(bool isTalking)
    {
        isPlayerTalking = isTalking;
        if (!isTalking) idleTimer = 0.5f;
    }

    void SetBlendshape(string name, float value)
    {
        int index = characterMesh.sharedMesh.GetBlendShapeIndex(name);
        if (index != -1) characterMesh.SetBlendShapeWeight(index, value);
    }

    // --- DEBUGGING ---
    // Tohle ti ukáže ve scénì èáry, abys vìdìl, co se dìje
    private void OnDrawGizmos()
    {
        if (headBone == null) return;

        // Aplikace offsetu pro vizualizaci
        Quaternion correction = Quaternion.Euler(rotationOffset);
        Vector3 forwardDir = (headBone.rotation * correction) * Vector3.forward;

        // MODRÁ ÈÁRA = Kam si script myslí, že je "PØEDEK" oblièeje
        // Tato èára musí míøit postavì z nosu ven! Pokud ne, uprav Rotation Offset.
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(headBone.position, forwardDir * 2f);

        if (targetToLookAt != null)
        {
            // ÈERVENÁ ÈÁRA = Kde je cíl
            Gizmos.color = Color.red;
            Gizmos.DrawLine(headBone.position, targetToLookAt.position);
        }
    }
}