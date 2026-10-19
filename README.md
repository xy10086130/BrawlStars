# BrawlStar_URP

荒野乱斗风格的多人实时对战 Demo：**Unity URP 客户端 + 自研 C# TCP 服务端**。

包含 1v1 随机匹配、好友约战房、4 人淘汰模式，以及服务器权威移动、草丛隐身、资源热更等完整链路。

## 功能

- **1v1 对战**：随机匹配或好友约战房（房号加入），先选英雄再匹配
- **4 人淘汰模式**：一局一命，按死亡顺序排名
- **好友系统**：搜索、申请、同意/拒绝、删除、在线状态
- **服务器权威移动**：客户端只上报输入，服务端算位置与碰撞；客户端本地预测 + 快照校正
- **形状攻击判定**：扇形 / 矩形，与服务端共用同一份数值；前摇结束才结算，走位可以躲
- **草丛隐身**：可见性由服务端按观察者计算，隐身时真实坐标不下发
- **脱战回血**、弹药独立充能、命中反馈等战斗细节
- **资源热更**：按 HTTP `Last-Modified` 判版本，无需重新出包即可替换视频、图片、音频

## 技术栈

| 端 | 技术 |
|---|---|
| 客户端 | Unity 2022.3.62f3、URP、C# |
| 服务端 | .NET Framework 4.7.2、TCP（`Socket.Select` 多路复用）、Protobuf、AES |
| 数据 | MySQL + SqlSugar（CodeFirst 自动建表）、log4net |

网络层使用长度前缀拆包、环形缓冲区收包、反射分发协议；移动与碰撞代码由客户端/服务端共用同一份源码。

## 目录结构

```
Assets/
  Scenes/            # LoadingScene（入口/登录）→ TransitionScene → HallScene → MatchScene → BattleScene
  Scripts/
    Client/          # 网络、协议、序列化、AES
    Battle/          # 战斗表现、预测与校正
    Match/           # 匹配、房间
    Friend/          # 好友系统（MVC）
    HallScene/       # 大厅
    LoginScene/      # 登录注册、热更检查
    Hot/             # 资源热更
    UIFrame/         # UI 栈式框架
    Audio/           # 音频管理
  Editor/            # 编辑器工具
BrawlServer/
  SimpleServer/      # 游戏服入口（默认端口 8011）
  ServerBase/        # 网络基础库
  MySQL/             # 数据库访问与建表
  Data/              # 英雄数值
  Maps/              # 字符地图与出生点
```

## 本地运行

### 1. 启动服务端

1. 安装 MySQL，创建数据库 `brawlstars`（表由 SqlSugar CodeFirst 自动创建）
2. 用 Visual Studio 2022 打开 `BrawlServer/BrawlServer.sln`
3. 修改 `BrawlServer/MySQL/MySqlManager.cs` 中的 MySQL 连接串，改成你本机的账号密码
4. 将 `SimpleServer` 设为启动项目并运行，默认监听 `8011` 端口

### 2. 启动客户端

1. 用 Unity Hub 以 **Unity 2022.3.62f3** 打开项目根目录
2. 打开 `Assets/Scenes/LoadingScene.unity` 并运行
3. 在 `Assets/Scripts/Client/ServerRoot.cs` 中把 `ServerIP` 改成本机地址：
   - 本机双开：`127.0.0.1`
   - 局域网联机：服务端机器的局域网 IP

> 客户端和服务端需同时运行；首次进入先点「注册」创建账号，再登录。

## 协议速览

协议号按功能分段追加，不修改已有编号：

| 协议号 | 用途 |
|---|---|
| 1-4 | 密钥交换、心跳、注册、登录 |
| 33-37 | 好友：列表 / 搜索 / 申请 / 处理 / 删除 |
| 38-42 | 组队房间（建房/加入）、匹配人数、战斗加载同步 |
| 43-45 | 战斗：输入上报、状态快照、结算 |
| 46-48 | 随机匹配：入队 / 取消 / 匹配成功 |
| 49-54 | 房间开始、退出、状态推送、解散、一键邀请 |
