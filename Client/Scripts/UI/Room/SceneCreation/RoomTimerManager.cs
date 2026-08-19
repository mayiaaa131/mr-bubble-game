using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RoomTimerManager : MonoBehaviour
{
    private int _totalSeconds;
    private int _remainingSeconds;
    private System.Action _onTimeUp;

    private string _roomId;  // ★ 新增：房间ID
    private float _lastSaveTime = 0f;  // ★ 新增：定期保存间隔

    public void Init( int countdownSeconds, System.Action onTimeUp, string roomId = "" )
    {
        _totalSeconds = countdownSeconds;
        _remainingSeconds = countdownSeconds;
        _onTimeUp = onTimeUp;
        _roomId = roomId;
    }

    public void StartCountdown( )
    {
        StartCoroutine(CountdownCoroutine());
    }

    private System.Collections.IEnumerator CountdownCoroutine( )
    {
        _lastSaveTime = Time.time;

        while (_remainingSeconds > 0)
        {
            yield return new WaitForSeconds(1f);
            _remainingSeconds--;

            // ★ 每 10 秒保存一次进度到 JSON
            if (Time.time - _lastSaveTime >= 10f)
            {
                SaveTimerStateToJson();
                _lastSaveTime = Time.time;
            }

            Debug.Log($"[RoomTimerManager] 剩余时间: {_remainingSeconds}s");
        }

        // ★ 游戏结束时保存最终状态
        SaveTimerStateToJson();
        _onTimeUp?.Invoke();
    }

    /// <summary>
    /// ★ 新增：保存倒计时状态到 JSON
    /// </summary>
    private void SaveTimerStateToJson( )
    {
        if (string.IsNullOrEmpty(_roomId)) return;

        try
        {
            Room room = RoomDataManager.Instance.GetRoomById(_roomId);
            if (room != null)
            {
                room.remainingTime = _remainingSeconds;
                room.startTime = (int)(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() - (_totalSeconds - _remainingSeconds));


                // 更新到 JSON
                List<Room> rooms = JsonFileHandler.Instance.LoadRoomsData();
                var targetRoom = rooms.FirstOrDefault(r => r.roomId == _roomId);
                if (targetRoom != null)
                {
                    targetRoom.remainingTime = room.remainingTime;
                    targetRoom.startTime = room.startTime;
                    JsonFileHandler.Instance.SaveRoomsData(rooms);
                    Debug.Log($"✓ 倒计时状态已保存: {_remainingSeconds}s");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 保存倒计时状态失败: {e.Message}");
        }
    }

    /// <summary>
    /// ★ 新增：从 JSON 恢复倒计时状态（游戏中断后恢复）
    /// </summary>
    public void LoadTimerStateFromJson( )
    {
        if (string.IsNullOrEmpty(_roomId)) return;

        try
        {
            Room room = RoomDataManager.Instance.GetRoomById(_roomId);
            if (room != null && room.remainingTime > 0)
            {
                _remainingSeconds = room.remainingTime;
                Debug.Log($"✓ 从 JSON 恢复倒计时: {_remainingSeconds}s");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 加载倒计时状态失败: {e.Message}");
        }
    }

    public int GetRemainingSeconds( ) => _remainingSeconds;
}
