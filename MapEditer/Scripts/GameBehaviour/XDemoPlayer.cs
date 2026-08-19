//------------------------------------
// XDemoPlayer - Character Controller & Animation Manager
//------------------------------------

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ✅ 修改点 1：缺失类型补丁定义
// 战斗显示管理器 - 提供战斗相机相关配置
public static class BattleDisplayerMgr
{
    // 战斗摄像机偏移量
    public static readonly Vector3 BATTLE_CAMERA_OFFSET = new Vector3(0, 2.5f, -5f);
    // 战斗摄像机视野角度
    public const float BATTLE_CAMERA_FOV = 45f;
}

// ✅ 修改点 2：日志级别枚举定义
public enum LogLevel
{
    INFO,
    WARN,
    ERROR
}

// ✅ 修改点 3：日志系统补丁
public static class Log
{
    /// <summary>
    /// 输出日志信息
    /// </summary>
    public static void Write(LogLevel level, string message)
    {
        // 将日志输出到 Unity Console
        switch (level)
        {
            case LogLevel.INFO:
                Debug.Log($"[INFO] {message}");
                break;
            case LogLevel.WARN:
                Debug.LogWarning($"[WARN] {message}");
                break;
            case LogLevel.ERROR:
                Debug.LogError($"[ERROR] {message}");
                break;
        }
    }
}

// ============================================================================
// XDemoPlayer - 角色控制与动画管理
// ============================================================================

[AddComponentMenu("Demo/XDemoPlayer")]
public class XDemoPlayer : MonoBehaviour
{
    // 动画状态枚举
    public enum AnimationState
    {
        Idle = 0,       // 待机
        Walk = 1,       // 行走
        Run = 2,        // 奔跑
        Jump = 3,       // 跳跃
        Attack = 4,     // 攻击
        Skill = 5,      // 技能
        Die = 6         // 死亡
    }

    // ========================================================================
    // 属性定义
    // ========================================================================

    [Header("移动速度")]
    public float m_fWalkSpeed = 3f;         // 行走速度
    public float m_fRunSpeed = 6f;          // 奔跑速度
    public float m_fJumpForce = 5f;         // 跳跃力度
    public float m_fGroundDrag = 0.3f;      // 地面阻力
    public float m_fAirDrag = 0.1f;         // 空中阻力

    [Header("摄像机设置")]
    public Camera m_MainCamera;             // 主摄像机
    public Transform m_CameraTransform;     // 摄像机 Transform
    public float m_fCameraDistance = 5f;    // 摄像机距离
    public float m_fCameraHeight = 2f;      // 摄像机高度
    public float m_fCameraRotateSpeed = 2f; // 摄像机旋转速度
    public float m_fCameraZoomSpeed = 2f;   // 摄像机缩放速度

    [Header("战斗摄像机模式")]
    public bool m_bUseFightCamera = false;  // 是否使用战斗摄像机
    private Vector3 m_vFightCameraOffset;   // 战斗摄像机偏移
    private float m_fFightCameraFOV;        // 战斗摄像机 FOV

    [Header("角色引用")]
    public Animator m_Animator;             // 动画控制器
    public CharacterController m_CharCtrl;  // 角色控制器
    public Transform m_ModelTransform;      // 模型 Transform
    public float m_fModelRotateSpeed = 8f;  // 模型旋转速度

    // 内部状态
    private AnimationState m_CurrentAnimState = AnimationState.Idle;
    private Vector3 m_vMovementVelocity = Vector3.zero;
    private bool m_bIsGrounded = true;
    private bool m_bIsRunning = false;
    private float m_fCameraRotationX = 0f;
    private float m_fCameraRotationY = 0f;

    // 动画参数 Hash
    private int m_HashAnimState;
    private int m_HashIsMoving;
    private int m_HashIsRunning;
    private int m_HashIsGrounded;

    // ========================================================================
    // 生命周期函数
    // ========================================================================

    void Start()
    {
        // ✅ 初始化缓存
        m_HashAnimState = Animator.StringToHash("AnimState");
        m_HashIsMoving = Animator.StringToHash("IsMoving");
        m_HashIsRunning = Animator.StringToHash("IsRunning");
        m_HashIsGrounded = Animator.StringToHash("IsGrounded");

        // ✅ 获取组件引用
        if (m_Animator == null)
            m_Animator = GetComponent<Animator>();

        if (m_CharCtrl == null)
            m_CharCtrl = GetComponent<CharacterController>();

        if (m_MainCamera == null)
            m_MainCamera = Camera.main;

        if (m_CameraTransform == null && m_MainCamera != null)
            m_CameraTransform = m_MainCamera.transform;

        // ✅ 模型 Transform 通常是第一个子对象
        if (m_ModelTransform == null && transform.childCount > 0)
            m_ModelTransform = transform.GetChild(0);

        // ✅ 初始化战斗摄像机参数
        m_vFightCameraOffset = BattleDisplayerMgr.BATTLE_CAMERA_OFFSET;
        m_fFightCameraFOV = BattleDisplayerMgr.BATTLE_CAMERA_FOV;

        // 初始化摄像机旋转
        if (m_CameraTransform != null)
        {
            Vector3 eulerAngles = m_CameraTransform.eulerAngles;
            m_fCameraRotationX = eulerAngles.x;
            m_fCameraRotationY = eulerAngles.y;
        }

        Log.Write(LogLevel.INFO, "XDemoPlayer initialized successfully");
    }

    void Update()
    {
        if (m_CharCtrl == null)
            return;

        // 更新地面状态
        UpdateGroundState();

        // 获取输入
        Vector3 moveDir = GetMovementInput();

        // 处理摄像机
        HandleCameraInput();

        // 处理跳跃
        HandleJumpInput();

        // 应用移动
        ApplyMovement(moveDir);

        // 更新动画
        UpdateAnimations();
    }

    // ========================================================================
    // 移动与输入处理
    // ========================================================================

    /// <summary>
    /// 获取移动输入方向
    /// </summary>
    private Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");  // A/D 或左右箭头
        float vertical = Input.GetAxis("Vertical");      // W/S 或上下箭头

        Vector3 moveDir = Vector3.zero;

        // 基于摄像机方向计算移动方向
        if (m_CameraTransform != null)
        {
            Vector3 forward = m_CameraTransform.forward;
            Vector3 right = m_CameraTransform.right;

            // 忽略 Y 轴，使移动始终在地面上
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();

            moveDir = (forward * vertical + right * horizontal).normalized;
        }
        else
        {
            // 基于角色本身的方向
            moveDir = new Vector3(horizontal, 0, vertical).normalized;
        }

        // 处理跑步
        m_bIsRunning = Input.GetKey(KeyCode.LeftShift);

        return moveDir;
    }

    /// <summary>
    /// 处理摄像机输入（旋转和缩放）
    /// </summary>
    private void HandleCameraInput()
    {
        if (m_CameraTransform == null)
            return;

        // 右键拖拽旋转摄像机
        if (Input.GetMouseButton(1))
        {
            float deltaX = Input.GetAxis("Mouse X");
            float deltaY = Input.GetAxis("Mouse Y");

            m_fCameraRotationY += deltaX * m_fCameraRotateSpeed;
            m_fCameraRotationX -= deltaY * m_fCameraRotateSpeed;
            m_fCameraRotationX = Mathf.Clamp(m_fCameraRotationX, -30f, 60f);
        }

        // 滚轮缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        m_fCameraDistance -= scroll * m_fCameraZoomSpeed;
        m_fCameraDistance = Mathf.Clamp(m_fCameraDistance, 1f, 15f);

        // F 键切换战斗摄像机
        if (Input.GetKeyDown(KeyCode.F))
        {
            m_bUseFightCamera = !m_bUseFightCamera;
            Log.Write(LogLevel.INFO, $"战斗摄像机切换: {(m_bUseFightCamera ? "开启" : "关闭")}");
        }

        UpdateCameraPosition();
    }

    /// <summary>
    /// 处理跳跃输入
    /// </summary>
    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && m_bIsGrounded)
        {
            m_vMovementVelocity.y = m_fJumpForce;
            m_bIsGrounded = false;
            Log.Write(LogLevel.INFO, "Jump!");
        }
    }

    /// <summary>
    /// 应用移动
    /// </summary>
    private void ApplyMovement(Vector3 moveDir)
    {
        if (m_CharCtrl == null)
            return;

        // 根据是否奔跑选择速度
        float speed = m_bIsRunning ? m_fRunSpeed : m_fWalkSpeed;

        // 计算水平移动
        Vector3 horizontalMovement = moveDir * speed;

        // 应用重力
        if (!m_bIsGrounded)
        {
            m_vMovementVelocity.y -= 9.8f * Time.deltaTime;
        }
        else
        {
            m_vMovementVelocity.y = 0;
        }

        // 合并速度
        Vector3 finalMovement = horizontalMovement + new Vector3(0, m_vMovementVelocity.y, 0);

        // 使用角色控制器移动
        m_CharCtrl.Move(finalMovement * Time.deltaTime);

        // 旋转模型面向移动方向
        if (moveDir.magnitude > 0.1f && m_ModelTransform != null)
        {
            Vector3 targetDir = moveDir;
            Quaternion targetRotation = Quaternion.LookRotation(targetDir);
            m_ModelTransform.rotation = Quaternion.Lerp(
                m_ModelTransform.rotation,
                targetRotation,
                m_fModelRotateSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// 更新地面状态
    /// </summary>
    private void UpdateGroundState()
    {
        if (m_CharCtrl == null)
            return;

        // 检查是否在地面上
        m_bIsGrounded = m_CharCtrl.isGrounded;

        if (m_bIsGrounded && m_vMovementVelocity.y < 0)
        {
            m_vMovementVelocity.y = 0;
        }
    }

    /// <summary>
    /// 更新摄像机位置
    /// </summary>
    private void UpdateCameraPosition()
    {
        if (m_CameraTransform == null)
            return;

        if (m_bUseFightCamera)
        {
            // 战斗摄像机模式：固定偏移
            Vector3 targetPos = transform.position + m_vFightCameraOffset;
            m_CameraTransform.position = Vector3.Lerp(m_CameraTransform.position, targetPos, Time.deltaTime * 5f);
            m_CameraTransform.LookAt(transform.position + Vector3.up * m_fCameraHeight);
            if (m_MainCamera != null)
                m_MainCamera.fieldOfView = m_fFightCameraFOV;
        }
        else
        {
            // 正常摄像机模式：围绕角色旋转
            Vector3 targetPos = transform.position
                + Vector3.up * m_fCameraHeight
                - (Quaternion.Euler(m_fCameraRotationX, m_fCameraRotationY, 0) * Vector3.forward) * m_fCameraDistance;

            m_CameraTransform.position = Vector3.Lerp(m_CameraTransform.position, targetPos, Time.deltaTime * 5f);
            m_CameraTransform.LookAt(transform.position + Vector3.up * m_fCameraHeight);
        }
    }

    // ========================================================================
    // 动画更新
    // ========================================================================

    /// <summary>
    /// 更新动画状态
    /// </summary>
    private void UpdateAnimations()
    {
        if (m_Animator == null)
            return;

        // 判断当前动画状态
        Vector3 horizontalVelocity = new Vector3(m_CharCtrl.velocity.x, 0, m_CharCtrl.velocity.z);
        bool isMoving = horizontalVelocity.magnitude > 0.1f;

        if (isMoving)
        {
            m_CurrentAnimState = m_bIsRunning ? AnimationState.Run : AnimationState.Walk;
        }
        else
        {
            m_CurrentAnimState = AnimationState.Idle;
        }

        if (!m_bIsGrounded)
        {
            m_CurrentAnimState = AnimationState.Jump;
        }

        // 设置动画参数
        m_Animator.SetInteger(m_HashAnimState, (int)m_CurrentAnimState);
        m_Animator.SetBool(m_HashIsMoving, isMoving);
        m_Animator.SetBool(m_HashIsRunning, m_bIsRunning);
        m_Animator.SetBool(m_HashIsGrounded, m_bIsGrounded);
    }

    // ========================================================================
    // 公共方法
    // ========================================================================

    /// <summary>
    /// 播放指定动画
    /// </summary>
    public void PlayAnimation(AnimationState state)
    {
        if (m_Animator == null)
            return;

        m_CurrentAnimState = state;
        m_Animator.SetInteger(m_HashAnimState, (int)state);
        Log.Write(LogLevel.INFO, $"Playing animation: {state}");
    }

    /// <summary>
    /// 设置移动速度
    /// </summary>
    public void SetSpeed(float walkSpeed, float runSpeed)
    {
        m_fWalkSpeed = walkSpeed;
        m_fRunSpeed = runSpeed;
    }

    /// <summary>
    /// 获取当前动画状态
    /// </summary>
    public AnimationState GetCurrentAnimationState()
    {
        return m_CurrentAnimState;
    }
}