using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ServerRoot : MonoBehaviour
{
    public static ServerRoot Instance;

    // 本机双开：填 "127.0.0.1"（默认，保持原样）
    // ⚠ 这个值只该在本地联机测试时改，别提交成局域网地址：别人拉下来会连到你的机器上。
    public const string ServerIP = "10.169.157.41";   // ← 本机双开用这个。局域网地址别提交，别人拉下来会连到你机器上
                                                      // public：HotUpdateManager 的 BaseUrl 也认这一个（游戏服 8011 + 静态目录 8080 同机）
    const int ServerPort = 8011;


    void Awake()
    {
        Application.runInBackground = true;

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        gameObject.AddComponent<AudioManager>();

        NetManager.Instance.Init(ServerIP, ServerPort);
    }

    private void Start()
    {

    }

    void Update()
    {
        NetManager.Instance.MsgUpdate();
    }
}