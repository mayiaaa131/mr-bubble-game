// ============================================
// 文件路径：Assets/scripts/UI/MapSelectPanel.cs
// ============================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MapSelectPanel : MonoBehaviour
{
    [Header("地图按钮列表容器（ScrollView的Content）")]
    [SerializeField] private Transform mapListContent;

    [Header("地图按钮 Prefab（含Button + Text）")]
    [SerializeField] private GameObject mapButtonPrefab;

    [Header("当前选择的地图 文本框")]
    [SerializeField] private TextMeshProUGUI currentMapText;

    [Header("当前选择的地图 预览图")]
    [SerializeField] private Image currentMapPreviewImage;
    [SerializeField] private Sprite defaultPreviewSprite;



    [Header("确认选择地图 按钮")]
    [SerializeField] private Button confirmThisMap;

    [Header("返回按钮（可选）")]
    [SerializeField] private Button backButton;

    [Header("房间设置")]
    [SerializeField] private string roomName = "新房间";
    [SerializeField] private int countdownSeconds = 300;
    [SerializeField] private int maxPlayers = 4;

    // ★ 只保留这一个事件
    public System.Action<string, string> OnMapSelected;

    public bool HasSelectedMap => !string.IsNullOrEmpty(_selectedMapId);

    private string _selectedMapId = "";
    private string _selectedMapName = "";

    // ★ 改：不再使用 Resources 路径
    // private const string MAP_RESOURCES_PATH = "MapData";

    private GameObject _highlightedBtn;
    private Color _normalBtnColor = Color.white;
    private Color _selectedBtnColor = new Color(0.5f, 0.85f, 1f);

    private void Awake()
    {

        // ★ [新增]绑定 confirmThisMap 按钮  
        if (confirmThisMap != null)
        {
            confirmThisMap.onClick.AddListener(OnClickConfirmThisMap);  // ✅ 方法名改正  
            Debug.Log("[MapSelectPanel] confirmThisMap 按钮已绑定");
        }
        else
        {
            Debug.LogWarning("[MapSelectPanel] confirmThisMap 未配置");
        }

        backButton?.onClick.AddListener(OnClickBack);
    }

    private void OnEnable()
    {
        // ★ 新增：确保按钮在面板显示时启用  
        if (confirmThisMap != null)
            confirmThisMap.interactable = true;
    }

    /// <summary>  
    /// ★ 重写 RefreshMapList，在显示地图列表时也重置按钮  
    /// </summary>  
    public void RefreshMapList()
    {
        foreach (Transform child in mapListContent)
            Destroy(child.gameObject);

        _selectedMapId = "";
        _selectedMapName = "";
        _highlightedBtn = null;
        UpdateCurrentMapText();

        if (currentMapPreviewImage != null)
            currentMapPreviewImage.sprite = defaultPreviewSprite;

        // ★ 新增：确保按钮启用  
        if (confirmThisMap != null)
        {
            confirmThisMap.interactable = true;
        }



        // ★ 改：从 Assets/JSON/MapData/ 读取地图 JSON 文件
        string mapDataPath = System.IO.Path.Combine(
            Application.dataPath, "json", "MapData"
        );

        if (!System.IO.Directory.Exists(mapDataPath))
        {
            Debug.LogWarning($"[MapSelectPanel] 地图数据目录不存在: {mapDataPath}");
            return;
        }

        // 获取所有 JSON 文件
        string[] jsonFiles = System.IO.Directory.GetFiles(mapDataPath, "*.json");

        if (jsonFiles == null || jsonFiles.Length == 0)
        {
            Debug.LogWarning($"[MapSelectPanel] 在 {mapDataPath} 下没找到任何地图文件！");
            return;
        }

        Debug.Log($"[MapSelectPanel] 找到 {jsonFiles.Length} 张地图");

        foreach (string filePath in jsonFiles)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            string mapId = fileName;

            try
            {
                string json = System.IO.File.ReadAllText(filePath);
                string mapName = ParseMapName(json, mapId);
                CreateMapButton(mapId, mapName);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MapSelectPanel] 读取地图文件失败: {filePath}, 错误: {ex.Message}");
            }
        }
    }

    private string ParseMapName(string json, string fallback)
    {
        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<MapNameHelper>(json);
            return string.IsNullOrEmpty(data?.mapName) ? fallback : data.mapName;
        }
        catch
        {
            Debug.LogWarning($"[MapSelectPanel] 解析地图名失败，使用文件名：{fallback}");
            return fallback;
        }
    }

    [System.Serializable]
    private class MapNameHelper
    {
        public string mapName;
    }

    private void CreateMapButton(string mapId, string mapName)
    {
        GameObject go = Instantiate(mapButtonPrefab, mapListContent);
        go.name = $"MapBtn_{mapId}";

        var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.text = mapName;
        else
        {
            var legacyText = go.GetComponentInChildren<Text>();
            if (legacyText != null) legacyText.text = mapName;
        }

        // ★ 改：从 Assets/JSON/MapPreviews/ 读取预览图
        Sprite preview = LoadPreviewFromAssets(mapId);

        var thumbImg = go.GetComponentInChildren<Image>();
        if (thumbImg != null)
            thumbImg.sprite = preview != null ? preview : defaultPreviewSprite;

        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            string capturedId = mapId;
            string capturedName = mapName;
            Sprite capturedSprite = preview;
            btn.onClick.AddListener(() => OnSelectMap(capturedId, capturedName, go, capturedSprite));
        }
    }

    // ★ 新增：从 Assets/JSON/MapPreviews/ 读取预览图
    private Sprite LoadPreviewFromAssets(string mapId)
    {
        string previewPath = System.IO.Path.Combine(
            Application.dataPath, "json", "MapPreviews", $"{mapId}.png"
        );

        // 也支持 jpg 格式
        if (!System.IO.File.Exists(previewPath))
        {
            previewPath = System.IO.Path.Combine(
                Application.dataPath, "json", "MapPreviews", $"{mapId}.jpg"
            );
        }

        if (!System.IO.File.Exists(previewPath))
        {
            Debug.LogWarning($"[MapSelectPanel] 找不到预览图: {mapId}");
            return null;
        }

        try
        {
            byte[] imageData = System.IO.File.ReadAllBytes(previewPath);
            Texture2D texture = new Texture2D(1, 1);
            texture.LoadImage(imageData);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                Vector2.one * 0.5f
            );
            return sprite;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MapSelectPanel] 加载预览图失败: {mapId}, 错误: {ex.Message}");
            return null;
        }
    }

    private void OnSelectMap(string mapId, string mapName, GameObject btnGo, Sprite previewSprite)
    {
        _selectedMapId = mapId;
        _selectedMapName = mapName;

        if (_highlightedBtn != null)
        {
            var oldImg = _highlightedBtn.GetComponent<Image>();
            if (oldImg != null) oldImg.color = _normalBtnColor;
        }

        var img = btnGo.GetComponent<Image>();
        if (img != null) img.color = _selectedBtnColor;
        _highlightedBtn = btnGo;

        if (currentMapPreviewImage != null)
            currentMapPreviewImage.sprite = previewSprite != null ? previewSprite : defaultPreviewSprite;

        UpdateCurrentMapText();



        OnMapSelected?.Invoke(mapId, mapName);

        Debug.Log($"[MapSelectPanel] 选择地图：{mapName}（{mapId}）");
    }

    private void UpdateCurrentMapText()
    {
        if (currentMapText == null) return;
        currentMapText.text = string.IsNullOrEmpty(_selectedMapId)
            ? "当前选择的地图：（未选择）"
            : $"当前选择的地图：{_selectedMapName}";
    }

    private void OnClickConfirmEnter()
    {
        if (string.IsNullOrEmpty(_selectedMapId))
        {
            Debug.LogWarning("[MapSelectPanel] 请先选择一张地图");
            return;
        }

        Debug.Log($"[MapSelectPanel] 确认选择地图，地图ID={_selectedMapId}，房间名={roomName}");



        // ★ 步骤1：创建房间（仅创建，不启动游戏）
        RoomInstance room = ServerRoomManager.Instance.CreateRoom(
            roomName,
            _selectedMapId,
            maxPlayers,
            countdownSeconds
        );

        Debug.Log($"[MapSelectPanel] ✅ 房间创建成功！roomId={room.roomData.roomId}");

        // ★ 步骤2：关闭地图选择界面并返回玩家编辑界面
        gameObject.SetActive(false);

        var uiManager = FindFirstObjectByType<RoomUIManager>();
        if (uiManager != null)
        {
            uiManager.ShowPanel(2);
            Debug.Log("[MapSelectPanel] 已返回到房间配置页");
        }
    }

    private void OnClickConfirmThisMap()
    {
        if (string.IsNullOrEmpty(_selectedMapId))
        {
            Debug.LogWarning("[MapSelectPanel] 请先选择一张地图");
            return;
        }

        Debug.Log($"[MapSelectPanel] 确认选择地图，地图ID={_selectedMapId}");

        if (confirmThisMap != null)
            confirmThisMap.interactable = false;

        try
        {
            // ★ 获取当前房间ID（从 RoomDetailManager）
            string currentRoomId = RoomDetailManager.Instance?.GetCurrentRoomId();

            if (string.IsNullOrEmpty(currentRoomId))
            {
                Debug.LogWarning("[MapSelectPanel] 无法获取当前房间ID");
                if (confirmThisMap != null)
                    confirmThisMap.interactable = true; // ★ 新增  
                return;
            }

            // ★ 方案1：尝试从 ServerRoomManager 更新运行时房间
            RoomInstance runtimeRoom = ServerRoomManager.Instance?.GetCurrentRoom();
            if (runtimeRoom != null)
            {
                runtimeRoom.roomData.mapId = _selectedMapId;
                Debug.Log($"✓ 运行时房间地图ID已更新: {_selectedMapId}");
                // ★ 新增：立即加载地图到运行时房间（关键！）  
                if (runtimeRoom.mapLoader != null)
                {
                    runtimeRoom.mapLoader.Init(_selectedMapId, runtimeRoom.transform.Find("MapRoot_" + currentRoomId));
                    runtimeRoom.mapLoader.LoadMap();
                    Debug.Log($"✓ 地图已在运行时加载: {_selectedMapId}");
                }
            }
            else
            {
                Debug.LogWarning("[MapSelectPanel] 未找到运行时房间实例");
            }

            // ★ 方案2：直接更新 JSON 文件（必须执行）
            JsonFileHandler.Instance.UpdateMapIdInRoom(currentRoomId, _selectedMapId);
            Debug.Log($"✓ 房间 JSON 已同步: {currentRoomId} → {_selectedMapId}");

            // ★ 方案3：同步到 RoomDataManager 内存缓存
            Room cachedRoom = RoomDataManager.Instance.GetRoomById(currentRoomId);
            if (cachedRoom != null)
            {
                cachedRoom.mapId = _selectedMapId;
                Debug.Log($"✓ RoomDataManager 缓存已更新");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 更新地图ID失败: {e.Message}");
            // ★ 新增：异常时重新启用按钮  
            if (confirmThisMap != null)
                confirmThisMap.interactable = true;
            return; // ★ 异常时返回，不关闭面板 
        }

        // 关闭地图选择界面并返回玩家编辑界面
        gameObject.SetActive(false);

        var uiManager = FindFirstObjectByType<RoomUIManager>();
        if (uiManager != null)
        {
            uiManager.ShowPanel(2);
            Debug.Log("[MapSelectPanel] 已返回到房间配置页");
        }
    }


    private void OnClickBack()
    {
        gameObject.SetActive(false);

        // ★ 新增：返回前重置地图选择状态  
        ResetMapSelection();

        var uiManager = FindFirstObjectByType<RoomUIManager>();
        if (uiManager != null)
        {
            uiManager.ShowPanel(2);
        }

        Debug.Log("[MapSelectPanel] 返回上一页");
    }

    /// <summary>  
    /// ★ 新增方法：重置地图选择状态  
    /// 当返回到地图选择界面时调用此方法  
    /// </summary>  
    private void ResetMapSelection()
    {
        _selectedMapId = "";
        _selectedMapName = "";
        _highlightedBtn = null;
        UpdateCurrentMapText();

        if (currentMapPreviewImage != null)
            currentMapPreviewImage.sprite = defaultPreviewSprite;

        // ★ 关键：重新启用确认按钮（以便下次可以选择）  
        if (confirmThisMap != null)
        {
            confirmThisMap.interactable = true;
            Debug.Log("[MapSelectPanel] 确认按钮已重新启用");
        }

        Debug.Log("[MapSelectPanel] 地图选择状态已重置");
    }

}
