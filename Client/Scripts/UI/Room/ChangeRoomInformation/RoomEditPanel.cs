using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 房间编辑面板（房间信息编辑界面）
/// 显示房间信息并允许编辑
/// ★ 改进：使用 TMP_InputField 替代 valueText
/// ★ 新增：删除房间功能
/// ★ 新增：游戏配置字段（积分系数、玩家最大血量、游玩局数、游戏倒计时）
/// ★ 新增：详细的分数系数编辑（基础分、击杀系数、死亡系数、助攻系数）
/// ★ 新增：Image 组件支持，同步显示/隐藏背景和文本
/// </summary>
public class RoomEditPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomIdText;                 // 房间ID显示（只读）
    [SerializeField] private TMP_InputField roomNameText;               // 房间名称显示
    [SerializeField] private TextMeshProUGUI maxPlayersText;             // 最大玩家数
    [SerializeField] private TextMeshProUGUI currentPlayersText;         // 当前玩家数
    [SerializeField] private TextMeshProUGUI mapNameText;                // 地图名称

    [Header("★ 游戏配置区域 - 基础设置")]
    [SerializeField] private TextMeshProUGUI maxHealthText;              // 玩家最大血量标签
    [SerializeField] private Slider maxHealthSlider;                    // 最大血量滑块 [1, 6]
    [SerializeField] private TMP_InputField maxHealthInputField;         // ★ 改为 InputField（替代 valueText）

    [SerializeField] private TextMeshProUGUI gameModeText;               // 游玩局数显示
    [SerializeField] private TMP_Dropdown gameModeDropdown;              // 游玩局数下拉菜单

    [SerializeField] private TextMeshProUGUI countdownText;              // 游戏倒计时显示
    [SerializeField] private TMP_InputField countdownInputField;         // 倒计时输入框

    [Header("★ 详细分数系数设置")]
    [SerializeField] private TextMeshProUGUI baseScoreText;              // 基础分标签
    [SerializeField] private Slider baseScoreSlider;                    // 基础分滑块 [0, 500]
    [SerializeField] private TMP_InputField baseScoreInputField;         // ★ 基础分 InputField

    [SerializeField] private TextMeshProUGUI killCoefficientText;        // 击杀系数标签
    [SerializeField] private Slider killCoefficientSlider;              // 击杀系数滑块 [-100, 100]
    [SerializeField] private TMP_InputField killCoefficientInputField;   // ★ 击杀系数 InputField

    [SerializeField] private TextMeshProUGUI deathCoefficientText;       // 死亡系数标签
    [SerializeField] private Slider deathCoefficientSlider;             // 死亡系数滑块 [-100, 0]
    [SerializeField] private TMP_InputField deathCoefficientInputField;  // ★ 死亡系数 InputField

    [SerializeField] private TextMeshProUGUI assistCoefficientText;      // 助攻系数标签
    [SerializeField] private Slider assistCoefficientSlider;            // 助攻系数滑块 [-100, 100]
    [SerializeField] private TMP_InputField assistCoefficientInputField; // ★ 助攻系数 InputField

    [Header("按钮")]
    [SerializeField] private Button confirmButton;            // 确认按钮
    [SerializeField] private Button returnButton;             // 返回按钮
    [SerializeField] private Button deleteButton;             // 删除按钮

    [Header("★ 删除确认弹窗（完整配置）")]
    [SerializeField] private GameObject deleteConfirmPanel;        // 弹窗容器
    [SerializeField] private Image deleteConfirmBackground;        // 背景图（Image 组件）
    [SerializeField] private TextMeshProUGUI deleteConfirmText;    // 删除确认文本
    [SerializeField] private Button deleteConfirmButton;           // 确认删除按钮
    [SerializeField] private Button deleteCancelButton;            // 取消删除按钮

    [Header("★ 更新成功弹窗（完整配置）")]
    [SerializeField] private GameObject updateSuccessPanel;        // 弹窗容器
    [SerializeField] private Image updateSuccessBackground;        // 背景图（Image 组件）
    [SerializeField] private TextMeshProUGUI updateSuccessText;    // 更新成功文本

    private string _currentRoomId;
    private Room _currentRoom;

    // 标记当前打开模式
    private bool _isFromRoomList = false;  // true=从房间列表打开，false=从玩家编辑界面打开

    private void Start( )
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmButtonClicked);

        if (returnButton != null)
            returnButton.onClick.AddListener(OnReturnButtonClicked);

        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);

        if (deleteConfirmButton != null)
            deleteConfirmButton.onClick.AddListener(OnConfirmDelete);

        if (deleteCancelButton != null)
            deleteCancelButton.onClick.AddListener(OnCancelDelete);

        // ★ 绑定游戏配置的事件
        SetupGameConfigUI();
    }

    /// <summary>
    /// ★ 设置游戏配置 UI（包含详细的分数系数）
    /// </summary>
    private void SetupGameConfigUI( )
    {
        // ========== 基础分滑块配置 ==========
        if (baseScoreSlider != null && baseScoreInputField != null)
        {
            baseScoreSlider.minValue = 0;
            baseScoreSlider.maxValue = 500;
            baseScoreSlider.wholeNumbers = true;

            // ★ Slider → InputField 同步（用户拖动滑块时）
            baseScoreSlider.onValueChanged.AddListener(( value ) =>
            {
                if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                {
                    _currentRoom.scoreCoefficients.baseScore = (int)value;
                    baseScoreInputField.text = ((int)value).ToString();
                    Debug.Log($"✓ 基础分已更新: {(int)value}");
                }
            });

            // ★ InputField → Slider 同步（用户直接输入时）
            baseScoreInputField.onEndEdit.AddListener(( value ) =>
            {
                if (int.TryParse(value, out int score) && score >= 0 && score <= 500)
                {
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                    {
                        _currentRoom.scoreCoefficients.baseScore = score;
                        baseScoreSlider.value = score;
                        Debug.Log($"✓ 基础分已更新: {score}");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ 基础分必须在 0-500 之间");
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                        baseScoreInputField.text = _currentRoom.scoreCoefficients.baseScore.ToString();
                }
            });
        }

        // ========== 击杀系数滑块配置 ==========
        if (killCoefficientSlider != null && killCoefficientInputField != null)
        {
            killCoefficientSlider.minValue = -100;
            killCoefficientSlider.maxValue = 100;
            killCoefficientSlider.wholeNumbers = true;

            // ★ Slider → InputField 同步
            killCoefficientSlider.onValueChanged.AddListener(( value ) =>
            {
                if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                {
                    _currentRoom.scoreCoefficients.killCoefficient = (int)value;
                    killCoefficientInputField.text = ((int)value).ToString();
                    Debug.Log($"✓ 击杀系数已更新: {(int)value}");
                }
            });

            // ★ InputField → Slider 同步
            killCoefficientInputField.onEndEdit.AddListener(( value ) =>
            {
                if (int.TryParse(value, out int coefficient) && coefficient >= -100 && coefficient <= 100)
                {
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                    {
                        _currentRoom.scoreCoefficients.killCoefficient = coefficient;
                        killCoefficientSlider.value = coefficient;
                        Debug.Log($"✓ 击杀系数已更新: {coefficient}");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ 击杀系数必须在 -100 到 100 之间");
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                        killCoefficientInputField.text = _currentRoom.scoreCoefficients.killCoefficient.ToString();
                }
            });
        }

        // ========== 死亡系数滑块配置 ==========
        if (deathCoefficientSlider != null && deathCoefficientInputField != null)
        {
            deathCoefficientSlider.minValue = -100;
            deathCoefficientSlider.maxValue = 0;
            deathCoefficientSlider.wholeNumbers = true;

            // ★ Slider → InputField 同步
            deathCoefficientSlider.onValueChanged.AddListener(( value ) =>
            {
                if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                {
                    _currentRoom.scoreCoefficients.deathCoefficient = (int)value;
                    deathCoefficientInputField.text = ((int)value).ToString();
                    Debug.Log($"✓ 死亡系数已更新: {(int)value}");
                }
            });

            // ★ InputField → Slider 同步
            deathCoefficientInputField.onEndEdit.AddListener(( value ) =>
            {
                if (int.TryParse(value, out int coefficient) && coefficient >= -100 && coefficient <= 0)
                {
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                    {
                        _currentRoom.scoreCoefficients.deathCoefficient = coefficient;
                        deathCoefficientSlider.value = coefficient;
                        Debug.Log($"✓ 死亡系数已更新: {coefficient}");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ 死亡系数必须在 -100 到 0 之间");
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                        deathCoefficientInputField.text = _currentRoom.scoreCoefficients.deathCoefficient.ToString();
                }
            });
        }

        // ========== 助攻系数滑块配置 ==========
        if (assistCoefficientSlider != null && assistCoefficientInputField != null)
        {
            assistCoefficientSlider.minValue = -100;
            assistCoefficientSlider.maxValue = 100;
            assistCoefficientSlider.wholeNumbers = true;

            // ★ Slider → InputField 同步
            assistCoefficientSlider.onValueChanged.AddListener(( value ) =>
            {
                if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                {
                    _currentRoom.scoreCoefficients.assistCoefficient = (int)value;
                    assistCoefficientInputField.text = ((int)value).ToString();
                    Debug.Log($"✓ 助攻系数已更新: {(int)value}");
                }
            });

            // ★ InputField → Slider 同步
            assistCoefficientInputField.onEndEdit.AddListener(( value ) =>
            {
                if (int.TryParse(value, out int coefficient) && coefficient >= -100 && coefficient <= 100)
                {
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                    {
                        _currentRoom.scoreCoefficients.assistCoefficient = coefficient;
                        assistCoefficientSlider.value = coefficient;
                        Debug.Log($"✓ 助攻系数已更新: {coefficient}");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ 助攻系数必须在 -100 到 100 之间");
                    if (_currentRoom != null && _currentRoom.scoreCoefficients != null)
                        assistCoefficientInputField.text = _currentRoom.scoreCoefficients.assistCoefficient.ToString();
                }
            });
        }

        // ========== 最大血量滑块配置 ==========
        if (maxHealthSlider != null && maxHealthInputField != null)
        {
            maxHealthSlider.minValue = 1;
            maxHealthSlider.maxValue = 6;
            maxHealthSlider.wholeNumbers = true;

            // ★ Slider → InputField 同步
            maxHealthSlider.onValueChanged.AddListener(( value ) =>
            {
                if (_currentRoom != null)
                {
                    _currentRoom.maxPlayerHealth = (int)value;
                    maxHealthInputField.text = ((int)value).ToString();
                    Debug.Log($"✓ 玩家最大血量已更新: {(int)value}");
                }
            });

            // ★ InputField → Slider 同步
            maxHealthInputField.onEndEdit.AddListener(( value ) =>
            {
                if (int.TryParse(value, out int health) && health >= 6 && health <= 100)
                {
                    if (_currentRoom != null)
                    {
                        _currentRoom.maxPlayerHealth = health;
                        maxHealthSlider.value = health;
                        Debug.Log($"✓ 玩家最大血量已更新: {health}");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ 血量必须在 1-6 之间");
                    if (_currentRoom != null)
                        maxHealthInputField.text = _currentRoom.maxPlayerHealth.ToString();
                }
            });
        }

        // ========== 游玩局数下拉菜单配置 ==========
        if (gameModeDropdown != null)
        {
            gameModeDropdown.options.Clear();
            gameModeDropdown.options.Add(new TMP_Dropdown.OptionData("五局三胜"));
            gameModeDropdown.options.Add(new TMP_Dropdown.OptionData("三局两胜"));
            gameModeDropdown.options.Add(new TMP_Dropdown.OptionData("单局游戏"));

            gameModeDropdown.onValueChanged.AddListener(( index ) =>
            {
                if (_currentRoom != null)
                {
                    string mode = index switch
                    {
                        0 => "BO5",
                        1 => "BO3",
                        2 => "SingleRound",
                        _ => "SingleRound"
                    };
                    _currentRoom.gameMode = mode;
                    Debug.Log($"✓ 游玩局数已更新: {gameModeDropdown.options[ index ].text}");
                }
            });
        }

        // ========== 游戏倒计时输入框配置 ==========
        if (countdownInputField != null)
        {
            countdownInputField.onEndEdit.AddListener(( value ) =>
            {
                if (int.TryParse(value, out int seconds) && seconds > 0)
                {
                    if (_currentRoom != null)
                    {
                        _currentRoom.countdownSeconds = seconds;
                        Debug.Log($"✓ 游戏倒计时已更新: {seconds}秒");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ 倒计时必须为正整数");
                    if (_currentRoom != null)
                        countdownInputField.text = _currentRoom.countdownSeconds.ToString();
                }
            });
        }
    }

    /// <summary>
    /// 初始化编辑面板
    /// ★ 新增参数：isFromRoomList，用于判断返回位置
    /// </summary>
    public void InitializeWithRoom( string roomId, bool isFromRoomList = false )
    {
        Debug.Log($"[RoomEditPanel] 初始化房间编辑面板: {roomId}");
        Debug.Log($"[RoomEditPanel] 打开来源: {(isFromRoomList ? "房间列表" : "玩家编辑界面")}");

        _currentRoomId = roomId;
        _isFromRoomList = isFromRoomList;

        // 从 JSON 加载房间信息
        _currentRoom = RoomDataManager.Instance.GetRoomById(roomId);

        if (_currentRoom == null)
        {
            Debug.LogError($"❌ 无法找到房间: {roomId}");
            return;
        }

        // 确保 scoreCoefficients 已初始化
        if (_currentRoom.scoreCoefficients == null)
        {
            _currentRoom.scoreCoefficients = new Room.ScoreCoefficients();
            Debug.Log($"✓ 已为房间初始化 scoreCoefficients");
        }

        RefreshUI();
    }

    /// <summary>
    /// 刷新 UI 显示房间信息
    /// </summary>
    private void RefreshUI( )
    {
        if (_currentRoom == null)
        {
            Debug.LogError("❌ 当前房间为空");
            return;
        }

        // ========== 房间基本信息 ==========
        if (roomIdText != null)
            roomIdText.text = _currentRoom.roomId;

        if (roomNameText != null)
            roomNameText.text = _currentRoom.roomName;

        if (maxPlayersText != null)
            maxPlayersText.text = $"{_currentRoom.maxPlayers}";

        if (currentPlayersText != null)
            currentPlayersText.text = $"{_currentRoom.currentPlayers}";

        if (mapNameText != null)
            mapNameText.text = $"{_currentRoom.mapId}";

        // ========== 基础分显示 ==========
        if (baseScoreText != null)
            baseScoreText.text = "基础分";
        if (baseScoreSlider != null && _currentRoom.scoreCoefficients != null)
            baseScoreSlider.value = _currentRoom.scoreCoefficients.baseScore;
        if (baseScoreInputField != null && _currentRoom.scoreCoefficients != null)
            baseScoreInputField.text = _currentRoom.scoreCoefficients.baseScore.ToString();

        // ========== 击杀系数显示 ==========
        if (killCoefficientText != null)
            killCoefficientText.text = "击杀系数";
        if (killCoefficientSlider != null && _currentRoom.scoreCoefficients != null)
            killCoefficientSlider.value = _currentRoom.scoreCoefficients.killCoefficient;
        if (killCoefficientInputField != null && _currentRoom.scoreCoefficients != null)
            killCoefficientInputField.text = _currentRoom.scoreCoefficients.killCoefficient.ToString();

        // ========== 死亡系数显示 ==========
        if (deathCoefficientText != null)
            deathCoefficientText.text = "死亡系数";
        if (deathCoefficientSlider != null && _currentRoom.scoreCoefficients != null)
            deathCoefficientSlider.value = _currentRoom.scoreCoefficients.deathCoefficient;
        if (deathCoefficientInputField != null && _currentRoom.scoreCoefficients != null)
            deathCoefficientInputField.text = _currentRoom.scoreCoefficients.deathCoefficient.ToString();

        // ========== 助攻系数显示 ==========
        if (assistCoefficientText != null)
            assistCoefficientText.text = "助攻系数";
        if (assistCoefficientSlider != null && _currentRoom.scoreCoefficients != null)
            assistCoefficientSlider.value = _currentRoom.scoreCoefficients.assistCoefficient;
        if (assistCoefficientInputField != null && _currentRoom.scoreCoefficients != null)
            assistCoefficientInputField.text = _currentRoom.scoreCoefficients.assistCoefficient.ToString();

        // ========== 最大血量显示 ==========
        if (maxHealthText != null)
            maxHealthText.text = "玩家最大血量";
        if (maxHealthSlider != null)
            maxHealthSlider.value = _currentRoom.maxPlayerHealth;
        if (maxHealthInputField != null)
            maxHealthInputField.text = _currentRoom.maxPlayerHealth.ToString();

        // ========== 游玩局数显示 ==========
        if (gameModeText != null)
            gameModeText.text = "游玩局数";
        if (gameModeDropdown != null)
        {
            int modeIndex = _currentRoom.gameMode switch
            {
                "five_best_three" => 0,
                "three_best_two" => 1,
                "single" => 2,
                _ => 2
            };
            gameModeDropdown.value = modeIndex;
        }

        // ========== 倒计时显示 ==========
        if (countdownText != null)
            countdownText.text = "游戏倒计时";
        if (countdownInputField != null)
            countdownInputField.text = _currentRoom.countdownSeconds.ToString();

        Debug.Log($"✓ 房间编辑面板 UI 已刷新");
    }

    /// <summary>
    /// 确认按钮被点击 - 保存房间信息
    /// </summary>
    private void OnConfirmButtonClicked( )
    {
        Debug.Log(">>> 房间编辑 - 确认按钮被点击");

        if (_currentRoom == null)
        {
            Debug.LogError("❌ 当前房间为空");
            return;
        }

        // ★ 步骤1：更新所有字段（不仅仅是房间名称）
        if (roomNameText != null)
        {
            string newName = roomNameText.text.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                _currentRoom.roomName = newName;
            }
        }

        // ★ 步骤2：确保所有Slider同步完成
        if (countdownInputField != null)
        {
            if (int.TryParse(countdownInputField.text, out int countdown))
            {
                _currentRoom.countdownSeconds = countdown;
            }
        }

        if (maxHealthInputField != null)
        {
            if (int.TryParse(maxHealthInputField.text, out int health))
            {
                _currentRoom.maxPlayerHealth = health;
            }
        }

        // ★ 步骤3：保存到 JSON
        RoomDetailManager.Instance.UpdateRoomData(_currentRoom);
        Debug.Log($"✓ 房间数据已保存到 JSON 文件");

        // ★ 步骤4：更新 Rooms.json 列表（如果从房间列表打开）
        if (_isFromRoomList)
        {
            JsonFileHandler.Instance.UpdateRoomNameInList(_currentRoom.roomId, _currentRoom.roomName);
            Debug.Log($"✓ 房间列表已更新 (Rooms.json)");
        }

        ShowUpdateSuccessPanel();
    }


    /// <summary>
    /// 返回按钮被点击 - 返回上一级
    /// ★ 修改：根据打开来源决定返回位置
    /// </summary>
    private void OnReturnButtonClicked( )
    {
        Debug.Log(">>> 房间编辑 - 返回按钮被点击");
        Debug.Log($"   返回位置: {(_isFromRoomList ? "房间列表" : "玩家编辑界面")}");

        RoomUIManager uiManager = FindFirstObjectByType<RoomUIManager>();

        if (uiManager != null)
        {
            if (_isFromRoomList)
            {
                uiManager.HideRoomEditPanel();
                RoomListManager.Instance.RefreshRoomList();
                Debug.Log("✓ 已返回房间列表");
            }
            else
            {
                uiManager.HideRoomEditPanel();
                Debug.Log("✓ 已返回玩家编辑界面");
            }
        }
    }

    /// <summary>
    /// 删除按钮被点击
    /// </summary>
    private void OnDeleteButtonClicked( )
    {
        Debug.Log(">>> 房间编辑 - 删除按钮被点击");

        if (_currentRoom == null)
        {
            Debug.LogError("❌ 当前房间为空");
            return;
        }

        ShowDeleteConfirmPanel();
    }

    /// <summary>
    /// ★ 显示删除确认面板（完整的 Image + Text 同步）
    /// </summary>
    private void ShowDeleteConfirmPanel( )
    {
        if (deleteConfirmPanel == null)
        {
            Debug.LogError("❌ deleteConfirmPanel 未配置");
            return;
        }

        // ★ 显示面板容器
        deleteConfirmPanel.SetActive(true);

        // ★ 同步显示背景 Image
        if (deleteConfirmBackground != null)
        {
            deleteConfirmBackground.enabled = true;
            Debug.Log("✓ deleteConfirmBackground 已显示");
        }
        else
        {
            Debug.LogWarning("⚠ deleteConfirmBackground 未配置，面板可能缺少背景");
        }

        // ★ 同步显示和更新文本
        if (deleteConfirmText != null)
        {
            deleteConfirmText.enabled = true;
            deleteConfirmText.text = $"是否删除 \"{_currentRoom.roomName}\" ？\n房间ID: {_currentRoom.roomId}\n\n此操作无法撤销！";
            Debug.Log("✓ deleteConfirmText 已显示和更新");
        }
        else
        {
            Debug.LogError("❌ deleteConfirmText 未配置");
        }

        Debug.Log("✓ 删除确认面板已显示");
    }

    /// <summary>
    /// 确认删除房间
    /// </summary>
    private void OnConfirmDelete( )
    {
        Debug.Log(">>> 确认删除房间");

        if (_currentRoom == null) return;

        try
        {
            // ★ 直接调用 RoomDataManager 的完整删除逻辑
            RoomDataManager.Instance.DeleteRoom(_currentRoom.roomId);

            HideDeleteConfirmPanel();
            ShowDeleteSuccessPanel();
            StartCoroutine(ReturnAfterDelay(2f));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ 删除房间失败: {ex.Message}");
            ShowDeleteFailurePanel(ex.Message);
        }
    }



    /// <summary>
    /// ★ 隐藏删除确认面板（同步隐藏背景和文本）
    /// </summary>
    private void HideDeleteConfirmPanel( )
    {
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.SetActive(false);

        // ★ 同步隐藏背景 Image
        if (deleteConfirmBackground != null)
            deleteConfirmBackground.enabled = false;

        // ★ 同步隐藏文本
        if (deleteConfirmText != null)
            deleteConfirmText.enabled = false;

        Debug.Log("✓ 删除确认面板已隐藏");
    }

    /// <summary>
    /// 取消删除
    /// </summary>
    private void OnCancelDelete( )
    {
        Debug.Log(">>> 取消删除房间");
        HideDeleteConfirmPanel();
    }

    /// <summary>
    /// ★ 显示删除成功提示（完整的 Image + Text 同步）
    /// </summary>
    private void ShowDeleteSuccessPanel( )
    {
        if (updateSuccessPanel != null)
        {
            // 显示面板容器
            updateSuccessPanel.SetActive(true);

            // ★ 同步显示背景 Image
            if (updateSuccessBackground != null)
            {
                updateSuccessBackground.enabled = true;
                Debug.Log("✓ updateSuccessBackground 已显示");
            }
            else
            {
                Debug.LogWarning("⚠ updateSuccessBackground 未配置，面板可能缺少背景");
            }

            // ★ 同步显示和更新文本（使用序列化字段，不用 GetComponentInChildren）
            if (updateSuccessText != null)
            {
                updateSuccessText.enabled = true;
                updateSuccessText.text = "房间已删除";
                Debug.Log("✓ updateSuccessText 已显示");
            }
            else
            {
                Debug.LogError("❌ updateSuccessText 未配置");
            }

            StartCoroutine(HideMessageAfterDelay(updateSuccessPanel, 2f));
        }
    }

    /// <summary>
    /// 显示删除失败提示
    /// </summary>
    private void ShowDeleteFailurePanel( string errorMessage )
    {
        if (updateSuccessPanel != null)
        {
            // 显示面板容器
            updateSuccessPanel.SetActive(true);

            // ★ 同步显示背景 Image
            if (updateSuccessBackground != null)
                updateSuccessBackground.enabled = true;

            // ★ 同步显示和更新文本
            if (updateSuccessText != null)
            {
                updateSuccessText.enabled = true;
                updateSuccessText.text = $"删除房间失败：{errorMessage}";
            }

            StartCoroutine(HideMessageAfterDelay(updateSuccessPanel, 3f));
        }
    }

    /// <summary>
    /// ★ 显示更新成功弹窗（完整的 Image + Text 同步）
    /// </summary>
    private void ShowUpdateSuccessPanel( )
    {
        if (updateSuccessPanel != null)
        {
            // 显示面板容器
            updateSuccessPanel.SetActive(true);

            // ★ 同步显示背景 Image
            if (updateSuccessBackground != null)
            {
                updateSuccessBackground.enabled = true;
                Debug.Log("✓ updateSuccessBackground 已显示");
            }
            else
            {
                Debug.LogWarning("⚠ updateSuccessBackground 未配置，面板可能缺少背景");
            }

            // ★ 同步显示和更新文本（使用序列化字段，不用 GetComponentInChildren）
            if (updateSuccessText != null)
            {
                updateSuccessText.enabled = true;
                updateSuccessText.text = "房间信息更新成功！";
                Debug.Log("✓ updateSuccessText 已显示");
            }
            else
            {
                Debug.LogError("❌ updateSuccessText 未配置");
            }

            Debug.Log("✓ 更新成功提示已显示");
            StartCoroutine(HideMessageAfterDelay(updateSuccessPanel, 3f));
        }
    }

    /// <summary>
    /// ★ 隐藏消息面板（同步隐藏背景和文本）
    /// </summary>
    private System.Collections.IEnumerator HideMessageAfterDelay( GameObject panel, float delay )
    {
        yield return new WaitForSeconds(delay);

        if (panel != null)
        {
            // 隐藏面板容器
            panel.SetActive(false);

            // ★ 同步隐藏背景 Image
            if (updateSuccessBackground != null)
                updateSuccessBackground.enabled = false;

            // ★ 同步隐藏文本
            if (updateSuccessText != null)
                updateSuccessText.enabled = false;

            Debug.Log("✓ 提示已隐藏");
        }
    }

    /// <summary>
    /// 延迟返回房间列表
    /// </summary>
    private System.Collections.IEnumerator ReturnAfterDelay( float delay )
    {
        yield return new WaitForSeconds(delay);
        OnReturnButtonClicked();
    }

    private void OnDestroy( )
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);

        if (returnButton != null)
            returnButton.onClick.RemoveListener(OnReturnButtonClicked);

        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnDeleteButtonClicked);

        if (deleteConfirmButton != null)
            deleteConfirmButton.onClick.RemoveListener(OnConfirmDelete);

        if (deleteCancelButton != null)
            deleteCancelButton.onClick.RemoveListener(OnCancelDelete);
    }
}
