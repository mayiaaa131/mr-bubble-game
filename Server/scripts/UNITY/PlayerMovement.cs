using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 2f;

    void Update()
    {
        // WASD 移动（平面移动）
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveX, 0, moveZ).normalized * moveSpeed * Time.deltaTime;
        transform.position += movement;

        // 鼠标控制旋转（模拟头显旋转）
        if (Input.GetMouseButton(1)) // 右键拖拽
        {
            float rotationX = Input.GetAxis("Mouse X") * rotationSpeed;
            transform.Rotate(0, rotationX, 0);
        }

        // 打印位置用于 Debug
        if (movement.magnitude > 0)
        {
            Debug.Log($"[玩家位置] {transform.position}");
        }
    }
}
