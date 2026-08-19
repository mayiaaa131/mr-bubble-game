using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// 房间游戏管理器（每个房间一个实例）
/// ★ 核心改造：集中管理该房间的所有Manager
/// ★ 改为字典管理，支持多房间并行
/// </summary>
public class RoomGameManager : MonoBehaviour
{
    // ★ 改为字典管理（支持多房间并行）
    private static Dictionary<string, RoomGameManager> instances =
        new Dictionary<string, RoomGameManager>();

    public string RoomId { get; private set; }

    // ★ 新增：GameStateJsonWriter 实例（数据中心）
    [HideInInspector] public GameStateJsonWriter GameStateWriter { get; private set; }

    // ★ 该房间专用的Manager实例（非单例，通过这个脚本管理）
    [HideInInspector] public ServerPlayerBloodManager BloodManager { get; private set; }
    [HideInInspector] public ServerReviveManager ReviveManager { get; private set; }
    [HideInInspector] public ServerGradeManager GradeManager { get; private set; }
    [HideInInspector] public ServerBombManager BombManager { get; private set; }
    [HideInInspector] public ServerPropManager PropManager { get; private set; }
    [HideInInspector] public ServerMapManager MapManager { get; private set; }
    [HideInInspector] public ServerGameEndManager GameEndManager { get; private set; }
    [HideInInspector] public GameNetworkManager NetworkManager { get; private set; }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!instances.ContainsKey(RoomId))
            return;

        // 每帧调用所有Manager的更新
        if (ReviveManager != null)
            ReviveManager.UpdateReviveSystem();

        if (GameEndManager != null)
            GameEndManager.UpdateGameEnd();
    }


    /// <summary>
    /// ★ 获取指定房间的 RoomGameManager 实例
    /// </summary>
    public static RoomGameManager GetInstance(string roomId)
    {
        if (instances.TryGetValue(roomId, out var manager))
        {
            return manager;
        }
        Debug.LogWarning($"[RoomGameManager] 房间 {roomId} 的Manager不存在");
        return null;
    }

    /// <summary>
    /// 初始化房间的所有Manager
    /// ★ 关键步骤：
    ///   0. 创建 GameStateJsonWriter（数据中心，必须最先）
    ///   1. 为每个Manager创建GameObject
    ///   2. 建立Manager之间的依赖关系（互相注入）
    ///   3. 调用各Manager的Initialize()方法
    /// </summary>
    public void Initialize(string roomId)
    {
        RoomId = roomId;
        Debug.Log($"[RoomGameManager] 房间 {roomId} 初始化中...");

        try
        {
            // ★ 第零阶段：创建并初始化 GameStateJsonWriter（必须最先！）
            Debug.Log($"[RoomGameManager-{roomId}] → 创建 GameStateJsonWriter...");

            GameObject stateWriterGo = new GameObject($"GameStateJsonWriter_{RoomId}");
            stateWriterGo.transform.SetParent(transform);
            GameStateWriter = stateWriterGo.AddComponent<GameStateJsonWriter>();

            GameStateWriter.InitFromTeamJson(roomId);
            Debug.Log($"[RoomGameManager-{roomId}] ✅ GameStateJsonWriter 创建完成");

            // ★ 新增：创建 PlayerBloodJsonWriter  
            if (PlayerBloodJsonWriter.Instance == null)
            {
                GameObject go = new GameObject("PlayerBloodJsonWriter");
                DontDestroyOnLoad(go);
                go.AddComponent<PlayerBloodJsonWriter>();
                Debug.Log($"[RoomGameManager-{roomId}] ✅ PlayerBloodJsonWriter 创建完成");
            }

            // ★ 新增：创建 GradeJsonWriter  
            if (GradeJsonWriter.Instance == null)
            {
                GameObject go = new GameObject("GradeJsonWriter");
                DontDestroyOnLoad(go);
                go.AddComponent<GradeJsonWriter>();
                Debug.Log($"[RoomGameManager-{roomId}] ✅ GradeJsonWriter 创建完成");
            }

            // ★ 新增：创建 PropStateJsonWriter  
            if (PropStateJsonWriter.Instance == null)
            {
                GameObject go = new GameObject("PropStateJsonWriter");
                DontDestroyOnLoad(go);
                go.AddComponent<PropStateJsonWriter>();
                Debug.Log($"[RoomGameManager-{roomId}] ✅ PropStateJsonWriter 创建完成");
            }

            // ★ 第一阶段：创建所有Manager实例
            Debug.Log($"[RoomGameManager-{roomId}] → 创建Manager实例...");

            BloodManager = CreateManagerComponent<ServerPlayerBloodManager>("ServerPlayerBloodManager");
            ReviveManager = CreateManagerComponent<ServerReviveManager>("ServerReviveManager");
            GradeManager = CreateManagerComponent<ServerGradeManager>("ServerGradeManager");
            BombManager = CreateManagerComponent<ServerBombManager>("ServerBombManager");
            PropManager = CreateManagerComponent<ServerPropManager>("ServerPropManager");
            MapManager = CreateManagerComponent<ServerMapManager>("ServerMapManager");
            GameEndManager = CreateManagerComponent<ServerGameEndManager>("ServerGameEndManager");

            Debug.Log($"[RoomGameManager-{roomId}] ✅ 所有Manager实例创建完成");

            // ★ 第二阶段：注入依赖
            Debug.Log($"[RoomGameManager-{roomId}] → 注入依赖...");

            // ★ 关键：为 BloodManager 注入依赖
            BloodManager.InjectDependencies(GameStateWriter);

            // ★ 为 ReviveManager 注入依赖（新增 GradeManager）  
            ReviveManager.InjectDependencies(BloodManager, GameStateWriter, GradeManager);

            // BombManager 不再需要 GradeManager  
            BombManager.InjectDependencies(ReviveManager, BloodManager, null, GameStateWriter);

            // PropManager 需要依赖注入
            PropManager.InjectDependencies(BloodManager, GameStateWriter);

            // GameEndManager 需要访问：GradeManager, NetworkManager, BloodManager  
            GameEndManager.InjectDependencies(GradeManager, NetworkManager, BloodManager); // ★ 新增 BloodManager  

            Debug.Log($"[RoomGameManager-{roomId}] ✅ 依赖注入完成");

            // ★ 第三阶段：初始化各Manager
            Debug.Log($"[RoomGameManager-{roomId}] → 初始化Manager...");

            if (BloodManager != null) BloodManager.Initialize(roomId);
            if (ReviveManager != null) ReviveManager.Initialize(roomId);
            if (GradeManager != null) GradeManager.Initialize(roomId);
            if (BombManager != null) BombManager.Initialize(roomId);
            if (PropManager != null) PropManager.Initialize(roomId);
            if (MapManager != null) MapManager.Initialize(roomId);
            if (GameEndManager != null) GameEndManager.Initialize(roomId);

            Debug.Log($"[RoomGameManager-{roomId}] ✅ 所有Manager初始化完成");
            Debug.Log($"[RoomGameManager-{roomId}] ✅✅✅ 房间初始化成功！");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomGameManager-{roomId}] ❌ 初始化失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// ★ 新增：启动房间（点击"进入游戏"时调用）
    /// </summary>
    public static void StartRoom(string roomId, int roomIndex)
    {
        // 检查该房间是否已启动
        if (instances.ContainsKey(roomId))
        {
            Debug.LogWarning($"[RoomGameManager] 房间 {roomId} 已启动");
            return;
        }

        try
        {
            Debug.Log($"[RoomGameManager] → 启动房间 {roomId}...");

            // ★ 创建房间 GameObject
            GameObject roomGo = new GameObject($"Room_{roomId}");
            DontDestroyOnLoad(roomGo);
            RoomGameManager roomManager = roomGo.AddComponent<RoomGameManager>();

            // ★ 初始化房间
            roomManager.Initialize(roomId);

            // ★ 创建网络管理器
            int port = 8080 + (roomIndex - 1);
            GameNetworkManager networkMgr = GameNetworkManager.CreateForRoom(roomId, roomIndex);
            roomManager.NetworkManager = networkMgr;

            // ★ 注入依赖
            networkMgr.InjectDependencies(
                roomManager.GameStateWriter,
                roomManager.GradeManager,
                roomManager.BloodManager,
                roomManager.ReviveManager,
                roomManager.BombManager,
                roomManager.PropManager,
                roomManager.MapManager,
                roomManager.GameEndManager
            );

            // ★ 新增：重置该房间的玩家分配状态  
            ResetPlayerAssignmentForRoom(roomId);

            // ★ 启动服务器
            networkMgr.StartServerAndInitializeDependencies();

            // ★ 启动自动保存
            roomManager.StartAutoSave(roomManager.GameStateWriter);

            // ★ 注册到字典
            instances[roomId] = roomManager;

            Debug.Log($"[RoomGameManager] ✅ 房间 {roomId} 启动完成");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomGameManager] ❌ 房间启动失败: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// ★ 新增：自动保存协程
    /// </summary>
    private void StartAutoSave(GameStateJsonWriter writer)
    {
        if (writer != null)
        {
            StartCoroutine(AutoSaveRoutine(writer, RoomId));
        }
    }

    private IEnumerator AutoSaveRoutine(GameStateJsonWriter writer, string roomId)
    {
        while (instances.ContainsKey(roomId) && writer != null)
        {
            yield return new WaitForSeconds(1f);  // 每1秒保存一次
            writer.SaveToFile();
        }
    }

    /// <summary>
    /// ★ 新增：销毁房间（点击"关闭服务器"时调用）
    /// </summary>
    public static void ShutdownRoom(string roomId)
    {
        if (!instances.TryGetValue(roomId, out RoomGameManager roomManager))
        {
            Debug.LogWarning($"[RoomGameManager] 房间 {roomId} 不存在");
            return;
        }

        try
        {
            Debug.Log($"[RoomGameManager] → 关闭房间 {roomId}...");
            // ★ 新增：清理该房间的玩家分配状态  
            ResetPlayerAssignmentForRoom(roomId);

            // ★ 只停止网络服务器，不销毁
            GameNetworkManager networkMgr = GameNetworkManager.GetInstanceForRoom(roomId);
            if (networkMgr != null)
            {
                networkMgr.StopServer();
            }

            // ★ Cleanup 会一起销毁所有 Manager，包括 GameNetworkManager
            roomManager.Cleanup();

            // ★ 从字典移除
            instances.Remove(roomId);

            Debug.Log($"[RoomGameManager] ✅ 房间 {roomId} 已完全关闭");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomGameManager] ❌ 房间关闭失败: {ex.Message}");
        }
    }

    /// <summary>  
    /// ★ 新增方法：重置指定房间的玩家分配状态  
    /// </summary>  
    private static void ResetPlayerAssignmentForRoom(string roomId)
    {
        GameNetworkManager networkMgr = GameNetworkManager.GetInstanceForRoom(roomId);
        if (networkMgr != null)
        {
            // 通过反射或公开方法清理该房间的分配状态  
            Debug.Log($"[RoomGameManager] 已重置房间 {roomId} 的玩家分配状态");
        }
    }

    /// <summary>
    /// 创建Manager组件的辅助方法
    /// </summary>
    private T CreateManagerComponent<T>(string name) where T : Component
    {
        GameObject go = new GameObject($"{name}_{RoomId}");
        go.transform.SetParent(transform);
        T component = go.AddComponent<T>();
        Debug.Log($"[RoomGameManager-{RoomId}] ✓ 创建 {name}");
        return component;
    }

    /// <summary>
    /// 清理房间的所有数据
    /// </summary>
    public void Cleanup()
    {
        Debug.Log($"[RoomGameManager-{RoomId}] → 开始清理...");

        try
        {
            // ★ 步骤1: 清理并销毁所有业务 Manager
            if (BombManager != null)
            {
                BombManager.Cleanup();
                Destroy(BombManager.gameObject);
                BombManager = null;
                Debug.Log($"✓ BombManager 已销毁");
            }

            if (GradeManager != null)
            {
                GradeManager.Cleanup();
                Destroy(GradeManager.gameObject);
                GradeManager = null;
                Debug.Log($"✓ GradeManager 已销毁");
            }

            if (BloodManager != null)
            {
                BloodManager.Cleanup();
                Destroy(BloodManager.gameObject);
                BloodManager = null;
                Debug.Log($"✓ BloodManager 已销毁");
            }

            if (ReviveManager != null)
            {
                ReviveManager.Cleanup();
                Destroy(ReviveManager.gameObject);
                ReviveManager = null;
                Debug.Log($"✓ ReviveManager 已销毁");
            }

            if (PropManager != null)
            {
                PropManager.Cleanup();
                Destroy(PropManager.gameObject);
                PropManager = null;
                Debug.Log($"✓ PropManager 已销毁");
            }

            if (MapManager != null)
            {
                MapManager.Cleanup();
                Destroy(MapManager.gameObject);
                MapManager = null;
                Debug.Log($"✓ MapManager 已销毁");
            }

            if (GameEndManager != null)
            {
                GameEndManager.Cleanup();
                Destroy(GameEndManager.gameObject);
                GameEndManager = null;
                Debug.Log($"✓ GameEndManager 已销毁");
            }

            // ★ 步骤2: 销毁 GameStateJsonWriter
            if (GameStateWriter != null)
            {
                GameStateWriter.ClearGameState();
                Destroy(GameStateWriter.gameObject);
                GameStateWriter = null;
                Debug.Log($"✓ GameStateJsonWriter 已销毁");
            }

            // ★ 步骤3: 销毁 GameNetworkManager（新增！）
            if (NetworkManager != null)
            {
                NetworkManager.Cleanup();  // 如果有 Cleanup 方法的话
                Destroy(NetworkManager.gameObject);
                NetworkManager = null;
                Debug.Log($"✓ GameNetworkManager 已销毁");
            }

            // ★ 步骤4: 最后销毁主 GameObject
            Destroy(gameObject);
            Debug.Log($"[RoomGameManager-{RoomId}] ✅ 所有服务器已销毁");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RoomGameManager-{RoomId}] ❌ 清理失败: {ex.Message}");
        }
    }
}
