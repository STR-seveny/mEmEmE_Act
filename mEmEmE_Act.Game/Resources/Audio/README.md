# Audio

音乐文件**不包含在本仓库中**（授权仅限使用，不允许公开再分发）。

The music file is **not included in this repository** (the licence covers use only,
not public redistribution).

要让游戏正常出声，请自备音频放在本目录，命名为：

To hear audio in the game, place your own track here named:

```
1_1_1.mp3
```

路径与文件名在 `mEmEmE_Act.Game\MainScreen.cs` 的 `LoadComplete()` 里指定：

The path and filename are set in `LoadComplete()` in `mEmEmE_Act.Game\MainScreen.cs`:

```csharp
playfield.LoadChart(parser.Parse(chartPath), "Audio/1_1_1.mp3");
```

没有这个文件游戏仍可启动，只是没有音乐。
Without this file the game still starts, it just plays no music.
