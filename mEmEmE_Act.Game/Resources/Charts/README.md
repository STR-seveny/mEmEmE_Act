# Charts

谱面文件**不包含在本仓库中**（属于关卡内容）。

Chart files are **not included in this repository** (they are level content).

要让游戏跑起来，请自备谱面放在本目录，命名为：

To get the game running, place your own chart here named:

```
1_1_1.me3
```

路径与文件名在 `mEmEmE_Act.Game\MainScreen.cs` 的 `LoadComplete()` 里指定：

The path and filename are set in `LoadComplete()` in `mEmEmE_Act.Game\MainScreen.cs`:

```csharp
var chartPath = Path.Combine(
    System.AppDomain.CurrentDomain.BaseDirectory,
    "Resources", "Charts", "1_1_1.me3"
);
```

**没有谱面时游戏会启动，但没有任何音符**（判定区、接收器仍在）。

**Without a chart the game still starts, but no notes appear** (the receptor and judgement zone are still drawn).

---

## 谱面格式 / Chart format

`.me3` 是纯文本，一行一条指令，`//` 开头为注释。

`.me3` is plain text, one command per line; lines starting with `//` are comments.

```
=me3
play(0.9,0)                     // 0.9 秒开始播放音频，从音频 0 秒处起
speed(0,10)                     // 0 秒时把下落速度设为 10
note(1.5,dE,0,0)                // 1.5 秒生成 dE 音符，X 参数 0，id 0
note(4.0,tE,-150,0)             // tE 音符，X 参数 -150
bug(24.28,SPT,0,0)              // 24.28 秒生成 SPT 触发块
end(37)
```

| 指令 / Command | 参数 / Arguments |
| --- | --- |
| `play(t, start)` | 播放音频，`start` 为音频起点（秒） |
| `speed(t, v)` | 设置下落速度（v 越大越快） |
| `note(t, type, x, id)` | 生成音符，`type` = `dE` / `tE` / `nE` |
| `bug(t, type, x, id)` | 生成触发块，`type` = `REV` / `SPT` |
| `move(t, id, x, y)` | 移动对象 |
| `effect(t, type, size)` | 演出特效（**未实现 / not implemented**） |
| `camera(t, x, y)` | 镜头移动（**未实现 / not implemented**） |
| `end(t)` | 结束 / end |
| `var(t, name, value)` | 设置变量 |

**`x` 参数**支持数值与表达式：

| 写法 / Form | 含义 / Meaning |
| --- | --- |
| `120` | 数值 / literal |
| `me_x` / `me_y` | 鼠标位置（受 REV 影响）/ mouse position (affected by REV) |
| `var.名字` | 变量值 / variable value |
| `id_my_x` / `id_my_y` | 某个对象的当前坐标 / another object's position |
| `random(最小/最大)` | 随机数 / random range |

音符类型与机制说明见根目录 `README.md` 的「音符类型」和「特殊机制」章节。

See the "Note Types" and "Gameplay Mechanics" sections of the root `README.md` for what each note type and mechanic does.

> 需要音乐的话，另见 `../Audio/README.md`。
> For audio, see `../Audio/README.md`.
