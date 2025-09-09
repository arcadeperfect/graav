using UnityEngine;
using UnityEngine.InputSystem;

public class CircularPlanetCameraController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float panSpeed = 5f;
    public float forwardSpeed = 1f;
    
    private Vector2 panInput;
    private float inOutInput;
    
    
    private void Update()
    {
        HandleMovement();
    }
    
    private void HandleMovement()
    {
        Vector3 currentPos = transform.position;
        Vector3 moveAmount = Vector3.zero;

        if (inOutInput != 0)
        {
            // Current z distance from the z=0 plane
            float currentZ = Mathf.Abs(currentPos.z);
            
            // Scale movement by current distance - closer to 0 means smaller movements
            // This creates exponential-like behavior where you approach but never reach 0
            float scaledMovement = inOutInput * forwardSpeed * currentZ * Time.deltaTime;
            
            // Apply movement in the correct direction
            if (currentPos.z > 0)
            {
                moveAmount.z = -scaledMovement; // Moving toward 0 from positive side
            }
            else
            {
                moveAmount.z = scaledMovement;  // Moving toward 0 from negative side
            }
        }
        
        if (panInput != Vector2.zero)
        {
            Vector2 panAmount = panInput * panSpeed * Time.deltaTime * transform.position.z * 0.2f;
            moveAmount.x = - panAmount.x;
            moveAmount.y = - panAmount.y;
        }
        
        transform.position = currentPos + moveAmount;
    
    }
    
    // Input System callbacks
    public void OnPan(InputValue value)
    {
        panInput = value.Get<Vector2>();
        print(panInput);
    }

    public void OnInOut(InputValue value)
    {
        inOutInput = value.Get<float>();

    }

}